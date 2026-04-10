using System;
using System.Collections.Generic;
using System.IO;
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

        // 1) hlavička
        var cmdHead = conn.CreateCommand();
        cmdHead.Transaction = tx;
        cmdHead.CommandText = @"
            INSERT INTO Prijemka (Cislo_Dokladu, Datum_Prijeti, Dodavatel, Cislo_Faktury, Poznamka, Celkem_Bez_DPH, Celkem_S_DPH)
            VALUES ($cislo, $datum, $dod, $fak, $pozn, $cbez, $csdph);
            SELECT last_insert_rowid();";

        cmdHead.Parameters.AddWithValue("$cislo", p.CisloDokladu);
        cmdHead.Parameters.AddWithValue("$datum", p.DatumPrijeti.ToString("o"));
        cmdHead.Parameters.AddWithValue("$dod",   (object?)p.Dodavatel ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$fak",   (object?)p.CisloFaktury ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$pozn",  (object?)p.Poznamka ?? DBNull.Value);
        cmdHead.Parameters.AddWithValue("$cbez",  (double)p.CelkemBezDPH);
        cmdHead.Parameters.AddWithValue("$csdph", (double)p.CelkemSDPH);

        var prijemkaId = (long)(cmdHead.ExecuteScalar() ?? 0L);

        // 2) řádky + aktualizace skladu + auditní pohyb
        foreach (var r in p.Radky)
        {
            var mnozstviEv = (double)(r.PocetBaleni * r.KoeficientPrepoctu);

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
            cmdRadek.Parameters.AddWithValue("$me",   mnozstviEv);
            cmdRadek.Parameters.AddWithValue("$cena", (double)r.NakupniCenaBezDPH);
            cmdRadek.Parameters.AddWithValue("$dph",  (double)r.SazbaDPH);
            cmdRadek.Parameters.AddWithValue("$cbez", (double)r.CelkemBezDPH);
            cmdRadek.Parameters.AddWithValue("$csdph",(double)r.CelkemSDPH);
            cmdRadek.ExecuteNonQuery();

            // aktualizace skladu + datum posledního naskladnění + cena
            var cmdUpd = conn.CreateCommand();
            cmdUpd.Transaction = tx;
            cmdUpd.CommandText = @"
                UPDATE SkladovaKarta
                   SET Aktualni_Stav_Evidencni = Aktualni_Stav_Evidencni + $me,
                       Nakupni_Cena_Bez_DPH = $cena,
                       Datum_Posledniho_Naskladneni = $dat
                 WHERE Id = $kid;";
            cmdUpd.Parameters.AddWithValue("$me",   mnozstviEv);
            cmdUpd.Parameters.AddWithValue("$cena", (double)r.NakupniCenaBezDPH);
            cmdUpd.Parameters.AddWithValue("$dat",  p.DatumPrijeti.ToString("o"));
            cmdUpd.Parameters.AddWithValue("$kid",  r.SkladovaKartaId);
            cmdUpd.ExecuteNonQuery();

            // pohyb
            var cmdPohyb = conn.CreateCommand();
            cmdPohyb.Transaction = tx;
            cmdPohyb.CommandText = @"
                INSERT INTO PohybSkladu (SkladovaKarta_Id, Typ_Pohybu, Mnozstvi_Evidencni, Stav_Po_Pohybu, Doklad_Typ, Doklad_Id)
                VALUES ($kid, 'Prijem', $me,
                        (SELECT Aktualni_Stav_Evidencni FROM SkladovaKarta WHERE Id = $kid),
                        'Prijemka', $pid);";
            cmdPohyb.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
            cmdPohyb.Parameters.AddWithValue("$me",  mnozstviEv);
            cmdPohyb.Parameters.AddWithValue("$pid", prijemkaId);
            cmdPohyb.ExecuteNonQuery();
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

        // Ověření stavu před zápisem
        foreach (var r in v.Radky)
        {
            using var check = conn.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT Aktualni_Stav_Evidencni, Nazev FROM SkladovaKarta WHERE Id = $id";
            check.Parameters.AddWithValue("$id", r.SkladovaKartaId);
            using var rd = check.ExecuteReader();
            if (!rd.Read())
                throw new InvalidOperationException($"Karta Id={r.SkladovaKartaId} neexistuje.");
            var stav = (decimal)rd.GetDouble(0);
            var nazev = rd.GetString(1);
            if (stav < r.MnozstviEvidencni)
                throw new InvalidOperationException(
                    $"Nedostatek zásob u '{nazev}': k dispozici {stav:N2}, požadováno {r.MnozstviEvidencni:N2}.");
        }

        var head = conn.CreateCommand();
        head.Transaction = tx;
        head.CommandText = @"
            INSERT INTO Vydejka (Cislo_Dokladu, Datum_Vydeje, Stredisko, Typ_Vydeje, Poznamka)
            VALUES ($cislo, $datum, $stred, $typ, $pozn);
            SELECT last_insert_rowid();";
        head.Parameters.AddWithValue("$cislo", v.CisloDokladu);
        head.Parameters.AddWithValue("$datum", v.DatumVydeje.ToString("o"));
        head.Parameters.AddWithValue("$stred", v.Stredisko.ToString());
        head.Parameters.AddWithValue("$typ",   v.TypVydeje.ToString());
        head.Parameters.AddWithValue("$pozn",  (object?)v.Poznamka ?? DBNull.Value);
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

            var cmdU = conn.CreateCommand();
            cmdU.Transaction = tx;
            cmdU.CommandText = @"
                UPDATE SkladovaKarta
                   SET Aktualni_Stav_Evidencni = Aktualni_Stav_Evidencni - $me
                 WHERE Id = $kid;";
            cmdU.Parameters.AddWithValue("$me", (double)r.MnozstviEvidencni);
            cmdU.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
            cmdU.ExecuteNonQuery();

            var cmdP = conn.CreateCommand();
            cmdP.Transaction = tx;
            cmdP.CommandText = @"
                INSERT INTO PohybSkladu (SkladovaKarta_Id, Typ_Pohybu, Mnozstvi_Evidencni, Stav_Po_Pohybu, Doklad_Typ, Doklad_Id)
                VALUES ($kid, 'Vydej', -$me,
                        (SELECT Aktualni_Stav_Evidencni FROM SkladovaKarta WHERE Id = $kid),
                        'Vydejka', $vid);";
            cmdP.Parameters.AddWithValue("$kid", r.SkladovaKartaId);
            cmdP.Parameters.AddWithValue("$me",  (double)r.MnozstviEvidencni);
            cmdP.Parameters.AddWithValue("$vid", vydejkaId);
            cmdP.ExecuteNonQuery();
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
    public static void SaveUzaverka(DateTime datum, List<SkladovaKarta> karty)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var cmdH = conn.CreateCommand();
        cmdH.Transaction = tx;
        cmdH.CommandText = @"
            INSERT INTO Uzaverka (Typ, Datum_Od, Datum_Do, Vytvoreno)
            VALUES ('Manualni', $datum, $datum, $now);
            SELECT last_insert_rowid();";
        cmdH.Parameters.AddWithValue("$datum", datum.ToString("o"));
        cmdH.Parameters.AddWithValue("$now",   DateTime.Now.ToString("o"));
        var uzId = cmdH.ExecuteScalar()!;

        using var cmdR = conn.CreateCommand();
        cmdR.Transaction = tx;
        cmdR.CommandText = @"
            INSERT INTO UzaverkaRadek (Uzaverka_Id, SkladovaKarta_Id, Stav_Evidencni)
            VALUES ($uid, $kid, $stav)";
        cmdR.Parameters.AddWithValue("$uid",  0);
        cmdR.Parameters.AddWithValue("$kid",  0);
        cmdR.Parameters.AddWithValue("$stav", 0.0);

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
    public static List<Prijemka> LoadPrijemky(int limit = 500)
    {
        var list = new List<Prijemka>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Cislo_Dokladu, Datum_Prijeti, Dodavatel, Cislo_Faktury,
                   Poznamka, Celkem_Bez_DPH, Celkem_S_DPH
              FROM Prijemka
             ORDER BY Datum_Prijeti DESC, Id DESC
             LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);

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
                CelkemSDPH    = (decimal)reader.GetDouble(7)
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
    public static List<Vydejka> LoadVydejky(int limit = 500)
    {
        var list = new List<Vydejka>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Cislo_Dokladu, Datum_Vydeje, Stredisko, Typ_Vydeje, Poznamka
              FROM Vydejka
             ORDER BY Datum_Vydeje DESC, Id DESC
             LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);

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
                Poznamka     = reader.IsDBNull(5) ? null : reader.GetString(5)
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
                   vr.Nakupni_Cena_Bez_DPH, sk.Evidencni_Jednotka
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
                EvidencniJednotka = reader.GetString(6)
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
}
