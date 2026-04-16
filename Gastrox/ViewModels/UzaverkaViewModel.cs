using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gastrox.ViewModels;

public class UzaverkaViewModel : ViewModelBase
{
    public ObservableCollection<SkladovaKarta> Karty { get; } = new();
    public ObservableCollection<Sklad> Sklady { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad
    {
        get => _vybranySklad;
        set
        {
            if (SetProperty(ref _vybranySklad, value))
                NacistKarty();
        }
    }

    public decimal CelkemNakupBezDph => Karty.Sum(k => k.HodnotaNakupBezDPH);
    public decimal CelkemProdejSDph  => Karty.Sum(k => k.HodnotaProdejSDPH);
    public int PocetKaret => Karty.Count;

    public ICommand GenerovatPdfCommand { get; }

    public event Action? Hotovo;

    public UzaverkaViewModel()
    {
        foreach (var s in DatabaseService.LoadSklady())
            Sklady.Add(s);

        NacistKarty();

        GenerovatPdfCommand = new RelayCommand(GenerovatPdf);
    }

    private void NacistKarty()
    {
        Karty.Clear();
        var list = _vybranySklad is null
            ? DatabaseService.LoadAktivniKarty()
            : DatabaseService.LoadAktivniKartyProSklad(_vybranySklad.Id);
        foreach (var k in list)
            Karty.Add(k);

        OnPropertyChanged(nameof(CelkemNakupBezDph));
        OnPropertyChanged(nameof(CelkemProdejSDph));
        OnPropertyChanged(nameof(PocetKaret));
    }

    private void GenerovatPdf()
    {
        var datum = DateTime.Now;

        // Uložit uzávěrku do DB
        try
        {
            DatabaseService.SaveUzaverka(datum, Karty.ToList(), _vybranySklad?.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při ukládání uzávěrky:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Generovat PDF
        try
        {
            var fileName = $"Uzavěrka-skladu-{datum:yyyy-MM-dd-HHmm}.pdf";
            var dir = Path.Combine(AppContext.BaseDirectory, "Uzavěrky");
            Directory.CreateDirectory(dir);
            var pdfPath = Path.Combine(dir, fileName);

            var karty = Karty.ToList();
            var licensed = LicenseService.IsLicensed;
            string cenaText(decimal v) => licensed ? $"{v:N2} Kč" : "DEMO";

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Uzávěrka skladu")
                            .FontSize(22).Bold();
                        col.Item().Text($"Datum: {datum:d.M.yyyy  HH:mm}")
                            .FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(_vybranySklad is null
                                ? "Sklady: všechny (souhrnně)"
                                : $"Sklad: {_vybranySklad.Nazev}")
                            .FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingBottom(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().Column(content =>
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);   // Název
                                cols.RelativeColumn(1.5f); // Kategorie
                                cols.RelativeColumn(1.5f); // Stav
                                cols.RelativeColumn(1.5f); // Nákup/j. bez DPH
                                cols.RelativeColumn(1.5f); // Nákup celkem
                                cols.RelativeColumn(1.5f); // Prodej/j. s DPH
                                cols.RelativeColumn(1.5f); // Prodej celkem
                            });

                            // Hlavička
                            table.Header(header =>
                            {
                                var style = TextStyle.Default.Bold().FontSize(9);

                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .Text("Název").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .Text("Kategorie").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .AlignRight().Text("Stav").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .AlignRight().Text("Nákupní cena/j.").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .AlignRight().Text("Nákupní cena celk.").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .AlignRight().Text("Prodejní cena/j.").Style(style);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                    .AlignRight().Text("Prodejní cena celk.").Style(style);
                            });

                            // Řádky
                            foreach (var k in karty)
                            {
                                var bg = karty.IndexOf(k) % 2 == 1
                                    ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bg).Padding(5)
                                    .Text(k.Nazev);
                                table.Cell().Background(bg).Padding(5)
                                    .Text(k.Kategorie);
                                table.Cell().Background(bg).Padding(5)
                                    .AlignRight().Text(k.StavSJednotkou);
                                table.Cell().Background(bg).Padding(5)
                                    .AlignRight().Text(cenaText(k.NakupniCenaZaJednotkuBezDPH));
                                table.Cell().Background(bg).Padding(5)
                                    .AlignRight().Text(cenaText(k.HodnotaNakupBezDPH));
                                table.Cell().Background(bg).Padding(5)
                                    .AlignRight().Text(cenaText(k.ProdejniCenaZaJednotkuSDPH));
                                table.Cell().Background(bg).Padding(5)
                                    .AlignRight().Text(cenaText(k.HodnotaProdejSDPH));
                            }
                        });

                        // Rozpis DPH po sazbách (nákup i prodej)
                        var dphSkupiny = karty
                            .GroupBy(k => k.SazbaDPH)
                            .Select(g =>
                            {
                                var sazba          = g.Key;
                                var nakupZaklad    = g.Sum(k => k.HodnotaNakupBezDPH);
                                var nakupCelkem    = nakupZaklad * (1 + sazba / 100m);
                                var nakupDan       = nakupCelkem - nakupZaklad;
                                var prodejCelkem   = g.Sum(k => k.HodnotaProdejSDPH);
                                var prodejZaklad   = sazba > 0 ? prodejCelkem / (1 + sazba / 100m) : prodejCelkem;
                                var prodejDan      = prodejCelkem - prodejZaklad;
                                return new { sazba, nakupZaklad, nakupDan, nakupCelkem, prodejZaklad, prodejDan, prodejCelkem };
                            })
                            .OrderBy(x => x.sazba)
                            .ToList();

                        content.Item().PaddingTop(16).Text("Rozpis DPH").FontSize(12).Bold();
                        content.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(0.8f); // Sazba
                                c.RelativeColumn(1.4f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f); // Nákup základ/DPH/celkem
                                c.RelativeColumn(1.4f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f); // Prodej základ/DPH/celkem
                            });

                            var h = TextStyle.Default.Bold().FontSize(8);
                            var hdrBg = Colors.Grey.Lighten3;
                            var nakBg = Colors.Blue.Lighten5;
                            var proBg = Colors.Green.Lighten5;

                            t.Header(hdr =>
                            {
                                hdr.Cell().RowSpan(2).Background(hdrBg).Padding(4).AlignMiddle().Text("Sazba").Style(h);
                                hdr.Cell().ColumnSpan(3).Background(nakBg).Padding(4).AlignCenter().Text("Nákupní hodnota").Style(h);
                                hdr.Cell().ColumnSpan(3).Background(proBg).Padding(4).AlignCenter().Text("Prodejní hodnota").Style(h);

                                hdr.Cell().Background(nakBg).Padding(4).AlignRight().Text("Základ").Style(h);
                                hdr.Cell().Background(nakBg).Padding(4).AlignRight().Text("DPH").Style(h);
                                hdr.Cell().Background(nakBg).Padding(4).AlignRight().Text("Celkem").Style(h);
                                hdr.Cell().Background(proBg).Padding(4).AlignRight().Text("Základ").Style(h);
                                hdr.Cell().Background(proBg).Padding(4).AlignRight().Text("DPH").Style(h);
                                hdr.Cell().Background(proBg).Padding(4).AlignRight().Text("Celkem").Style(h);
                            });

                            foreach (var g in dphSkupiny)
                            {
                                t.Cell().Padding(4).Text($"{g.sazba:N0} %");
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.nakupZaklad));
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.nakupDan));
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.nakupCelkem));
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.prodejZaklad));
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.prodejDan));
                                t.Cell().Padding(4).AlignRight().Text(cenaText(g.prodejCelkem));
                            }

                            // Součtový řádek
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("Celkem").Bold();
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.nakupZaklad))).Bold();
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.nakupDan))).Bold();
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.nakupCelkem))).Bold().FontColor(Colors.Blue.Darken2);
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.prodejZaklad))).Bold();
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.prodejDan))).Bold();
                            t.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text(cenaText(dphSkupiny.Sum(x => x.prodejCelkem))).Bold().FontColor(Colors.Green.Darken2);
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Položek: {karty.Count}").FontSize(10).Bold();
                            row.RelativeItem().AlignRight().Text(t =>
                            {
                                t.Span("Nákup celkem: ").FontSize(10);
                                t.Span(licensed ? $"{karty.Sum(k => k.HodnotaNakupBezDPH):N2} Kč" : "DEMO")
                                    .FontSize(10).Bold();
                            });
                        });
                        col.Item().PaddingTop(4).AlignRight().Text(t =>
                        {
                            t.Span("Prodej celkem: ").FontSize(10);
                            t.Span(licensed ? $"{karty.Sum(k => k.HodnotaProdejSDPH):N2} Kč" : "DEMO")
                                .FontSize(10).Bold();
                        });
                        col.Item().PaddingTop(12).AlignCenter()
                            .Text($"Gastrox – vygenerováno {datum:d.M.yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf(pdfPath);

            // Otevřít PDF v systémovém prohlížeči
            Process.Start(new ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            });

            MessageBox.Show(
                $"Uzávěrka uložena a PDF vygenerováno.\n\n{pdfPath}",
                "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);

            Hotovo?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při generování PDF:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
