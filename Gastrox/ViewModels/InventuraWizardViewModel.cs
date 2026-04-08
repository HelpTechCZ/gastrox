using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// Třístupňový průvodce inventurou:
/// 1) Hlavička (název, datum, poznámka)
/// 2) Fyzické stavy – ke každé kartě se zadá nasčítané množství
/// 3) Souhrn rozdílů + uzavřít/uložit jako rozpracovaná
/// </summary>
public class InventuraWizardViewModel : ViewModelBase
{
    public ObservableCollection<InventuraRadekViewModel> Radky { get; } = new();

    private int _krok = 1;
    public int Krok { get => _krok; set { if (SetProperty(ref _krok, value)) NotifyKrok(); } }
    public bool JeKrok1 => Krok == 1;
    public bool JeKrok2 => Krok == 2;
    public bool JeKrok3 => Krok == 3;

    private string _nazev = $"Inventura {DateTime.Now:d.M.yyyy}";
    public string Nazev { get => _nazev; set => SetProperty(ref _nazev, value); }

    private DateTime _datum = DateTime.Now;
    public DateTime Datum { get => _datum; set => SetProperty(ref _datum, value); }

    private string? _poznamka;
    public string? Poznamka { get => _poznamka; set => SetProperty(ref _poznamka, value); }

    public ICommand DalsiCommand { get; }
    public ICommand ZpetCommand { get; }
    public ICommand UlozitRozpracovanouCommand { get; }
    public ICommand UzavritCommand { get; }

    public event Action? Hotovo;

    public InventuraWizardViewModel()
    {
        // Načti všechny aktivní karty jako řádky inventury
        foreach (var k in DatabaseService.LoadAktivniKarty())
        {
            Radky.Add(new InventuraRadekViewModel
            {
                SkladovaKartaId   = k.Id,
                NazevZbozi        = k.Nazev,
                EvidencniJednotka = k.EvidencniJednotka,
                TeoretickyStav    = k.AktualniStavEvidencni,
                FyzickyStav       = k.AktualniStavEvidencni
            });
        }

        DalsiCommand = new RelayCommand(_ => Krok++, _ => Krok < 3 && !string.IsNullOrWhiteSpace(Nazev));
        ZpetCommand  = new RelayCommand(_ => Krok--, _ => Krok > 1);
        UlozitRozpracovanouCommand = new RelayCommand(_ => Uloz(false));
        UzavritCommand = new RelayCommand(_ => Uloz(true));
    }

    private void NotifyKrok()
    {
        OnPropertyChanged(nameof(JeKrok1));
        OnPropertyChanged(nameof(JeKrok2));
        OnPropertyChanged(nameof(JeKrok3));
        OnPropertyChanged(nameof(PocetSRozdilem));
        OnPropertyChanged(nameof(CelkovyRozdil));
        CommandManager.InvalidateRequerySuggested();
    }

    public int PocetSRozdilem => Radky.Count(r => r.Rozdil != 0);
    public decimal CelkovyRozdil => Radky.Sum(r => r.Rozdil);

    private void Uloz(bool uzavrit)
    {
        var inv = new Inventura
        {
            Nazev = Nazev,
            DatumInventury = Datum,
            Poznamka = Poznamka,
            Radky = Radky.Select(r => new InventuraRadek
            {
                SkladovaKartaId   = r.SkladovaKartaId,
                NazevZbozi        = r.NazevZbozi,
                EvidencniJednotka = r.EvidencniJednotka,
                TeoretickyStav    = r.TeoretickyStav,
                FyzickyStav       = r.FyzickyStav
            }).ToList()
        };

        try
        {
            DatabaseService.SaveInventura(inv, uzavrit);
            MessageBox.Show(uzavrit
                ? "Inventura uzavřena – stavy upraveny."
                : "Inventura uložena jako rozpracovaná.",
                "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            Hotovo?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při ukládání inventury:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public class InventuraRadekViewModel : ViewModelBase
{
    public int SkladovaKartaId { get; set; }
    public string NazevZbozi { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;
    public decimal TeoretickyStav { get; set; }

    private decimal _fyzickyStav;
    public decimal FyzickyStav
    {
        get => _fyzickyStav;
        set
        {
            if (SetProperty(ref _fyzickyStav, value))
                OnPropertyChanged(nameof(Rozdil));
        }
    }

    public decimal Rozdil => FyzickyStav - TeoretickyStav;
}
