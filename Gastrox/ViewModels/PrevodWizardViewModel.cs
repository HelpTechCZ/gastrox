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
/// Třístupňový průvodce převodem mezi sklady:
/// 1) Zdrojový + cílový sklad
/// 2) Položky (zboží + množství)
/// 3) Souhrn + uložení
/// </summary>
public class PrevodWizardViewModel : ViewModelBase
{
    public ObservableCollection<Sklad> Sklady { get; }
    public ObservableCollection<SkladovaKarta> DostupneZbozi { get; } = new();
    public ObservableCollection<PrevodRadekViewModel> Radky { get; } = new();

    private int _krok = 1;
    public int Krok { get => _krok; set { if (SetProperty(ref _krok, value)) NotifyKrok(); } }
    public bool JeKrok1 => Krok == 1;
    public bool JeKrok2 => Krok == 2;
    public bool JeKrok3 => Krok == 3;

    private string _cisloDokladu;
    public string CisloDokladu { get => _cisloDokladu; set => SetProperty(ref _cisloDokladu, value); }

    private DateTime _datum = DateTime.Now;
    public DateTime DatumPrevodu { get => _datum; set => SetProperty(ref _datum, value); }

    private string? _poznamka;
    public string? Poznamka { get => _poznamka; set => SetProperty(ref _poznamka, value); }

    private Sklad? _skladZdroj;
    public Sklad? SkladZdroj
    {
        get => _skladZdroj;
        set
        {
            if (SetProperty(ref _skladZdroj, value))
            {
                NacistZboziProZdroj();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private Sklad? _skladCil;
    public Sklad? SkladCil
    {
        get => _skladCil;
        set
        {
            if (SetProperty(ref _skladCil, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand DalsiCommand { get; }
    public ICommand ZpetCommand { get; }
    public ICommand PridatRadekCommand { get; }
    public ICommand OdebratRadekCommand { get; }
    public ICommand UlozitCommand { get; }

    public event Action? Hotovo;

    public PrevodWizardViewModel()
    {
        _cisloDokladu = DatabaseService.GenerujCisloPrevodky();
        Sklady = new ObservableCollection<Sklad>(DatabaseService.LoadSklady());
        _skladZdroj = Sklady.FirstOrDefault(s => s.JeVychozi) ?? Sklady.FirstOrDefault();
        _skladCil = Sklady.FirstOrDefault(s => s.Id != (_skladZdroj?.Id ?? 0));
        NacistZboziProZdroj();

        Radky.CollectionChanged += (_, __) => OnPropertyChanged(nameof(MuzeUlozit));

        DalsiCommand = new RelayCommand(_ => Krok++, _ => MuzeDalsi());
        ZpetCommand  = new RelayCommand(_ => Krok--, _ => Krok > 1);
        PridatRadekCommand  = new RelayCommand(_ => PridejRadek());
        OdebratRadekCommand = new RelayCommand(p => { if (p is PrevodRadekViewModel r) Radky.Remove(r); });
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
        if (Krok == 1)
            return SkladZdroj is not null && SkladCil is not null && SkladZdroj.Id != SkladCil.Id
                && !string.IsNullOrWhiteSpace(CisloDokladu);
        if (Krok == 2)
            return Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);
        return false;
    }

    public bool MuzeUlozit => Krok == 3 && Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);

    private void NacistZboziProZdroj()
    {
        DostupneZbozi.Clear();
        if (_skladZdroj is null) return;
        foreach (var k in DatabaseService.LoadAktivniKartyProSklad(_skladZdroj.Id))
            DostupneZbozi.Add(k);
    }

    private void PridejRadek()
    {
        var r = new PrevodRadekViewModel();
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
        if (SkladZdroj is null || SkladCil is null) return;

        var p = new Prevodka
        {
            CisloDokladu = CisloDokladu,
            DatumPrevodu = DatumPrevodu,
            SkladZdrojId = SkladZdroj.Id,
            SkladCilId   = SkladCil.Id,
            Poznamka     = Poznamka,
            Radky        = Radky.Select(r => r.ToModel()).ToList()
        };

        try
        {
            DatabaseService.SavePrevodka(p);
            MessageBox.Show($"Převodka uložena: {p.CisloDokladu}", "Hotovo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Hotovo?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nepodařilo se uložit převodku:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
