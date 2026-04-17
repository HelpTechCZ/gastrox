using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gastrox.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gastrox.Services;

/// <summary>
/// Centrální generátor PDF dokladů (příjemka, výdejka, převodka).
/// Soubory se ukládají do ./Doklady/ vedle .exe a otevírají se v systémovém prohlížeči.
/// </summary>
public static class PdfService
{
    private static readonly string DokladyDir =
        Path.Combine(AppContext.BaseDirectory, "Doklady");

    // ----------------------------------------------------------------
    // PŘÍJEMKA
    // ----------------------------------------------------------------
    public static string GenerujPrijemkuPdf(Prijemka p, IReadOnlyList<PrijemkaRadek> radky)
    {
        Directory.CreateDirectory(DokladyDir);
        var fileName = $"Prijemka-{SanitizeFileName(p.CisloDokladu)}.pdf";
        var path = Path.Combine(DokladyDir, fileName);
        var firma = LoadFirma();
        var licensed = LicenseService.IsLicensed;
        string cena(decimal v) => licensed ? $"{v:N2} Kč" : "DEMO";

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("PŘÍJEMKA").FontSize(18).Bold();
                            c.Item().Text($"Číslo dokladu: {p.CisloDokladu}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Datum přijetí: {p.DatumPrijeti:d.M.yyyy}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(p.SkladNazev))
                                c.Item().Text($"Sklad: {p.SkladNazev}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(220).Column(c => PsatFirmu(c, firma));
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Dodavatel").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(string.IsNullOrWhiteSpace(p.Dodavatel) ? "—" : p.Dodavatel!)
                                .FontSize(9).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Číslo faktury").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(string.IsNullOrWhiteSpace(p.CisloFaktury) ? "—" : p.CisloFaktury!)
                                .FontSize(9).Bold();
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(1f);
                            cols.RelativeColumn(1f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(1.4f);
                        });

                        var hdrStyle = TextStyle.Default.Bold().FontSize(8);
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Zboží").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Balení").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Počet").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("× Koef.").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("= EJ").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Cena/bal.").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("DPH %").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Bez DPH").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("S DPH").Style(hdrStyle);
                        });

                        int idx = 0;
                        foreach (var r in radky)
                        {
                            var bg = idx++ % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(5).Text(r.NazevZbozi);
                            table.Cell().Background(bg).Padding(5).Text(r.TypBaleni);
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.PocetBaleni:N2}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.KoeficientPrepoctu:N2}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.MnozstviEvidencni:N2} {r.EvidencniJednotka}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.NakupniCenaBezDPH));
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.SazbaDPH:N0}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.CelkemBezDPH));
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.CelkemSDPH));
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(p.Poznamka))
                    {
                        col.Item().PaddingTop(12).Text("Poznámka").FontSize(7).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(p.Poznamka!).FontSize(8);
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(8).Text($"Položek: {radky.Count}").FontSize(8).Bold();

                    // DPH sumář po sazbách
                    var dphSkupiny = radky
                        .GroupBy(r => r.SazbaDPH)
                        .Select(g => new
                        {
                            Sazba = g.Key,
                            Zaklad = g.Sum(r => r.CelkemBezDPH),
                            Dan    = g.Sum(r => r.CelkemSDPH - r.CelkemBezDPH),
                            Celkem = g.Sum(r => r.CelkemSDPH)
                        })
                        .OrderBy(x => x.Sazba)
                        .ToList();

                    col.Item().PaddingTop(8).AlignRight().Width(320).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1.3f);
                            c.RelativeColumn(1.3f);
                            c.RelativeColumn(1.3f);
                        });
                        var h = TextStyle.Default.Bold().FontSize(8);
                        t.Header(hdr =>
                        {
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Sazba").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Základ").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("DPH").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Celkem").Style(h);
                        });
                        foreach (var g in dphSkupiny)
                        {
                            t.Cell().Padding(4).Text($"{g.Sazba:N0} %");
                            t.Cell().Padding(4).AlignRight().Text(cena(g.Zaklad));
                            t.Cell().Padding(4).AlignRight().Text(cena(g.Dan));
                            t.Cell().Padding(4).AlignRight().Text(cena(g.Celkem));
                        }
                        // Součet
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("Celkem").Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cena(dphSkupiny.Sum(x => x.Zaklad))).Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cena(dphSkupiny.Sum(x => x.Dan))).Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight()
                            .Text(cena(dphSkupiny.Sum(x => x.Celkem))).Bold().FontColor(Colors.Green.Darken2);
                    });

                    col.Item().PaddingTop(12).AlignCenter()
                        .Text($"Gastrox – vygenerováno {DateTime.Now:d.M.yyyy HH:mm}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(path);

        OtevrPdf(path);
        return path;
    }

    // ----------------------------------------------------------------
    // VÝDEJKA
    // ----------------------------------------------------------------
    public static string GenerujVydejkuPdf(Vydejka v, IReadOnlyList<VydejkaRadek> radky)
    {
        Directory.CreateDirectory(DokladyDir);
        var fileName = $"Vydejka-{SanitizeFileName(v.CisloDokladu)}.pdf";
        var path = Path.Combine(DokladyDir, fileName);
        var firma = LoadFirma();
        var licensed = LicenseService.IsLicensed;
        string cena(decimal? val) => !licensed ? "DEMO" : (val.HasValue ? $"{val.Value:N2} Kč" : "—");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VÝDEJKA").FontSize(18).Bold();
                            c.Item().Text($"Číslo dokladu: {v.CisloDokladu}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Datum výdeje: {v.DatumVydeje:d.M.yyyy}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(v.SkladNazev))
                                c.Item().Text($"Sklad: {v.SkladNazev}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(220).Column(c => PsatFirmu(c, firma));
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Středisko").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(v.StrediskoLabel).FontSize(9).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Typ výdeje").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(v.TypVydejeLabel).FontSize(9).Bold();
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                        });

                        var hdrStyle = TextStyle.Default.Bold().FontSize(7);
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Zboží").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Množství (EJ)").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Nákup bez DPH").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Nákup s DPH").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Prodej bez DPH").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Prodej s DPH").Style(hdrStyle);
                        });

                        int idx = 0;
                        foreach (var r in radky)
                        {
                            var bg = idx++ % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(5).Text(r.NazevZbozi);
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.MnozstviEvidencni:N2} {r.EvidencniJednotka}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.HodnotaBezDPH));
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.HodnotaSDPH));
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.HodnotaProdejBezDPH));
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(cena(r.HodnotaProdejSDPH));
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(v.Poznamka))
                    {
                        col.Item().PaddingTop(12).Text("Poznámka").FontSize(7).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(v.Poznamka!).FontSize(8);
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(8).Text($"Položek: {radky.Count}").FontSize(8).Bold();

                    // DPH sumář po sazbách – nákupní i prodejní
                    var dphSkupiny = radky
                        .GroupBy(r => r.SazbaDPH)
                        .Select(g => new
                        {
                            Sazba           = g.Key,
                            NakupZaklad     = g.Sum(r => r.HodnotaBezDPH),
                            NakupDan        = g.Sum(r => r.HodnotaSDPH - r.HodnotaBezDPH),
                            NakupCelkem     = g.Sum(r => r.HodnotaSDPH),
                            ProdejZaklad    = g.Sum(r => r.HodnotaProdejBezDPH),
                            ProdejDan       = g.Sum(r => r.HodnotaProdejSDPH - r.HodnotaProdejBezDPH),
                            ProdejCelkem    = g.Sum(r => r.HodnotaProdejSDPH)
                        })
                        .OrderBy(x => x.Sazba)
                        .ToList();

                    col.Item().PaddingTop(8).AlignRight().Width(420).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.8f);    // Sazba
                            c.RelativeColumn(1.2f);    // Nákup bez DPH
                            c.RelativeColumn(1.2f);    // Nákup s DPH
                            c.RelativeColumn(1.2f);    // Prodej bez DPH
                            c.RelativeColumn(1.2f);    // Prodej s DPH
                        });
                        var h = TextStyle.Default.Bold().FontSize(7);
                        t.Header(hdr =>
                        {
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Sazba").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Nákup bez DPH").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Nákup s DPH").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Prodej bez DPH").Style(h);
                            hdr.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Prodej s DPH").Style(h);
                        });
                        foreach (var g in dphSkupiny)
                        {
                            t.Cell().Padding(4).Text($"{g.Sazba:N0} %");
                            t.Cell().Padding(4).AlignRight().Text(cena(g.NakupZaklad));
                            t.Cell().Padding(4).AlignRight().Text(cena(g.NakupCelkem));
                            t.Cell().Padding(4).AlignRight().Text(cena(g.ProdejZaklad));
                            t.Cell().Padding(4).AlignRight().Text(cena(g.ProdejCelkem));
                        }
                        // Součet
                        var bold = TextStyle.Default.Bold().FontSize(8);
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("Celkem").Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight()
                            .Text(cena(dphSkupiny.Sum(x => x.NakupZaklad))).Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight()
                            .Text(cena(dphSkupiny.Sum(x => x.NakupCelkem))).Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight()
                            .Text(cena(dphSkupiny.Sum(x => x.ProdejZaklad))).Bold();
                        t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight()
                            .Text(cena(dphSkupiny.Sum(x => x.ProdejCelkem))).Bold().FontColor(Colors.Red.Darken2);
                    });

                    col.Item().PaddingTop(12).AlignCenter()
                        .Text($"Gastrox – vygenerováno {DateTime.Now:d.M.yyyy HH:mm}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(path);

        OtevrPdf(path);
        return path;
    }

    // ----------------------------------------------------------------
    // PŘEVODKA
    // ----------------------------------------------------------------
    public static string GenerujPrevodkuPdf(Prevodka p, IReadOnlyList<PrevodkaRadek> radky)
    {
        Directory.CreateDirectory(DokladyDir);
        var fileName = $"Prevodka-{SanitizeFileName(p.CisloDokladu)}.pdf";
        var path = Path.Combine(DokladyDir, fileName);
        var firma = LoadFirma();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("PŘEVODKA").FontSize(18).Bold();
                            c.Item().Text($"Číslo dokladu: {p.CisloDokladu}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Datum převodu: {p.DatumPrevodu:d.M.yyyy}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(220).Column(c => PsatFirmu(c, firma));
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Ze skladu").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(p.SkladZdrojNazev).FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                        });
                        row.ConstantItem(40).AlignCenter().AlignMiddle().Text("➜").FontSize(14);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Do skladu").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(p.SkladCilNazev).FontSize(10).Bold().FontColor(Colors.Green.Darken2);
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                        });

                        var hdrStyle = TextStyle.Default.Bold().FontSize(8);
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Zboží").Style(hdrStyle);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Množství (EJ)").Style(hdrStyle);
                        });

                        int idx = 0;
                        foreach (var r in radky)
                        {
                            var bg = idx++ % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(5).Text(r.NazevKarty);
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{r.MnozstviEvidencni:N2} {r.EvidencniJednotka}");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(p.Poznamka))
                    {
                        col.Item().PaddingTop(12).Text("Poznámka").FontSize(7).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(p.Poznamka!).FontSize(8);
                    }

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
                            c.Item().AlignCenter().Text("Vydal (zdrojový sklad)").FontSize(7);
                        });
                        row.ConstantItem(40);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
                            c.Item().AlignCenter().Text("Přijal (cílový sklad)").FontSize(7);
                        });
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(8).Text($"Položek: {radky.Count}").FontSize(8).Bold();
                    col.Item().PaddingTop(12).AlignCenter()
                        .Text($"Gastrox – vygenerováno {DateTime.Now:d.M.yyyy HH:mm}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(path);

        OtevrPdf(path);
        return path;
    }

    // ----------------------------------------------------------------
    // Pomocné
    // ----------------------------------------------------------------
    private static void PsatFirmu(ColumnDescriptor c, FirmaInfo firma)
    {
        if (!string.IsNullOrWhiteSpace(firma.Nazev))
            c.Item().AlignRight().Text(firma.Nazev!).FontSize(9).Bold();
        if (!string.IsNullOrWhiteSpace(firma.Ulice))
            c.Item().AlignRight().Text(firma.Ulice!).FontSize(8).FontColor(Colors.Grey.Darken1);
        var misto = string.Join(" ", new[] { firma.Psc, firma.Mesto }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(misto))
            c.Item().AlignRight().Text(misto).FontSize(8).FontColor(Colors.Grey.Darken1);
        if (!string.IsNullOrWhiteSpace(firma.Ico) || !string.IsNullOrWhiteSpace(firma.Dic))
        {
            var idLine = string.Join(" · ", new[]
            {
                string.IsNullOrWhiteSpace(firma.Ico) ? null : $"IČO: {firma.Ico}",
                string.IsNullOrWhiteSpace(firma.Dic) ? null : $"DIČ: {firma.Dic}"
            }.Where(s => s is not null));
            c.Item().AlignRight().Text(idLine!).FontSize(8).FontColor(Colors.Grey.Darken1);
        }
    }

    private static FirmaInfo LoadFirma()
    {
        var n = DatabaseService.LoadNastaveni();
        string? get(string k) => n.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        return new FirmaInfo
        {
            Nazev   = get(NastaveniKey.FirmaNazev),
            Ico     = get(NastaveniKey.FirmaIco),
            Dic     = get(NastaveniKey.FirmaDic),
            Ulice   = get(NastaveniKey.FirmaUlice),
            Mesto   = get(NastaveniKey.FirmaMesto),
            Psc     = get(NastaveniKey.FirmaPsc),
            Stat    = get(NastaveniKey.FirmaStat),
            Email   = get(NastaveniKey.FirmaEmail),
            Telefon = get(NastaveniKey.FirmaTel)
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }

    private static void OtevrPdf(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch { /* soubor zůstal, uživatel ho najde v ./Doklady/ */ }
    }
}
