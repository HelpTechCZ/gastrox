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

    public decimal CelkemNakupBezDph => Karty.Sum(k => k.HodnotaNakupBezDPH);
    public decimal CelkemProdejSDph  => Karty.Sum(k => k.HodnotaProdejSDPH);
    public int PocetKaret => Karty.Count;

    public ICommand GenerovatPdfCommand { get; }

    public event Action? Hotovo;

    public UzaverkaViewModel()
    {
        foreach (var k in DatabaseService.LoadAktivniKarty())
            Karty.Add(k);

        GenerovatPdfCommand = new RelayCommand(GenerovatPdf);
    }

    private void GenerovatPdf()
    {
        var datum = DateTime.Now;

        // Uložit uzávěrku do DB
        try
        {
            DatabaseService.SaveUzaverka(datum, Karty.ToList());
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
                        col.Item().PaddingBottom(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().Table(table =>
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
                                .AlignRight().Text($"{k.NakupniCenaZaJednotkuBezDPH:N2} Kč");
                            table.Cell().Background(bg).Padding(5)
                                .AlignRight().Text($"{k.HodnotaNakupBezDPH:N2} Kč");
                            table.Cell().Background(bg).Padding(5)
                                .AlignRight().Text($"{k.ProdejniCenaZaJednotkuSDPH:N2} Kč");
                            table.Cell().Background(bg).Padding(5)
                                .AlignRight().Text($"{k.HodnotaProdejSDPH:N2} Kč");
                        }
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
                                t.Span($"{karty.Sum(k => k.HodnotaNakupBezDPH):N2} Kč")
                                    .FontSize(10).Bold();
                            });
                        });
                        col.Item().PaddingTop(4).AlignRight().Text(t =>
                        {
                            t.Span("Prodej celkem: ").FontSize(10);
                            t.Span($"{karty.Sum(k => k.HodnotaProdejSDPH):N2} Kč")
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
