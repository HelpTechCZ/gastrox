using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

public class MarzeViewModel : ViewModelBase
{
    public ObservableCollection<MarzeRadek> Radky { get; } = new();

    private DateTime _od = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    public DateTime Od { get => _od; set { if (SetProperty(ref _od, value)) Nacist(); } }

    private DateTime _do = DateTime.Now;
    public DateTime Do { get => _do; set { if (SetProperty(ref _do, value)) Nacist(); } }

    public decimal CelkemNakupBezDPH  => Radky.Sum(r => r.NakupBezDPH);
    public decimal CelkemNakupSDPH    => Radky.Sum(r => r.NakupSDPH);
    public decimal CelkemProdejBezDPH => Radky.Sum(r => r.ProdejBezDPH);
    public decimal CelkemProdejSDPH   => Radky.Sum(r => r.ProdejSDPH);
    public decimal CelkemMarzeKc      => CelkemProdejBezDPH - CelkemNakupBezDPH;
    public decimal CelkemMarzeProcent => CelkemProdejBezDPH > 0
        ? decimal.Round(CelkemMarzeKc / CelkemProdejBezDPH * 100m, 1) : 0m;

    public ICommand ExportExcelCommand { get; }

    public MarzeViewModel()
    {
        ExportExcelCommand = new RelayCommand(_ => ExportExcel());
        Nacist();
    }

    private void Nacist()
    {
        Radky.Clear();
        foreach (var r in DatabaseService.LoadMarzeZaObdobi(Od, Do))
            Radky.Add(r);
        NotifyCelkem();
    }

    private void NotifyCelkem()
    {
        OnPropertyChanged(nameof(CelkemNakupBezDPH));
        OnPropertyChanged(nameof(CelkemNakupSDPH));
        OnPropertyChanged(nameof(CelkemProdejBezDPH));
        OnPropertyChanged(nameof(CelkemProdejSDPH));
        OnPropertyChanged(nameof(CelkemMarzeKc));
        OnPropertyChanged(nameof(CelkemMarzeProcent));
    }

    private void ExportExcel()
    {
        try
        {
            var path = ExcelService.ExportMarze(Radky, Od, Do);
            MessageBox.Show($"Export uložen:\n{path}", "Excel export",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při exportu:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
