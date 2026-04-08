using System;
using System.Collections.Generic;
using System.IO;
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
    /// </summary>
    public static void Initialize()
    {
        var sqlPath = Path.Combine(AppContext.BaseDirectory, "Database", "init.sql");
        if (!File.Exists(sqlPath))
            throw new FileNotFoundException("Chybí init.sql", sqlPath);

        var script = File.ReadAllText(sqlPath);

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = script;
        cmd.ExecuteNonQuery();
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
}
