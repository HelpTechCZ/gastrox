using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// Třístupňový průvodce výdejem zboží:
/// 1) Typ výdeje + středisko
/// 2) Položky (zboží + množství)
/// 3) Souhrn + uložení
/// </summary>
public class VydejkaWizardViewModel : ViewModelBase
{
    public ObservableCollection<SkladovaKarta> DostupneZbozi { get; }
    public ObservableCollection<VydejkaRadekViewModel> Radky { get; } = new();

    private int _krok = 1;
    public int Krok { get => _krok; set { if (SetProperty(ref _krok, value)) NotifyKrok(); } }

    public bool JeKrok1 => Krok == 1;
    public bool JeKrok2 => Krok == 2;
    public bool JeKrok3 => Krok == 3;

    // ---------- Krok 1: typ + středisko ----------
    public Array TypyVydeje { get; } = Enum.GetValues(typeof(TypVydeje));
    public Array Strediska  { get; } = Enum.GetValues(typeof(Stredisko));

    private TypVydeje _typVydeje = TypVydeje.Prodej;
    public TypVydeje TypVydeje { get => _typVydeje; set => SetProperty(ref _typVydeje, value); }

    private Stredisko _stredisko = Stredisko.Bar;
    public Stredisko Stredisko { get => _stredisko; set => SetProperty(ref _stredisko, value); }

    private string? _poznamka;
    public string? Poznamka { get => _poznamka; set => SetProperty(ref _poznamka, value); }

    // ---------- Commands ----------
    public ICommand DalsiCommand { get; }
    public ICommand ZpetCommand { get; }
    public ICommand PridatRadekCommand { get; }
    public ICommand OdebratRadekCommand { get; }
    public ICommand UlozitCommand { get; }

    public event Action? Hotovo;

    public VydejkaWizardViewModel()
    {
        DostupneZbozi = new ObservableCollection<SkladovaKarta>(DatabaseService.LoadAktivniKarty());
        Radky.CollectionChanged += (_, __) => OnPropertyChanged(nameof(MuzeUlozit));

        DalsiCommand = new RelayCommand(_ => Krok++, _ => MuzeDalsi());
        ZpetCommand  = new RelayCommand(_ => Krok--, _ => Krok > 1);
        PridatRadekCommand  = new RelayCommand(_ => PridejRadek());
        OdebratRadekCommand = new RelayCommand(p => { if (p is VydejkaRadekViewModel r) Radky.Remove(r); });
        UlozitCommand = new RelayCommand(_ => Uloz(), _ => MuzeUlozit);

        PridejRadek();
    }

    private void NotifyKrok()
    {
        OnPropertyChanged(nameof(JeKrok1));
        OnPropertyChanged(nameof(JeKrok2));
        OnPropertyChanged(nameof(JeKrok3));
        OnPropertyChanged(nameof(MuzeUlozit));
        CommandManager.InvalidateRequerySuggested();
    }

    private bool MuzeDalsi()
    {
        if (Krok == 1) return true;
        if (Krok == 2) return Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);
        return false;
    }

    public bool MuzeUlozit => Krok == 3 && Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);

    private void PridejRadek()
    {
        var r = new VydejkaRadekViewModel();
        r.PropertyChanged += RadekZmenen;
        Radky.Add(r);
    }

    private void RadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(MuzeUlozit));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Uloz()
    {
        var v = new Vydejka
        {
            CisloDokladu = $"VY-{DateTime.Now:yyyyMMdd-HHmmss}",
            DatumVydeje  = DateTime.Now,
            Stredisko    = Stredisko,
            TypVydeje    = TypVydeje,
            Poznamka     = Poznamka,
            Radky        = Radky.Select(r => r.ToModel()).ToList()
        };

        try
        {
            DatabaseService.SaveVydejka(v);
            MessageBox.Show($"Výdejka uložena: {v.CisloDokladu}", "Hotovo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Hotovo?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nepodařilo se uložit výdejku:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
