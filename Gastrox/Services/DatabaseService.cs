using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gastrox.Models;
using Microsoft.Data.Sqlite;

namespace Gastrox.Services;

/// <summary>
/// Tenký wrapper nad SQLite. DB soubor se zakládá vedle .exe –
/// celá aplikace zůstává portable (zkopírujte složku, máte aplikaci i s daty).
/// </summary>
public static class DatabaseService
{
    public static string DbPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "sklad.db");

    public static string ConnectionString => $"Data Source={DbPath}";

    /// <summary>
    /// Vytvoří databázi a tabulky, pokud ještě neexistují.
    /// SQL skript je zabalený jako embedded resource přímo v binárce.
    /// </summary>
    public static void Initialize()
    {
        var script = LoadInitScript();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = script;
        cmd.ExecuteNonQuery();

        // Migrace pro existující DB – přidání Sklad_Id do stávajících tabulek
        RunMigrations(conn);
    }

    /// <summary>
    /// Migrace DB – přidává sloupce do existujících tabulek.
    /// SQLite nezná IF NOT EXISTS v ALTER TABLE, proto check přes PRAGMA.
    /// </summary>
    private static void RunMigrations(SqliteConnection conn)
    {
        void AddColumnIfMissing(string table, string column, string definition)
        {
            using var check = conn.CreateCommand();
            check.CommandText = $"PRAGMA table_info({table})";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return; // sloupec už existuje
            }
            reader.Close();

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }

        // Získat Id výchozího skladu
        using var def = conn.CreateCommand();
        def.CommandText = "SELECT Id FROM Sklad WHERE Je_Vychozi = 1 LIMIT 1";
        var defaultSkladId = Convert.ToInt32(def.ExecuteScalar() ?? 1);

        AddColumnIfMissing("Prijemka",      "Sklad_Id", $"INTEGER NOT NULL DEFAULT {defaultSkladId}");
        AddColumnIfMissing("Vydejka",       "Sklad_Id", $"INTEGER NOT NULL DEFAULT {defaultSkladId}");
        AddColumnIfMissing("Inventura",     "Sklad_Id", $"INTEGER NOT NULL DEFAULT {defaultSkladId}");
        AddColumnIfMissing("UzaverkaRadek", "Sklad_Id", $"INTEGER NOT NULL DEFAULT {defaultSkladId}");
        AddColumnIfMissing("Uzaverka",      "Sklad_Id", "INTEGER");
        AddColumnIfMissing("PohybSkladu",   "Sklad_Id", $"INTEGER NOT NULL DEFAULT {defaultSkladId}");

        // Pojistka pro starší DB: pokud init.sql z nějakého důvodu neaplikoval
        // nové tabulky pro převody mezi sklady, vytvoříme je explicitně zde.
        using (var ct = conn.CreateCommand())
        {
            ct.CommandText = @"
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
                CREATE INDEX IF NOT EXISTS IX_Prevodka_Datum         ON Prevodka(Datum_Prevodu);

                CREATE TABLE IF NOT EXISTS Sklad (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nazev       TEXT    NOT NULL UNIQUE,
                    Je_Vychozi  INTEGER NOT NULL DEFAULT 0,
                    Je_Aktivni  INTEGER NOT NULL DEFAULT 1,
                    Poradi      INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO Sklad (Nazev, Je_Vychozi, Je_Aktivni, Poradi)
                SELECT 'Hlavní sklad', 1, 1, 10
                WHERE NOT EXISTS (SELECT 1 FROM Sklad);

                CREATE TABLE IF NOT EXISTS SkladovyStav (
                    SkladovaKarta_Id  INTEGER NOT NULL,
                    Sklad_Id          INTEGER NOT NULL,
                    Stav_Evidencni    REAL    NOT NULL DEFAULT 0,
                    PRIMARY KEY (SkladovaKarta_Id, Sklad_Id),
                    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE CASCADE,
                    FOREIGN KEY (Sklad_Id)         REFERENCES Sklad(Id)         ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_SkladovyStav_Sklad ON SkladovyStav(Sklad_Id);

                -- Pro karty bez záznamu v SkladovyStav přenést aktuální stav do výchozího skladu
                INSERT OR IGNORE INTO SkladovyStav (SkladovaKarta_Id, Sklad_Id, Stav_Evidencni)
                SELECT k.Id, s.Id, k.Aktualni_Stav_Evidencni
                  FROM SkladovaKarta k
                  CROSS JOIN (SELECT Id FROM Sklad WHERE Je_Vychozi = 1 LIMIT 1) s
                 WHERE NOT EXISTS (
                     SELECT 1 FROM SkladovyStav ss WHERE ss.SkladovaKarta_Id = k.Id
                 );

                -- Varianty balení karty (Sud 50l, Sud 30l, Láhev 0,7l...)
                CREATE TABLE IF NOT EXISTS BaleniKarty (
                    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                    SkladovaKarta_Id      INTEGER NOT NULL,
                    Nazev                 TEXT    NOT NULL,
                    Koeficient_Prepoctu   REAL    NOT NULL DEFAULT 1,
                    Nakupni_Cena_Bez_DPH  REAL    NOT NULL DEFAULT 0,
                    Je_Vychozi            INTEGER NOT NULL DEFAULT 0,
                    Je_Aktivni            INTEGER NOT NULL DEFAULT 1,
                    Poradi                INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (SkladovaKarta_Id) REFERENCES SkladovaKarta(Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS IX_BaleniKarty_Karta ON BaleniKarty(SkladovaKarta_Id);

                -- Seed výchozích variant z polí karty pro existující karty
                INSERT INTO BaleniKarty (SkladovaKarta_Id, Nazev, Koeficient_Prepoctu, Nakupni_Cena_Bez_DPH, Je_Vychozi, Je_Aktivni, Poradi)
                SELECT k.Id,
                       CASE WHEN k.Typ_Baleni IS NULL OR trim(k.Typ_Baleni) = '' THEN 'Výchozí' ELSE k.Typ_Baleni END,
                       k.Koeficient_Prepoctu, k.Nakupni_Cena_Bez_DPH, 1, 1, 10
                  FROM SkladovaKarta k
                 WHERE NOT EXISTS (SELECT 1 FROM BaleniKarty b WHERE b.SkladovaKarta_Id = k.Id);";
            ct.ExecuteNonQuery();
        }
    }

    private static string LoadInitScript()
    {
        const string resourceName = "Gastrox.Database.init.sql";
        var asm = Assembly.GetExecutingAssembly();

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", asm.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' nenalezen. Dostupné: {available}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ----------------------------------------------------------------
    // Skladové karty
    // ----------------------------------------------------------------
    public static List<SkladovaKarta> LoadAktivniKarty()
    {
        var list = new List<SkladovaKarta>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Nazev, Kategorie, EAN, Evidencni_Jednotka, Typ_Baleni,
                   Koeficient_Prepoctu, Aktualni_Stav_Evidencni,
                   Nakupni_Cena_Bez_DPH, Sazba_DPH, Prodejni_Cena_S_DPH,
                   Minimalni_Stav, Dodavatel, Je_Aktivni,
                   Datum_Posledni_Inventury, Datum_Posledniho_Naskladneni
              FROM SkladovaKarta
             WHERE Je_Aktivni = 1
             ORDER BY Nazev";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SkladovaKarta
            {
                Id                          = reader.GetInt32(0),
                Nazev                       = reader.GetString(1),
                Kategorie                   = reader.GetString(2),
                EAN                         = reader.IsDBNull(3) ? null : reader.GetString(3),
                EvidencniJednotka           = reader.GetString(4),
                TypBaleni                   = reader.GetString(5),
                KoeficientPrepoctu          = (decimal)reader.GetDouble(6),
                AktualniStavEvidencni       = (decimal)reader.GetDouble(7),
                NakupniCenaBezDPH           = (decimal)reader.GetDouble(8),
                SazbaDPH                    = (decimal)reader.GetDouble(9),
                ProdejniCenaSDPH            = (decimal)reader.GetDouble(10),
                MinimalniStav               = (decimal)reader.GetDouble(11),
                Dodavatel                   = reader.IsDBNull(12) ? null : reader.GetString(12),
                JeAktivni                   = reader.GetInt32(13) == 1,
                DatumPosledniInventury      = reader.IsDBNull(14) ? null : DateTime.Parse(reader.GetString(14)),
                DatumPoslednihoNaskladneni  = reader.IsDBNull(15) ? null : DateTime.Parse(reader.GetString(15))
            });
        }

        return list;
    }

    /// <summary>
    /// Uloží novou kartu (Id == 0) nebo aktualizuje existující.
    /// Vrací Id karty.
    /// </summary>
    public static int SaveKarta(SkladovaKarta k)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        if (k.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO SkladovaKarta
                    (Nazev, Kategorie, EAN, Evidencni_Jednotka, Typ_Baleni,
                     Koeficient_Prepoctu, Aktualni_Stav_Evidencni,
                     Nakupni_Cena_Bez_DPH, Sazba_DPH, Prodejni_Cena_S_DPH,
                     Minimalni_Stav, Dodavatel, Je_Aktivni)
                VALUES
                    ($nazev, $kat, $ean, $ej, $tb,
                     $kp, $stav, $nc, $dph, $pc,
                     $min, $dod, 1);
                SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE SkladovaKarta SET
                    Nazev = $nazev,
                    Kategorie = $kat,
                    EAN = $ean,
                    Evidencni_Jednotka = $ej,
                    Typ_Baleni = $tb,
                    Koeficient_Prepoctu = $kp,
                    Nakupni_Cena_Bez_DPH = $nc,
                    Sazba_DPH = $dph,
                    Prodejni_Cena_S_DPH = $pc,
                    Minimalni_Stav = $min,
                    Dodavatel = $dod
                WHERE Id = $id;
                SELECT $id;";
            cmd.Parameters.AddWithValue("$id", k.Id);
        }

        cmd.Parameters.AddWithValue("$nazev", k.Nazev);
        cmd.Parameters.AddWithValue("$kat",   k.Kategorie);
        cmd.Parameters.AddWithValue("$ean",   (object?)k.EAN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ej",    k.EvidencniJednotka);
        cmd.Parameters.AddWithValue("$tb",    k.TypBaleni);
        cmd.Parameters.AddWithValue("$kp",    (double)k.KoeficientPrepoctu);
        cmd.Parameters.AddWithValue("$stav",  (double)k.AktualniStavEvidencni);
        cmd.Parameters.AddWithValue("$nc",    (double)k.NakupniCenaBezDPH);
        cmd.Parameters.AddWithValue("$dph",   (double)k.SazbaDPH);
        cmd.Parameters.AddWithValue("$pc",    (double)k.ProdejniCenaSDPH);
        cmd.Parameters.AddWithValue("$min",   (double)k.MinimalniStav);
        cmd.Parameters.AddWithValue("$dod",   (object?)k.Dodavatel ?? DBNull.Value);

        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    /// <summary>Místo mazání se karta pouze deaktivuje.</summary>
    public static void DeactivateKarta(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE SkladovaKarta SET Je_Aktivni = 0 WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------
    // Varianty balení karty
    // ----------------------------------------------------------------
    public static List<BaleniKarty> LoadBaleniProKartu(int kartaId)
    {
        var list = new List<BaleniKarty>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, SkladovaKarta_Id, Nazev, Koeficient_Prepoctu,
                   Nakupni_Cena_Bez_DPH, Je_Vychozi, Je_Aktivni, Poradi
              FROM BaleniKarty
             WHERE SkladovaKarta_Id = $kid AND Je_Aktivni = 1
             ORDER BY Je_Vychozi DESC, Poradi, Id";
        cmd.Parameters.AddWithValue("$kid", kartaId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new BaleniKarty
            {
                Id                 = r.GetInt32(0),
                SkladovaKartaId    = r.GetInt32(1),
                Nazev              = r.GetString(2),
                KoeficientPrepoctu = (decimal)r.GetDouble(3),
                NakupniCenaBezDPH  = (decimal)r.GetDouble(4),
                JeVychozi          = r.GetInt32(5) == 1,
                JeAktivni          = r.GetInt32(6) == 1,
                Poradi             = r.GetInt32(7)
            });
        }
        return list;
    }

    /// <summary>
    /// Uloží kompletní seznam variant pro danou kartu (replace-all v transakci).
    /// Vymažou se varianty, které už v seznamu nejsou; nové (Id=0) se vloží;
    /// existující se aktualizují. Právě jedna musí mít Je_Vychozi=1.
    /// </summary>
    public static void SaveBaleniProKartu(int kartaId, IEnumerable<BaleniKarty> baleni)
    {
        var list = new List<BaleniKarty>(baleni);
        if (list.Count == 0)
            throw new InvalidOperationException("Karta musí mít alespoň jednu variantu balení.");
        if (list.Count(b => b.JeVychozi) != 1)
            throw new InvalidOperationException("Právě jedna varianta musí být označena jako výchozí.");

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Zjistit existující Id v DB
        var existujici = new HashSet<int>();
        using (var q = conn.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "SELECT Id FROM BaleniKarty WHERE SkladovaKarta_Id = $kid";
            q.Parameters.AddWithValue("$kid", kartaId);
            using var r = q.ExecuteReader();
            while (r.Read()) existujici.Add(r.GetInt32(0));
        }

        var zachovat = new HashSet<int>();
        int poradi = 10;
        foreach (var b in list)
        {
            if (b.Id == 0)
            {
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO BaleniKarty
                        (SkladovaKarta_Id, Nazev, Koeficient_Prepoctu, Nakupni_Cena_Bez_DPH,
                         Je_Vychozi, Je_Aktivni, Poradi)
                    VALUES ($kid, $nazev, $kp, $nc, $vych, 1, $por);
                    SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$kid",   kartaId);
                ins.Parameters.AddWithValue("$nazev", b.Nazev);
                ins.Parameters.AddWithValue("$kp",    (double)b.KoeficientPrepoctu);
                ins.Parameters.AddWithValue("$nc",    (double)b.NakupniCenaBezDPH);
                ins.Parameters.AddWithValue("$vych",  b.JeVychozi ? 1 : 0);
                ins.Parameters.AddWithValue("$por",   poradi);
                var newId = Convert.ToInt32(ins.ExecuteScalar());
                b.Id = newId;
                zachovat.Add(newId);
            }
            else
            {
                using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = @"
                    UPDATE BaleniKarty SET
                        Nazev = $nazev,
                        Koeficient_Prepoctu = $kp,
                        Nakupni_Cena_Bez_DPH = $nc,
                        Je_Vychozi = $vych,
                        Poradi = $por
                     WHERE Id = $id AND SkladovaKarta_Id = $kid";
                upd.Parameters.AddWithValue("$id",    b.Id);
                upd.Parameters.AddWithValue("$kid",   kartaId);
                upd.Parameters.AddWithValue("$nazev", b.Nazev);
                upd.Parameters.AddWithValue("$kp",    (double)b.KoeficientPrepoctu);
                upd.Parameters.AddWithValue("$nc",    (double)b.NakupniCenaBezDPH);
                upd.Parameters.AddWithValue("$vych",  b.JeVychozi ? 1 : 0);
                upd.Parameters.AddWithValue("$por",   poradi);
                upd.ExecuteNonQuery();
                zachovat.Add(b.Id);
            }
            poradi += 10;
        }

        // Smazat varianty, které už nejsou v seznamu
        foreach (var id in existujici)
        {
            if (zachovat.Contains(id)) continue;
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM BaleniKarty WHERE Id = $id";
            del.Parameters.AddWithValue("$id", id);
            del.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static List<SkladovaKarta> LoadKartyPodLimitem()
    {
        var all = LoadAktivniKarty();
        var result = new List<SkladovaKarta>();
        foreach (var k in all)
            if (k.MinimalniStav > 0 && k.AktualniStavEvidencni <= k.MinimalniStav)
                result.Add(k);
        return result;
    }

    // ----------------------------------------------------------------
    // Nastavení (key/value)
    // ----------------------------------------------------------------
    public static Dictionary<string, string> LoadNastaveni()
    {
        var dict = new Dictionary<string, string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Klic, Hodnota FROM Nastaveni";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var klic = reader.GetString(0);
            var hod = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            dict[klic] = hod;
        }
        return dict;
    }

    public static void SaveNastaveni(string klic, string? hodnota)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Nastaveni (Klic, Hodnota) VALUES ($k, $v)
            ON CONFLICT(Klic) DO UPDATE SET Hodnota = excluded.Hodnota;";
        cmd.Parameters.AddWithValue("$k", klic);
        cmd.Parameters.AddWithValue("$v", (object?)hodnota ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static void SaveNastaveniBulk(Dictionary<string, string?> values)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        foreach (var kv in values)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Nastaveni (Klic, Hodnota) VALUES ($k, $v)
                ON CONFLICT(Klic) DO UPDATE SET Hodnota = excluded.Hodnota;";
            cmd.Parameters.AddWithValue("$k", kv.Key);
            cmd.Parameters.AddWithValue("$v", (object?)kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ----------------------------------------------------------------
    // Sazby DPH
    // ----------------------------------------------------------------
    public static List<SazbaDPH> LoadAktivniSazbyDph()
    {
        var list = new List<SazbaDPH>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Sazba, Popis, Je_Vychozi, Je_Aktivni
              FROM SazbaDPH
             WHERE Je_Aktivni = 1
             ORDER BY Je_Vychozi DESC, Sazba DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SazbaDPH
            {
                Id        = reader.GetInt32(0),
                Sazba     = (decimal)reader.GetDouble(1),
                Popis     = reader.GetString(2),
                JeVychozi = reader.GetInt32(3) == 1,
                JeAktivni = reader.GetInt32(4) == 1
            });
        }
        return list;
    }

    public static int SaveSazbaDph(SazbaDPH s)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        if (s.JeVychozi)
        {
            // jen jedna výchozí sazba
            using var reset = conn.CreateCommand();
            reset.CommandText = "UPDATE SazbaDPH SET Je_Vychozi = 0";
            reset.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        if (s.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO SazbaDPH (Sazba, Popis, Je_Vychozi, Je_Aktivni)
                VALUES ($sazba, $popis, $vych, 1);
                SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE SazbaDPH
                   SET Sazba = $sazba, Popis = $popis, Je_Vychozi = $vych
                 WHERE Id = $id;
                SELECT $id;";
            cmd.Parameters.AddWithValue("$id", s.Id);
        }
        cmd.Parameters.AddWithValue("$sazba", (double)s.Sazba);
        cmd.Parameters.AddWithValue("$popis", s.Popis);
        cmd.Parameters.AddWithValue("$vych",  s.JeVychozi ? 1 : 0);

        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public static void DeactivateSazbaDph(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE SazbaDPH SET Je_Aktivni = 0 WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------
    // Kategorie zboží
    // ----------------------------------------------------------------
    public static List<Kategorie> LoadAktivniKategorie()
    {
        var list = new List<Kategorie>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Nazev, Poradi, Je_Aktivni
              FROM Kategorie
             WHERE Je_Aktivni = 1
             ORDER BY Poradi, Nazev";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Kategorie
            {
                Id        = reader.GetInt32(0),
                Nazev     = reader.GetString(1),
                Poradi    = reader.GetInt32(2),
                JeAktivni = reader.GetInt32(3) == 1
            });
        }
        return list;
    }

    public static int SaveKategorie(Kategorie k)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        if (k.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Kategorie (Nazev, Poradi, Je_Aktivni)
                VALUES ($nazev, $por, 1);
                SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Kategorie
                   SET Nazev = $nazev, Poradi = $por
                 WHERE Id = $id;
                SELECT $id;";
            cmd.Parameters.AddWithValue("$id", k.Id);
        }
        cmd.Parameters.AddWithValue("$nazev", k.Nazev);
        cmd.Parameters.AddWithValue("$por", k.Poradi);
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public static void DeactivateKategorie(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Kategorie SET Je_Aktivni = 0 WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------
    // Příjemka – uložení v transakci, aktualizace stavu skladu
    // ----------------------------------------------------------------
    public static int SavePrijemka(Prijemka p)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Zdroj/cíl skladu - pokud nezvolen, použij výchozí
        var skladId = p.SkladId > 0 ? p.SkladId : (LoadVychoziSklad()?.Id ?? 0);

        // 1) hlavička
        var cmdHead = conn.CreateCommand();
        cmdHead.Transaction = tx;
        cmdHead.CommandText = @"
            INSERT INTO Prijemka (Cislo_Dokladu, Datum_Prijeti, Dodavatel, Cislo_Faktury, Poznamka, Celkem_Bez_DPH, Celkem_S_DPH, Sklad_Id)
            VALUES ($cislo, $datum, $dod, $fak, $pozn, $cbez, $csdph, $sid);
            SELECT last_insert_rowid();";

        cmdHead.Parameters.AddWithValue("$cislo", p.CisloDokladu);
        cmdHead.Parameters.AddWithValue("$datum", p.DatumPrijeti.ToString("o"));
        cmdHead.Parameters.AddWithValue("$dod",   (object?)p.Dodavatel ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$fak",   (object?)p.CisloFaktury ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$pozn",  (object?)p.Poznamka ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$cbez",  (double)p.CelkemBezDPH);
        cmdHead.Parameters.AddWithValue("$csdph", (double)p.CelkemSDPH);
        cmdHead.Parameters.AddWithValue("$sid",   skladId);

        var prijemkaId = (long)(cmdHead.ExecuteScalar() ?? 0L);

        // 2) řádky + aktualizace skladu + auditní pohyb
        foreach (var r in p.Radky)
        {
            var mnozstviEv = r.PocetBaleni * r.KoeficientPrepoctu;

            var cmdRadek = conn.CreateCommand();
            cmdRadek.Transaction = tx;
            cmdRadek.CommandText = @"
                INSERT INTO PrijemkaRadek
                    (Prijemka_Id, SkladovaKarta_Id, Pocet_Baleni, Koeficient_Prepoctu,
                     Mnozstvi_Evidencni, Nakupni_Cena_Bez_DPH, Sazba_DPH, Celkem_Bez_DPH, Celkem_S_DPH)
                VALUES ($pid, $kid, $pb, $kp, $me, $cena, $dph, $cbez, $csdph);";
            cmdRadek.Parameters.AddWithValue("$pid",  prijemkaId);
            cmdRadek.Parameters.AddWithValue("$kid",  r.SkladovaKartaId);
            cmdRadek.Parameters.AddWithValue("$pb",   (double)r.PocetBaleni);
            cmdRadek.Parameters.AddWithValue("$kp",   (double)r.KoeficientPrepoctu);
            cmdRadek.Parameters.AddWithValue("$me",   (double)mnozstviEv);
            cmdRadek.Parameters.AddWithValue("$cena", (double)r.NakupniCenaBezDPH);
            cmdRadek.Parameters.AddWithValue("$dph",  (double)r.SazbaDPH);
            cmdRadek.Parameters.AddWithValue("$cbez", (double)r.CelkemBezDPH);
            cmdRadek.Parameters.AddWithValue("$csdph",(double)r.CelkemSDPH);
            cmdRadek.ExecuteNonQuery();

            // Aktualizace stavu v konkrétním skladu + zápis pohybu
            AplikujPohybInternal(conn, tx, r.SkladovaKartaId, skladId,
                mnozstviEv, "Prijem", "Prijemka", (int)prijemkaId);

            // Aktualizace nákupní ceny + data posledního naskladnění na kartě
            var cmdUpd = conn.CreateCommand();
            cmdUpd.Transaction = tx;
            cmdUpd.CommandText = @"
                UPDATE SkladovaKarta
                   SET Nakupni_Cena_Bez_DPH = $cena,
                       Datum_Posledniho_Naskladneni = $dat
                 WHERE Id = $kid;";
            cmdUpd.Parameters.AddWithValue("$cena", (double)r.NakupniCenaBezDPH);
            cmdUpd.Parameters.AddWithValue("$dat",  p.DatumPrijeti.ToString("o"));
            cmdUpd.Parameters.AddWithValue("$kid",  r.SkladovaKartaId);
            cmdUpd.ExecuteNonQuery();
        }

        tx.Commit();
        return (int)prijemkaId;
    }

    // ----------------------------------------------------------------
    // Výdejka – uložení v transakci, ochrana proti mínusovému stavu
    // ----------------------------------------------------------------
    public static int SaveVydejka(Vydejka v)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Zdrojový sklad - pokud nezvolen, použij výchozí
        var skladId = v.SkladId > 0 ? v.SkladId : (LoadVychoziSklad()?.Id ?? 0);

        // Ověření stavu v daném skladu před zápisem
        foreach (var r in v.Radky)
        {
            using var check = conn.CreateCommand();
            check.Transaction = tx;
            check.CommandText = @"
                SELECT COALESCE((SELECT Stav_Evidencni FROM SkladovyStav
                                  WHERE SkladovaKarta_Id = $id AND Sklad_Id = $sid), 0) AS stav,
                       (SELECT Nazev FROM SkladovaKarta WHERE Id = $id) AS nazev";
            check.Parameters.AddWithValue("$id",  r.SkladovaKartaId);
            check.Parameters.AddWithValue("$sid", skladId);
            using var rd = check.ExecuteReader();
            if (!rd.Read())
                throw new InvalidOperationException($"Karta Id={r.SkladovaKartaId} neexistuje.");
            var stav = (decimal)rd.GetDouble(0);
            var nazev = rd.IsDBNull(1) ? $"Id={r.SkladovaKartaId}" : rd.GetString(1);
            if (stav < r.MnozstviEvidencni)
                throw new InvalidOperationException(
                    $"Nedostatek zásob u '{nazev}' ve vybraném skladu: k dispozici {stav:N2}, požadováno {r.MnozstviEvidencni:N2}.");
        }

        var head = conn.CreateCommand();
        head.Transaction = tx;
        head.CommandText = @"
            INSERT INTO Vydejka (Cislo_Dokladu, Datum_Vydeje, Stredisko, Typ_Vydeje, Poznamka, Sklad_Id)
            VALUES ($cislo, $datum, $stred, $typ, $pozn, $sid);
            SELECT last_insert_rowid();";
        head.Parameters.AddWithValue("$cislo", v.CisloDokladu);
        head.Parameters.AddWithValue("$datum", v.DatumVydeje.ToString("o"));
        head.Parameters.AddWithValue("$stred", v.Stredisko.ToString());
        head.Parameters.AddWithValue("$typ",   v.TypVydeje.ToString());
        head.Parameters.AddWithValue("$pozn",  (object?)v.Poznamka ?? DBNull.Value);
        head.Parameters.AddWithValue("$sid",   skladId);
        var vydejkaId = (long)(head.ExecuteScalar() ?? 0L);

        foreach (var r in v.Radky)
        {
            var cmdR = conn.CreateCommand();
            cmdR.Transaction = tx;
            cmdR.CommandText = @"
                INSERT INTO VydejkaRadek
                    (Vydejka_Id, SkladovaKarta_Id, Mnozstvi_Evidencni, Pocet_Baleni_Info, Nakupni_Cena_Bez_DPH)
                VALUES ($vid, $kid, $me, $pbi, $nc);";
            cmdR.Parameters.AddWithValue("$vid", vydejkaId);
            cmdR.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
            cmdR.Parameters.AddWithValue("$me",  (double)r.MnozstviEvidencni);
            cmdR.Parameters.AddWithValue("$pbi", r.PocetBaleniInfo.HasValue ? (double)r.PocetBaleniInfo.Value : (object)DBNull.Value);
            cmdR.Parameters.AddWithValue("$nc",  r.NakupniCenaBezDPH.HasValue ? (double)r.NakupniCenaBezDPH.Value : (object)DBNull.Value);
            cmdR.ExecuteNonQuery();

            // Aktualizace stavu ve zdrojovém skladu (záporná změna) + pohyb
            AplikujPohybInternal(conn, tx, r.SkladovaKartaId, skladId,
                -r.MnozstviEvidencni, "Vydej", "Vydejka", (int)vydejkaId);
        }

        tx.Commit();
        return (int)vydejkaId;
    }

    // ----------------------------------------------------------------
    // Inventura – uložení s dopočtem rozdílů a korekcí stavů
    // ----------------------------------------------------------------
    public static int SaveInventura(Inventura inv, bool uzavrit)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        var head = conn.CreateCommand();
        head.Transaction = tx;
        head.CommandText = @"
            INSERT INTO Inventura (Datum_Inventury, Nazev, Je_Uzavrena, Poznamka)
            VALUES ($datum, $nazev, $uz, $pozn);
            SELECT last_insert_rowid();";
        head.Parameters.AddWithValue("$datum", inv.DatumInventury.ToString("o"));
        head.Parameters.AddWithValue("$nazev", inv.Nazev);
        head.Parameters.AddWithValue("$uz",    uzavrit ? 1 : 0);
        head.Parameters.AddWithValue("$pozn",  (object?)inv.Poznamka ?? DBNull.Value);
        var invId = (long)(head.ExecuteScalar() ?? 0L);

        foreach (var r in inv.Radky)
        {
            var rozdil = r.FyzickyStav - r.TeoretickyStav;

            var cmdR = conn.CreateCommand();
            cmdR.Transaction = tx;
            cmdR.CommandText = @"
                INSERT INTO InventuraRadek (Inventura_Id, SkladovaKarta_Id, Teoreticky_Stav, Fyzicky_Stav, Rozdil)
                VALUES ($iid, $kid, $tst, $fst, $rd);";
            cmdR.Parameters.AddWithValue("$iid", invId);
            cmdR.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
            cmdR.Parameters.AddWithValue("$tst", (double)r.TeoretickyStav);
            cmdR.Parameters.AddWithValue("$fst", (double)r.FyzickyStav);
            cmdR.Parameters.AddWithValue("$rd",  (double)rozdil);
            cmdR.ExecuteNonQuery();

            if (uzavrit)
            {
                var cmdU = conn.CreateCommand();
                cmdU.Transaction = tx;
                cmdU.CommandText = @"
                    UPDATE SkladovaKarta
                       SET Aktualni_Stav_Evidencni = $fst,
                           Datum_Posledni_Inventury = $dat
                     WHERE Id = $kid;";
                cmdU.Parameters.AddWithValue("$fst", (double)r.FyzickyStav);
                cmdU.Parameters.AddWithValue("$dat", inv.DatumInventury.ToString("o"));
                cmdU.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
                cmdU.ExecuteNonQuery();

                if (rozdil != 0)
                {
                    var cmdP = conn.CreateCommand();
                    cmdP.Transaction = tx;
                    cmdP.CommandText = @"
                        INSERT INTO PohybSkladu (SkladovaKarta_Id, Typ_Pohybu, Mnozstvi_Evidencni, Stav_Po_Pohybu, Doklad_Typ, Doklad_Id)
                        VALUES ($kid, 'InventurniKorekce', $rd,
                                (SELECT Aktualni_Stav_Evidencni FROM SkladovaKarta WHERE Id = $kid),
                                'Inventura', $iid);";
                    cmdP.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
                    cmdP.Parameters.AddWithValue("$rd",  (double)rozdil);
                    cmdP.Parameters.AddWithValue("$iid", invId);
                    cmdP.ExecuteNonQuery();
                }
            }
        }

        tx.Commit();
        return (int)invId;
    }

    // ----------------------------------------------------------------
    // Uzávěrka skladu (snapshot aktuálních stavů)
    // ----------------------------------------------------------------
    public static void SaveUzaverka(DateTime datum, List<SkladovaKarta> karty, int? skladId = null)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var cmdH = conn.CreateCommand();
        cmdH.Transaction = tx;
        cmdH.CommandText = @"
            INSERT INTO Uzaverka (Typ, Datum_Od, Datum_Do, Vytvoreno, Sklad_Id)
            VALUES ('Manualni', $datum, $datum, $now, $sid);
            SELECT last_insert_rowid();";
        cmdH.Parameters.AddWithValue("$datum", datum.ToString("o"));
        cmdH.Parameters.AddWithValue("$now",   DateTime.Now.ToString("o"));
        cmdH.Parameters.AddWithValue("$sid",   (object?)skladId ?? DBNull.Value);
        var uzId = cmdH.ExecuteScalar()!;

        // Pro UzaverkaRadek je Sklad_Id NOT NULL – při souhrnné uzávěrce fallback na výchozí sklad
        var radekSkladId = skladId ?? (LoadVychoziSklad()?.Id ?? 1);

        using var cmdR = conn.CreateCommand();
        cmdR.Transaction = tx;
        cmdR.CommandText = @"
            INSERT INTO UzaverkaRadek (Uzaverka_Id, SkladovaKarta_Id, Stav_Evidencni, Sklad_Id)
            VALUES ($uid, $kid, $stav, $sid)";
        cmdR.Parameters.AddWithValue("$uid",  0);
        cmdR.Parameters.AddWithValue("$kid",  0);
        cmdR.Parameters.AddWithValue("$stav", 0.0);
        cmdR.Parameters.AddWithValue("$sid",  radekSkladId);

        foreach (var k in karty)
        {
            cmdR.Parameters["$uid"].Value  = Convert.ToInt64(uzId);
            cmdR.Parameters["$kid"].Value  = k.Id;
            cmdR.Parameters["$stav"].Value = (double)k.AktualniStavEvidencni;
            cmdR.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ----------------------------------------------------------------
    // Pohyby na kartě (deník / historie)
    // ----------------------------------------------------------------
    public static List<PohybSkladu> LoadPohybyKarty(int kartaId, int limit = 200)
    {
        var list = new List<PohybSkladu>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Datum, SkladovaKarta_Id, Typ_Pohybu,
                   Mnozstvi_Evidencni, Stav_Po_Pohybu, Doklad_Typ, Doklad_Id
              FROM PohybSkladu
             WHERE SkladovaKarta_Id = $kid
             ORDER BY Datum DESC, Id DESC
             LIMIT $lim";
        cmd.Parameters.AddWithValue("$kid", kartaId);
        cmd.Parameters.AddWithValue("$lim", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PohybSkladu
            {
                Id                = reader.GetInt32(0),
                Datum             = DateTime.Parse(reader.GetString(1)),
                SkladovaKartaId   = reader.GetInt32(2),
                TypPohybu         = reader.GetString(3),
                MnozstviEvidencni = (decimal)reader.GetDouble(4),
                StavPoPohybu      = (decimal)reader.GetDouble(5),
                DokladTyp         = reader.IsDBNull(6) ? null : reader.GetString(6),
                DokladId          = reader.IsDBNull(7) ? null : reader.GetInt32(7)
            });
        }
        return list;
    }

    // ----------------------------------------------------------------
    // Příjemky – seznam hlaviček
    // ----------------------------------------------------------------
    public static List<Prijemka> LoadPrijemky(int limit = 500, int? skladId = null)
    {
        var list = new List<Prijemka>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Cislo_Dokladu, p.Datum_Prijeti, p.Dodavatel, p.Cislo_Faktury,
                   p.Poznamka, p.Celkem_Bez_DPH, p.Celkem_S_DPH,
                   p.Sklad_Id, COALESCE(s.Nazev, '') AS SkladNazev
              FROM Prijemka p
              LEFT JOIN Sklad s ON s.Id = p.Sklad_Id
             WHERE ($sid IS NULL OR p.Sklad_Id = $sid)
             ORDER BY p.Datum_Prijeti DESC, p.Id DESC
             LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);
        cmd.Parameters.AddWithValue("$sid", (object?)skladId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Prijemka
            {
                Id            = reader.GetInt32(0),
                CisloDokladu  = reader.GetString(1),
                DatumPrijeti  = DateTime.Parse(reader.GetString(2)),
                Dodavatel     = reader.IsDBNull(3) ? null : reader.GetString(3),
                CisloFaktury  = reader.IsDBNull(4) ? null : reader.GetString(4),
                Poznamka      = reader.IsDBNull(5) ? null : reader.GetString(5),
                CelkemBezDPH  = (decimal)reader.GetDouble(6),
                CelkemSDPH    = (decimal)reader.GetDouble(7),
                SkladId       = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                SkladNazev    = reader.GetString(9)
            });
        }
        return list;
    }

    public static List<PrijemkaRadek> LoadPrijemkaRadky(int prijemkaId)
    {
        var list = new List<PrijemkaRadek>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT pr.Id, pr.SkladovaKarta_Id, sk.Nazev, pr.Pocet_Baleni,
                   pr.Koeficient_Prepoctu, pr.Mnozstvi_Evidencni,
                   pr.Nakupni_Cena_Bez_DPH, pr.Sazba_DPH, pr.Celkem_Bez_DPH, pr.Celkem_S_DPH,
                   sk.Evidencni_Jednotka, sk.Typ_Baleni
              FROM PrijemkaRadek pr
              JOIN SkladovaKarta sk ON sk.Id = pr.SkladovaKarta_Id
             WHERE pr.Prijemka_Id = $pid
             ORDER BY pr.Id";
        cmd.Parameters.AddWithValue("$pid", prijemkaId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PrijemkaRadek
            {
                Id                 = reader.GetInt32(0),
                SkladovaKartaId    = reader.GetInt32(1),
                NazevZbozi         = reader.GetString(2),
                PocetBaleni        = (decimal)reader.GetDouble(3),
                KoeficientPrepoctu = (decimal)reader.GetDouble(4),
                NakupniCenaBezDPH  = (decimal)reader.GetDouble(6),
                SazbaDPH           = (decimal)reader.GetDouble(7),
                EvidencniJednotka  = reader.GetString(10),
                TypBaleni          = reader.GetString(11)
            });
        }
        return list;
    }

    // ----------------------------------------------------------------
    // Výdejky – seznam hlaviček
    // ----------------------------------------------------------------
    public static List<Vydejka> LoadVydejky(int limit = 500, int? skladId = null)
    {
        var list = new List<Vydejka>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT v.Id, v.Cislo_Dokladu, v.Datum_Vydeje, v.Stredisko, v.Typ_Vydeje, v.Poznamka,
                   v.Sklad_Id, COALESCE(s.Nazev, '') AS SkladNazev
              FROM Vydejka v
              LEFT JOIN Sklad s ON s.Id = v.Sklad_Id
             WHERE ($sid IS NULL OR v.Sklad_Id = $sid)
             ORDER BY v.Datum_Vydeje DESC, v.Id DESC
             LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);
        cmd.Parameters.AddWithValue("$sid", (object?)skladId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var stredStr = reader.GetString(3);
            var typStr   = reader.GetString(4);
            Enum.TryParse<Stredisko>(stredStr, out var stred);
            Enum.TryParse<TypVydeje>(typStr, out var typ);

            list.Add(new Vydejka
            {
                Id           = reader.GetInt32(0),
                CisloDokladu = reader.GetString(1),
                DatumVydeje  = DateTime.Parse(reader.GetString(2)),
                Stredisko    = stred,
                TypVydeje    = typ,
                Poznamka     = reader.IsDBNull(5) ? null : reader.GetString(5),
                SkladId      = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                SkladNazev   = reader.GetString(7)
            });
        }
        return list;
    }

    public static List<VydejkaRadek> LoadVydejkaRadky(int vydejkaId)
    {
        var list = new List<VydejkaRadek>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT vr.Id, vr.SkladovaKarta_Id, sk.Nazev,
                   vr.Mnozstvi_Evidencni, vr.Pocet_Baleni_Info,
                   vr.Nakupni_Cena_Bez_DPH, sk.Evidencni_Jednotka, sk.Sazba_DPH
              FROM VydejkaRadek vr
              JOIN SkladovaKarta sk ON sk.Id = vr.SkladovaKarta_Id
             WHERE vr.Vydejka_Id = $vid
             ORDER BY vr.Id";
        cmd.Parameters.AddWithValue("$vid", vydejkaId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new VydejkaRadek
            {
                Id                = reader.GetInt32(0),
                SkladovaKartaId   = reader.GetInt32(1),
                NazevZbozi        = reader.GetString(2),
                MnozstviEvidencni = (decimal)reader.GetDouble(3),
                PocetBaleniInfo   = reader.IsDBNull(4) ? null : (decimal)reader.GetDouble(4),
                NakupniCenaBezDPH = reader.IsDBNull(5) ? null : (decimal)reader.GetDouble(5),
                EvidencniJednotka = reader.GetString(6),
                SazbaDPH          = reader.IsDBNull(7) ? 21m : (decimal)reader.GetDouble(7)
            });
        }
        return list;
    }

    // ----------------------------------------------------------------
    // Statistika – počet pohybů za posledních 7 dní (pro dashboard)
    // ----------------------------------------------------------------
    public static int SpocitatPohybyZaTyden()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PohybSkladu WHERE Datum >= datetime('now', '-7 days')";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    // ================================================================
    // SKLADY
    // ================================================================
    public static List<Sklad> LoadSklady(bool jenAktivni = true)
    {
        var list = new List<Sklad>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = jenAktivni
            ? "SELECT Id, Nazev, Je_Vychozi, Je_Aktivni, Poradi FROM Sklad WHERE Je_Aktivni = 1 ORDER BY Poradi, Nazev"
            : "SELECT Id, Nazev, Je_Vychozi, Je_Aktivni, Poradi FROM Sklad ORDER BY Poradi, Nazev";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Sklad
            {
                Id        = reader.GetInt32(0),
                Nazev     = reader.GetString(1),
                JeVychozi = reader.GetInt32(2) == 1,
                JeAktivni = reader.GetInt32(3) == 1,
                Poradi    = reader.GetInt32(4)
            });
        }
        return list;
    }

    public static Sklad? LoadVychoziSklad()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nazev, Je_Vychozi, Je_Aktivni, Poradi FROM Sklad WHERE Je_Vychozi = 1 LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Sklad
            {
                Id        = reader.GetInt32(0),
                Nazev     = reader.GetString(1),
                JeVychozi = true,
                JeAktivni = reader.GetInt32(3) == 1,
                Poradi    = reader.GetInt32(4)
            };
        }
        return null;
    }

    public static int SaveSklad(Sklad s)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        // Pokud je označen jako výchozí, odznačit ostatní
        if (s.JeVychozi)
        {
            using var clr = conn.CreateCommand();
            clr.CommandText = "UPDATE Sklad SET Je_Vychozi = 0 WHERE Id != $id";
            clr.Parameters.AddWithValue("$id", s.Id);
            clr.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        if (s.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Sklad (Nazev, Je_Vychozi, Je_Aktivni, Poradi)
                VALUES ($nazev, $vych, $akt, $por);
                SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Sklad SET Nazev=$nazev, Je_Vychozi=$vych, Je_Aktivni=$akt, Poradi=$por
                 WHERE Id=$id;
                SELECT $id;";
            cmd.Parameters.AddWithValue("$id", s.Id);
        }
        cmd.Parameters.AddWithValue("$nazev", s.Nazev);
        cmd.Parameters.AddWithValue("$vych",  s.JeVychozi ? 1 : 0);
        cmd.Parameters.AddWithValue("$akt",   s.JeAktivni ? 1 : 0);
        cmd.Parameters.AddWithValue("$por",   s.Poradi);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static void DeactivateSklad(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Sklad SET Je_Aktivni = 0 WHERE Id = $id AND Je_Vychozi = 0";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ================================================================
    // SKLADOVÝ STAV (per-sklad)
    // ================================================================
    /// <summary>Stav karty v daném skladu. Pokud záznam neexistuje, vrací 0.</summary>
    public static decimal GetStav(int kartaId, int skladId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Stav_Evidencni FROM SkladovyStav WHERE SkladovaKarta_Id=$k AND Sklad_Id=$s";
        cmd.Parameters.AddWithValue("$k", kartaId);
        cmd.Parameters.AddWithValue("$s", skladId);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0m : (decimal)Convert.ToDouble(result);
    }

    /// <summary>Sečte stav karty přes všechny sklady.</summary>
    public static decimal GetStavCelkem(int kartaId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Stav_Evidencni),0) FROM SkladovyStav WHERE SkladovaKarta_Id=$k";
        cmd.Parameters.AddWithValue("$k", kartaId);
        return (decimal)Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);
    }

    /// <summary>Načte aktivní karty se stavem v daném skladu (0 pokud tam nejsou).</summary>
    public static List<SkladovaKarta> LoadAktivniKartyProSklad(int skladId)
    {
        var list = new List<SkladovaKarta>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT k.Id, k.Nazev, k.Kategorie, k.EAN, k.Evidencni_Jednotka, k.Typ_Baleni,
                   k.Koeficient_Prepoctu,
                   COALESCE(ss.Stav_Evidencni, 0) AS Stav,
                   k.Nakupni_Cena_Bez_DPH, k.Sazba_DPH, k.Prodejni_Cena_S_DPH,
                   k.Minimalni_Stav, k.Dodavatel, k.Je_Aktivni,
                   k.Datum_Posledni_Inventury, k.Datum_Posledniho_Naskladneni
              FROM SkladovaKarta k
              LEFT JOIN SkladovyStav ss ON ss.SkladovaKarta_Id = k.Id AND ss.Sklad_Id = $s
             WHERE k.Je_Aktivni = 1
             ORDER BY k.Nazev";
        cmd.Parameters.AddWithValue("$s", skladId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SkladovaKarta
            {
                Id                          = reader.GetInt32(0),
                Nazev                       = reader.GetString(1),
                Kategorie                   = reader.GetString(2),
                EAN                         = reader.IsDBNull(3) ? null : reader.GetString(3),
                EvidencniJednotka           = reader.GetString(4),
                TypBaleni                   = reader.GetString(5),
                KoeficientPrepoctu          = (decimal)reader.GetDouble(6),
                AktualniStavEvidencni       = (decimal)reader.GetDouble(7),
                NakupniCenaBezDPH           = (decimal)reader.GetDouble(8),
                SazbaDPH                    = (decimal)reader.GetDouble(9),
                ProdejniCenaSDPH            = (decimal)reader.GetDouble(10),
                MinimalniStav               = (decimal)reader.GetDouble(11),
                Dodavatel                   = reader.IsDBNull(12) ? null : reader.GetString(12),
                JeAktivni                   = reader.GetInt32(13) == 1,
                DatumPosledniInventury      = reader.IsDBNull(14) ? null : DateTime.Parse(reader.GetString(14)),
                DatumPoslednihoNaskladneni  = reader.IsDBNull(15) ? null : DateTime.Parse(reader.GetString(15))
            });
        }
        return list;
    }

    /// <summary>
    /// Aplikuje pohyb na stav: upraví SkladovyStav (upsert), zapíše PohybSkladu,
    /// a aktualizuje SkladovaKarta.Aktualni_Stav_Evidencni na součet přes sklady.
    /// Musí být v rámci již otevřené transakce.
    /// </summary>
    internal static void AplikujPohybInternal(
        SqliteConnection conn, SqliteTransaction tx,
        int kartaId, int skladId, decimal zmena,
        string typPohybu, string? dokladTyp, int? dokladId)
    {
        // Upsert SkladovyStav
        using (var up = conn.CreateCommand())
        {
            up.Transaction = tx;
            up.CommandText = @"
                INSERT INTO SkladovyStav (SkladovaKarta_Id, Sklad_Id, Stav_Evidencni)
                VALUES ($k, $s, $z)
                ON CONFLICT(SkladovaKarta_Id, Sklad_Id)
                DO UPDATE SET Stav_Evidencni = Stav_Evidencni + $z";
            up.Parameters.AddWithValue("$k", kartaId);
            up.Parameters.AddWithValue("$s", skladId);
            up.Parameters.AddWithValue("$z", (double)zmena);
            up.ExecuteNonQuery();
        }

        // Nový stav v tomto skladu
        decimal novyStav;
        using (var r = conn.CreateCommand())
        {
            r.Transaction = tx;
            r.CommandText = "SELECT Stav_Evidencni FROM SkladovyStav WHERE SkladovaKarta_Id=$k AND Sklad_Id=$s";
            r.Parameters.AddWithValue("$k", kartaId);
            r.Parameters.AddWithValue("$s", skladId);
            novyStav = (decimal)Convert.ToDouble(r.ExecuteScalar() ?? 0.0);
        }

        // Zápis do PohybSkladu
        using (var p = conn.CreateCommand())
        {
            p.Transaction = tx;
            p.CommandText = @"
                INSERT INTO PohybSkladu
                    (SkladovaKarta_Id, Sklad_Id, Typ_Pohybu, Mnozstvi_Evidencni,
                     Stav_Po_Pohybu, Doklad_Typ, Doklad_Id)
                VALUES
                    ($k, $s, $typ, $zm, $stav, $dt, $did)";
            p.Parameters.AddWithValue("$k",    kartaId);
            p.Parameters.AddWithValue("$s",    skladId);
            p.Parameters.AddWithValue("$typ",  typPohybu);
            p.Parameters.AddWithValue("$zm",   (double)zmena);
            p.Parameters.AddWithValue("$stav", (double)novyStav);
            p.Parameters.AddWithValue("$dt",   (object?)dokladTyp ?? DBNull.Value);
            p.Parameters.AddWithValue("$did",  (object?)dokladId ?? DBNull.Value);
            p.ExecuteNonQuery();
        }

        // Souhrn přes sklady zpět do SkladovaKarta (pro kompatibilitu s existujícím kódem)
        using (var s = conn.CreateCommand())
        {
            s.Transaction = tx;
            s.CommandText = @"
                UPDATE SkladovaKarta
                   SET Aktualni_Stav_Evidencni =
                       (SELECT COALESCE(SUM(Stav_Evidencni),0)
                          FROM SkladovyStav WHERE SkladovaKarta_Id = $k)
                 WHERE Id = $k";
            s.Parameters.AddWithValue("$k", kartaId);
            s.ExecuteNonQuery();
        }
    }

    // ================================================================
    // PŘEVODKY
    // ================================================================
    public static int SavePrevodka(Prevodka p)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Hlavička
        int prevodkaId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Prevodka
                    (Cislo_Dokladu, Datum_Prevodu, Sklad_Zdroj_Id, Sklad_Cil_Id, Poznamka)
                VALUES ($c, $d, $sz, $sc, $pz);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$c",  p.CisloDokladu);
            cmd.Parameters.AddWithValue("$d",  p.DatumPrevodu.ToString("o"));
            cmd.Parameters.AddWithValue("$sz", p.SkladZdrojId);
            cmd.Parameters.AddWithValue("$sc", p.SkladCilId);
            cmd.Parameters.AddWithValue("$pz", (object?)p.Poznamka ?? DBNull.Value);
            prevodkaId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        // Řádky + dva pohyby na každý řádek (výdej ze zdroje + příjem v cíli)
        foreach (var r in p.Radky)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO PrevodkaRadek
                        (Prevodka_Id, SkladovaKarta_Id, Mnozstvi_Evidencni)
                    VALUES ($p, $k, $m)";
                cmd.Parameters.AddWithValue("$p", prevodkaId);
                cmd.Parameters.AddWithValue("$k", r.SkladovaKartaId);
                cmd.Parameters.AddWithValue("$m", (double)r.MnozstviEvidencni);
                cmd.ExecuteNonQuery();
            }

            // Výdej ze zdrojového skladu (záporná hodnota)
            AplikujPohybInternal(conn, tx, r.SkladovaKartaId, p.SkladZdrojId,
                -r.MnozstviEvidencni, "Prevod", "Prevodka", prevodkaId);

            // Příjem v cílovém skladu (kladná hodnota)
            AplikujPohybInternal(conn, tx, r.SkladovaKartaId, p.SkladCilId,
                r.MnozstviEvidencni, "Prevod", "Prevodka", prevodkaId);
        }

        tx.Commit();
        return prevodkaId;
    }

    public static List<Prevodka> LoadPrevodky()
    {
        var list = new List<Prevodka>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Cislo_Dokladu, p.Datum_Prevodu,
                   p.Sklad_Zdroj_Id, sz.Nazev,
                   p.Sklad_Cil_Id,   sc.Nazev,
                   p.Poznamka, p.Vytvoreno
              FROM Prevodka p
              JOIN Sklad sz ON sz.Id = p.Sklad_Zdroj_Id
              JOIN Sklad sc ON sc.Id = p.Sklad_Cil_Id
             ORDER BY p.Datum_Prevodu DESC, p.Id DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Prevodka
            {
                Id              = reader.GetInt32(0),
                CisloDokladu    = reader.GetString(1),
                DatumPrevodu    = DateTime.Parse(reader.GetString(2)),
                SkladZdrojId    = reader.GetInt32(3),
                SkladZdrojNazev = reader.GetString(4),
                SkladCilId      = reader.GetInt32(5),
                SkladCilNazev   = reader.GetString(6),
                Poznamka        = reader.IsDBNull(7) ? null : reader.GetString(7),
                Vytvoreno       = DateTime.Parse(reader.GetString(8))
            });
        }
        return list;
    }

    public static List<PrevodkaRadek> LoadPrevodkaRadky(int prevodkaId)
    {
        var list = new List<PrevodkaRadek>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT pr.Id, pr.Prevodka_Id, pr.SkladovaKarta_Id, pr.Mnozstvi_Evidencni,
                   sk.Nazev, sk.Evidencni_Jednotka
              FROM PrevodkaRadek pr
              JOIN SkladovaKarta sk ON sk.Id = pr.SkladovaKarta_Id
             WHERE pr.Prevodka_Id = $pid
             ORDER BY pr.Id";
        cmd.Parameters.AddWithValue("$pid", prevodkaId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PrevodkaRadek
            {
                Id                = reader.GetInt32(0),
                PrevodkaId        = reader.GetInt32(1),
                SkladovaKartaId   = reader.GetInt32(2),
                MnozstviEvidencni = (decimal)reader.GetDouble(3),
                NazevKarty        = reader.GetString(4),
                EvidencniJednotka = reader.GetString(5)
            });
        }
        return list;
    }

    /// <summary>Vygeneruje další číslo převodky ve formátu PR-YYYY-0001.</summary>
    public static string GenerujCisloPrevodky()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM Prevodka
             WHERE Cislo_Dokladu LIKE $p";
        var prefix = $"PR-{DateTime.Now.Year}-";
        cmd.Parameters.AddWithValue("$p", prefix + "%");
        var n = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
        return $"{prefix}{n:D4}";
    }
}
