-- =====================================================================
-- Gastrox – Skladový software pro restauraci
-- Inicializační skript SQLite databáze (sklad.db)
-- =====================================================================

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

-- ---------------------------------------------------------------------
-- SKLADY (fyzické lokace – Hlavní sklad, Bar, Kuchyň, ...)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Sklad (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Nazev       TEXT    NOT NULL UNIQUE,
    Je_Vychozi  INTEGER NOT NULL DEFAULT 0,
    Je_Aktivni  INTEGER NOT NULL DEFAULT 1,
    Poradi      INTEGER NOT NULL DEFAULT 0
);

-- Výchozí sklad (vloží se jen jednou)
INSERT INTO Sklad (Nazev, Je_Vychozi, Je_Aktivni, Poradi)
SELECT 'Hlavní sklad', 1, 1, 10
WHERE NOT EXISTS (SELECT 1 FROM Sklad);

-- ---------------------------------------------------------------------
-- SKLADOVÉ KARTY (katalog – sdílený napříč sklady)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SkladovaKarta (
    Id                           INTEGER PRIMARY KEY AUTOINCREMENT,
    Nazev                        TEXT    NOT NULL,
    Kategorie                    TEXT    NOT NULL,
    EAN                          TEXT,
    Evidencni_Jednotka           TEXT    NOT NULL,            -- Litr, Kg, Kus
    Typ_Baleni                   TEXT    NOT NULL,            -- Láhev 0,7l; Sud 50l
    Koeficient_Prepoctu          REAL    NOT NULL DEFAULT 1,  -- 1 balení * koef = evid. jednotky
    Aktualni_Stav_Evidencni      REAL    NOT NULL DEFAULT 0,
    Nakupni_Cena_Bez_DPH         REAL    NOT NULL DEFAULT 0,
    Sazba_DPH                    REAL    NOT NULL DEFAULT 21,
    Prodejni_Cena_S_DPH          REAL    NOT NULL DEFAULT 0,
    Minimalni_Stav               REAL    NOT NULL DEFAULT 0,
    Dodavatel                    TEXT,
    Je_Aktivni                   INTEGER NOT NULL DEFAULT 1,  -- boolean 0/1
    Datum_Posledni_Inventury     TEXT,                        -- ISO 8601
    Datum_Posledniho_Naskladneni TEXT
);

CREATE INDEX IF NOT EXISTS IX_SkladovaKarta_Nazev      ON SkladovaKarta(Nazev);
CREATE INDEX IF NOT EXISTS IX_SkladovaKarta_Kategorie  ON SkladovaKarta(Kategorie);
CREATE INDEX IF NOT EXISTS IX_SkladovaKarta_EAN        ON SkladovaKarta(EAN);
CREATE INDEX IF NOT EXISTS IX_SkladovaKarta_Aktivni    ON SkladovaKarta(Je_Aktivni);

