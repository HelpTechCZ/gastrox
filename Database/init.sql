-- =====================================================================
-- Gastrox – Skladový software pro restauraci
-- Inicializační skript SQLite databáze (sklad.db)
-- =====================================================================

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

-- ---------------------------------------------------------------------
-- SKLADOVÉ KARTY
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
-- PŘÍJEMKY (hlavička)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Prijemka (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Cislo_Dokladu     TEXT    NOT NULL UNIQUE,
    Datum_Prijeti     TEXT    NOT NULL,
    Dodavatel         TEXT,
    Cislo_Faktury     TEXT,
    Poznamka          TEXT,
    Celkem_Bez_DPH    REAL    NOT NULL DEFAULT 0,
    Celkem_S_DPH      REAL    NOT NULL DEFAULT 0,
    Vytvoreno         TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

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
    Stredisko        TEXT    NOT NULL,            -- Bar, Kuchyně
    Typ_Vydeje       TEXT    NOT NULL,            -- Prodej, Vlastní spotřeba, Odpis-Zlom, Odpis-Sanitace, Odpis-Expirace
    Poznamka         TEXT,
    Vytvoreno        TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP
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
    Je_Uzavrena        INTEGER NOT NULL DEFAULT 0,
    Poznamka           TEXT,
    Vytvoreno          TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP
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
    Vytvoreno     TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS UzaverkaRadek (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Uzaverka_Id         INTEGER NOT NULL,
    SkladovaKarta_Id    INTEGER NOT NULL,
    Stav_Evidencni      REAL    NOT NULL,
    FOREIGN KEY (Uzaverka_Id)      REFERENCES Uzaverka(Id)      ON DELETE CASCADE,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

-- ---------------------------------------------------------------------
-- POHYBY (deník všech změn na skladě – auditní stopa)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS PohybSkladu (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Datum               TEXT    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SkladovaKarta_Id    INTEGER NOT NULL,
    Typ_Pohybu          TEXT    NOT NULL,   -- Prijem, Vydej, InventurniKorekce
    Mnozstvi_Evidencni  REAL    NOT NULL,   -- + pro příjem, – pro výdej
    Stav_Po_Pohybu      REAL    NOT NULL,
    Doklad_Typ          TEXT,               -- Prijemka, Vydejka, Inventura
    Doklad_Id           INTEGER,
    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_PohybSkladu_Karta ON PohybSkladu(SkladovaKarta_Id);
CREATE INDEX IF NOT EXISTS IX_PohybSkladu_Datum ON PohybSkladu(Datum);

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
