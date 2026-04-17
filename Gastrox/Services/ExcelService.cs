using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ClosedXML.Excel;
using Gastrox.Models;

namespace Gastrox.Services;

public static class ExcelService
{
    private static readonly string ExportDir = Path.Combine(AppContext.BaseDirectory, "Exporty");

    private static string OtevrAVrat(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* nevadí */ }
        return path;
    }

    // ----------------------------------------------------------------
    // Marže za období
    // ----------------------------------------------------------------
    public static string ExportMarze(IEnumerable<MarzeRadek> radky, DateTime od, DateTime doo)
    {
        Directory.CreateDirectory(ExportDir);
        var path = Path.Combine(ExportDir, $"Marze_{od:yyyy-MM-dd}_{doo:yyyy-MM-dd}.xlsx");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Marže");

        ws.Cell(1, 1).Value = "Marže za období";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"{od:d.M.yyyy} – {doo:d.M.yyyy}";

        var row = 4;
        var headers = new[] { "Zboží", "Kategorie", "Množství", "EJ", "Nákup bez DPH", "Nákup s DPH", "Prodej bez DPH", "Prodej s DPH", "Marže Kč", "Marže %" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(row, i + 1).Value = headers[i];
            ws.Cell(row, i + 1).Style.Font.Bold = true;
            ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
        }

        row++;
        foreach (var r in radky)
        {
            ws.Cell(row, 1).Value = r.Nazev;
            ws.Cell(row, 2).Value = r.Kategorie;
            ws.Cell(row, 3).Value = (double)r.Mnozstvi;
            ws.Cell(row, 4).Value = r.EvidencniJednotka;
            ws.Cell(row, 5).Value = (double)r.NakupBezDPH;
            ws.Cell(row, 6).Value = (double)r.NakupSDPH;
            ws.Cell(row, 7).Value = (double)r.ProdejBezDPH;
            ws.Cell(row, 8).Value = (double)r.ProdejSDPH;
            ws.Cell(row, 9).Value = (double)r.MarzeKc;
            ws.Cell(row, 10).Value = (double)r.MarzeProcent;
            row++;
        }

        // Formát čísla
        var dataRange = ws.Range(5, 3, row - 1, 10);
        dataRange.Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
        return OtevrAVrat(path);
    }

    // ----------------------------------------------------------------
    // Skladové karty
    // ----------------------------------------------------------------
    public static string ExportSkladoveKarty(IEnumerable<SkladovaKarta> karty)
    {
        Directory.CreateDirectory(ExportDir);
        var path = Path.Combine(ExportDir, $"Sklad_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sklad");

        var headers = new[] { "Název", "Kategorie", "EAN", "EJ", "Stav", "Min. stav",
            "Nákup bez DPH", "Prodej s DPH", "DPH %", "Marže %", "Dodavatel", "Expirace" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
        }

        int row = 2;
        foreach (var k in karty)
        {
            ws.Cell(row, 1).Value = k.Nazev;
            ws.Cell(row, 2).Value = k.Kategorie;
            ws.Cell(row, 3).Value = k.EAN ?? "";
            ws.Cell(row, 4).Value = k.EvidencniJednotka;
            ws.Cell(row, 5).Value = (double)k.AktualniStavEvidencni;
            ws.Cell(row, 6).Value = (double)k.MinimalniStav;
            ws.Cell(row, 7).Value = (double)k.NakupniCenaBezDPH;
            ws.Cell(row, 8).Value = (double)k.ProdejniCenaSDPH;
            ws.Cell(row, 9).Value = (double)k.SazbaDPH;
            ws.Cell(row, 10).Value = (double)k.MarzeProcent;
            ws.Cell(row, 11).Value = k.Dodavatel ?? "";
            ws.Cell(row, 12).Value = k.DatumExpirace?.ToString("d.M.yyyy") ?? "";
            row++;
        }

        ws.Range(2, 5, row - 1, 10).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
        return OtevrAVrat(path);
    }

    // ----------------------------------------------------------------
    // Pohyby – příjemky
    // ----------------------------------------------------------------
    public static string ExportPrijemky(IEnumerable<Prijemka> prijemky)
    {
        Directory.CreateDirectory(ExportDir);
        var path = Path.Combine(ExportDir, $"Prijemky_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Příjemky");

        var headers = new[] { "Číslo dokladu", "Datum", "Dodavatel", "Číslo faktury", "Sklad", "Bez DPH", "S DPH" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
        }

        int row = 2;
        foreach (var p in prijemky)
        {
            ws.Cell(row, 1).Value = p.CisloDokladu;
            ws.Cell(row, 2).Value = p.DatumPrijeti.ToString("d.M.yyyy");
            ws.Cell(row, 3).Value = p.Dodavatel ?? "";
            ws.Cell(row, 4).Value = p.CisloFaktury ?? "";
            ws.Cell(row, 5).Value = p.SkladNazev;
            ws.Cell(row, 6).Value = (double)p.CelkemBezDPH;
            ws.Cell(row, 7).Value = (double)p.CelkemSDPH;
            row++;
        }

        ws.Range(2, 6, row - 1, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
        return OtevrAVrat(path);
    }

    // ----------------------------------------------------------------
    // Pohyby – výdejky
    // ----------------------------------------------------------------
    public static string ExportVydejky(IEnumerable<Vydejka> vydejky)
    {
        Directory.CreateDirectory(ExportDir);
        var path = Path.Combine(ExportDir, $"Vydejky_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Výdejky");

        var headers = new[] { "Číslo dokladu", "Datum", "Středisko", "Typ výdeje", "Sklad", "Poznámka" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
        }

        int row = 2;
        foreach (var v in vydejky)
        {
            ws.Cell(row, 1).Value = v.CisloDokladu;
            ws.Cell(row, 2).Value = v.DatumVydeje.ToString("d.M.yyyy");
            ws.Cell(row, 3).Value = v.StrediskoLabel;
            ws.Cell(row, 4).Value = v.TypVydejeLabel;
            ws.Cell(row, 5).Value = v.SkladNazev;
            ws.Cell(row, 6).Value = v.Poznamka ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
        return OtevrAVrat(path);
    }
}