-- ---------------------------------------------------------------------
-- STAV KARTY PER-SKLAD (nahrazuje SkladovaKarta.Aktualni_Stav_Evidencni)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SkladovyStav (
    SkladovaKarta_Id  INTEGER NOT NULL,
    Sklad_Id          INTEGER NOT NULL,
    Stav_Evidencni    REAL    NOT NULL DEFAULT 0,
    PRIMARY KEY (SkladovaKarta_Id, Sklad_Id),
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE CASCADE,
    FOREIGN KEY (Sklad_Id)         REFERENCES Sklad(Id)         ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_SkladovyStav_Sklad ON SkladovyStav(Sklad_Id);

-- Migrace: pro existující karty bez SkladovyStav přenést Aktualni_Stav_Evidencni do výchozího skladu
INSERT OR IGNORE INTO SkladovyStav (SkladovaKarta_Id, Sklad_Id, Stav_Evidencni)
SELECT k.Id, s.Id, k.Aktualni_Stav_Evidencni
  FROM SkladovaKarta k
  CROSS JOIN (SELECT Id FROM Sklad WHERE Je_Vychozi = 1 LIMIT 1) s
 WHERE NOT EXISTS (
     SELECT 1 FROM SkladovyStav ss
      WHERE ss.SkladovaKarta_Id = k.Id
 );

-- ---------------------------------------------------------------------
-- PŘÍJEMKY (hlavička)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Prijemka (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Cislo_Dokladu     TEXT    NOT NULL UNIQUE,
    Datum_Prijeti     TEXT    NOT NULL,
    Sklad_Id          INTEGER NOT NULL DEFAULT 1,
    Dodavatel         TEXT,
    Cislo_Faktury     TEXT,
    Poznamka          TEXT,
    Celkem_Bez_DPH    REAL    NOT NULL DEFAULT 0,
    Celkem_S_DPH      REAL    NOT NULL DEFAULT 0,
    Vytvoreno         TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (Sklad_Id) REFERENCES Sklad(Id) ON DELETE RESTRICT
);

-- Migrace: přidat Sklad_Id pokud chybí (pro existující DB)
-- (SQLite ALTER TABLE ADD COLUMN provádí jen z DatabaseService.Initialize)

CREATE TABLE IF NOT EXISTS PrijemkaRadek (
    Id                     INTEGER PRIMARY KEY AUTOINCREMENT,
    Prijemka_Id            INTEGER NOT NULL,
    SkladovaKarta_Id       INTEGER NOT NULL,
    Pocet_Baleni           REAL    NOT NULL,
    Koeficient_Prepoctu    REAL    NOT NULL,   -- snímek koef. v době naskladnění
    Mnozstvi_Evidencni     REAL    NOT NULL,   -- Pocet_Baleni * Koeficient
    Nakupni_Cena_Bez_DPH   REAL    NOT NULL,   -- za 1 balení
    Sazba_DPH              REAL    NOT NULL,
    Celkem_Bez_DPH         REAL    NOT NULL,
    Celkem_S_DPH           REAL    NOT NULL,
    FOREIGN KEY (Prijemka_Id)      REFERENCES Prijemka(Id)      ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_PrijemkaRadek_Prijemka ON PrijemkaRadek(Prijemka_Id);
CREATE INDEX IF NOT EXISTS IX_PrijemkaRadek_Karta    ON PrijemkaRadek(SkladovaKarta_Id);

-- ---------------------------------------------------------------------
-- VÝDEJKY (hlavička)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Vydejka (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    Cislo_Dokladu    TEXT    NOT NULL UNIQUE,
    Datum_Vydeje     TEXT    NOT NULL,
    Sklad_Id         INTEGER NOT NULL DEFAULT 1,
    Stredisko        TEXT    NOT NULL,            -- Bar, Kuchyně
    Typ_Vydeje       TEXT    NOT NULL,            -- Prodej, Vlastní spotřeba, Odpis-Zlom, Odpis-Sanitace, Odpis-Expirace
    Poznamka         TEXT,
    Vytvoreno        TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (Sklad_Id) REFERENCES Sklad(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS VydejkaRadek (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    Vydejka_Id           INTEGER NOT NULL,
    SkladovaKarta_Id     INTEGER NOT NULL,
    Mnozstvi_Evidencni   REAL    NOT NULL,        -- vždy se ukládá v evid. jednotkách
    Pocet_Baleni_Info    REAL,                    -- informativně (pokud byl zadán jako balení)
    Nakupni_Cena_Bez_DPH REAL,                    -- snímek ceny (pro reporting odpisů)
    FOREIGN KEY (Vydejka_Id)       REFERENCES Vydejka(Id)       ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_VydejkaRadek_Vydejka ON VydejkaRadek(Vydejka_Id);
CREATE INDEX IF NOT EXISTS IX_VydejkaRadek_Karta   ON VydejkaRadek(SkladovaKarta_Id);
CREATE INDEX IF NOT EXISTS IX_Vydejka_Typ          ON Vydejka(Typ_Vydeje);
CREATE INDEX IF NOT EXISTS IX_Vydejka_Datum        ON Vydejka(Datum_Vydeje);

-- ---------------------------------------------------------------------
-- INVENTURY
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Inventura (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    Datum_Inventury    TEXT    NOT NULL,
    Nazev              TEXT    NOT NULL,
    Sklad_Id           INTEGER NOT NULL DEFAULT 1,
    Je_Uzavrena        INTEGER NOT NULL DEFAULT 0,
    Poznamka           TEXT,
    Vytvoreno          TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (Sklad_Id) REFERENCES Sklad(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS InventuraRadek (
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    Inventura_Id          INTEGER NOT NULL,
    SkladovaKarta_Id      INTEGER NOT NULL,
    Teoreticky_Stav       REAL    NOT NULL,   -- stav v DB k datu inventury
    Fyzicky_Stav          REAL    NOT NULL,
    Rozdil                REAL    NOT NULL,   -- fyzický – teoretický
    FOREIGN KEY (Inventura_Id)     REFERENCES Inventura(Id)     ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

-- ---------------------------------------------------------------------
-- UZÁVĚRKY (snapshot stavů k datu)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Uzaverka (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Typ           TEXT    NOT NULL,   -- Denni, Tydenni, Mesicni, Rocni
    Datum_Od      TEXT    NOT NULL,
    Datum_Do      TEXT    NOT NULL,
    Sklad_Id      INTEGER,            -- NULL = souhrnná přes všechny sklady
    Vytvoreno     TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (Sklad_Id) REFERENCES Sklad(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS UzaverkaRadek (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Uzaverka_Id         INTEGER NOT NULL,
    SkladovaKarta_Id    INTEGER NOT NULL,
    Sklad_Id            INTEGER NOT NULL DEFAULT 1,
    Stav_Evidencni      REAL    NOT NULL,
    FOREIGN KEY (Uzaverka_Id)      REFERENCES Uzaverka(Id)      ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT,
    FOREIGN KEY (Sklad_Id)         REFERENCES Sklad(Id)         ON DELETE RESTRICT
);

-- ---------------------------------------------------------------------
-- POHYBY (deník všech změn na skladě – auditní stopa)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS PohybSkladu (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Datum               TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SkladovaKarta_Id    INTEGER NOT NULL,
    Sklad_Id            INTEGER NOT NULL DEFAULT 1,
    Typ_Pohybu          TEXT    NOT NULL,   -- Prijem, Vydej, InventurniKorekce, Prevod
    Mnozstvi_Evidencni  REAL    NOT NULL,   -- + pro příjem, – pro výdej
    Stav_Po_Pohybu      REAL    NOT NULL,
    Doklad_Typ          TEXT,               -- Prijemka, Vydejka, Inventura, Prevodka
    Doklad_Id           INTEGER,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT,
    FOREIGN KEY (Sklad_Id)         REFERENCES Sklad(Id)         ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_PohybSkladu_Karta ON PohybSkladu(SkladovaKarta_Id);
CREATE INDEX IF NOT EXISTS IX_PohybSkladu_Sklad ON PohybSkladu(Sklad_Id);
CREATE INDEX IF NOT EXISTS IX_PohybSkladu_Datum ON PohybSkladu(Datum);

-- ---------------------------------------------------------------------
-- PŘEVODKY (přesun zásob mezi sklady)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Prevodka (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Cislo_Dokladu     TEXT    NOT NULL UNIQUE,
    Datum_Prevodu     TEXT    NOT NULL,
    Sklad_Zdroj_Id    INTEGER NOT NULL,
    Sklad_Cil_Id      INTEGER NOT NULL,
    Poznamka          TEXT,
    Vytvoreno         TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (Sklad_Zdroj_Id) REFERENCES Sklad(Id) ON DELETE RESTRICT,
    FOREIGN KEY (Sklad_Cil_Id)   REFERENCES Sklad(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS PrevodkaRadek (
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    Prevodka_Id           INTEGER NOT NULL,
    SkladovaKarta_Id      INTEGER NOT NULL,
    Mnozstvi_Evidencni    REAL    NOT NULL,
    FOREIGN KEY (Prevodka_Id)      REFERENCES Prevodka(Id)      ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_PrevodkaRadek_Prevodka ON PrevodkaRadek(Prevodka_Id);
CREATE INDEX IF NOT EXISTS IX_Prevodka_Datum          ON Prevodka(Datum_Prevodu);

-- ---------------------------------------------------------------------
-- NASTAVENÍ (key/value: údaje o firmě, konfigurace aktualizací, …)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Nastaveni (
    Klic     TEXT PRIMARY KEY,
    Hodnota  TEXT
);

-- ---------------------------------------------------------------------
-- SAZBY DPH
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SazbaDPH (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Sazba        REAL    NOT NULL,
    Popis        TEXT    NOT NULL,
    Je_Vychozi   INTEGER NOT NULL DEFAULT 0,
    Je_Aktivni   INTEGER NOT NULL DEFAULT 1
);

-- Defaultní sazby (vloží se jen jednou, při prvním spuštění)
INSERT INTO SazbaDPH (Sazba, Popis, Je_Vychozi, Je_Aktivni)
SELECT 21, 'Základní 21 %', 1, 1
WHERE NOT EXISTS (SELECT 1 FROM SazbaDPH WHERE Sazba = 21);

INSERT INTO SazbaDPH (Sazba, Popis, Je_Vychozi, Je_Aktivni)
SELECT 12, 'Snížená 12 %', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM SazbaDPH WHERE Sazba = 12);

INSERT INTO SazbaDPH (Sazba, Popis, Je_Vychozi, Je_Aktivni)
SELECT 0, 'Nulová 0 %', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM SazbaDPH WHERE Sazba = 0);

-- ---------------------------------------------------------------------
-- KATEGORIE ZBOŽÍ (uživatelsky spravované v Nastavení)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Kategorie (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Nazev       TEXT    NOT NULL UNIQUE,
    Poradi      INTEGER NOT NULL DEFAULT 0,
    Je_Aktivni  INTEGER NOT NULL DEFAULT 1
);

-- Defaultní kategorie (vloží se jen jednou, při prvním spuštění)
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Tvrdý alkohol', 10, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Tvrdý alkohol');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Pivo',          20, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Pivo');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Víno',          30, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Víno');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Nealko',        40, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Nealko');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Káva/Čaj',      50, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Káva/Čaj');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Maso',          60, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Maso');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Zelenina',      70, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Zelenina');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Pečivo',        80, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Pečivo');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Mléčné',        90, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Mléčné');
INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni) SELECT 'Ostatní',      100, 1 WHERE NOT EXISTS (SELECT 1 FROM Kategorie WHERE Nazev = 'Ostatní');
