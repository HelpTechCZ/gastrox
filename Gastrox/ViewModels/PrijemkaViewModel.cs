using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// ViewModel pro okno Příjemka (naskladnění).
/// Drží hlavičku dokladu + kolekci řádků a automaticky přepočítává celkové částky.
/// </summary>
public class PrijemkaViewModel : ViewModelBase
{
    private string _cisloDokladu;
    private DateTime _datumPrijeti = DateTime.Now;
    private string? _dodavatel;
    private string? _cisloFaktury;
    private string? _poznamka;

    public PrijemkaViewModel()
    {
        _cisloDokladu = GenerujCisloDokladu();

        Radky = new ObservableCollection<PrijemkaRadekViewModel>();
        Radky.CollectionChanged += (_, __) => PrihlasRadky();

        // Dostupné zboží pro našeptávač
        DostupneZbozi = new ObservableCollection<SkladovaKarta>(DatabaseService.LoadAktivniKarty());
        DostupneSazby = new ObservableCollection<SazbaDPH>(DatabaseService.LoadAktivniSazbyDph());

        PridatRadekCommand = new RelayCommand(_ => PridejPrazdnyRadek());
        OdebratRadekCommand = new RelayCommand(OdeberRadek, radek => radek is PrijemkaRadekViewModel);
        UlozitCommand = new RelayCommand(_ => Uloz(), _ => Radky.Count > 0 && Radky.All(r => r.VybraneZbozi is not null && r.PocetBaleni > 0));

        // Jeden prázdný řádek na začátek pro rychlé zadávání
        PridejPrazdnyRadek();
    }

    // ---------- Hlavička ----------
    public string CisloDokladu { get => _cisloDokladu; set => SetProperty(ref _cisloDokladu, value); }
    public DateTime DatumPrijeti { get => _datumPrijeti; set => SetProperty(ref _datumPrijeti, value); }
    public string? Dodavatel { get => _dodavatel; set => SetProperty(ref _dodavatel, value); }
    public string? CisloFaktury { get => _cisloFaktury; set => SetProperty(ref _cisloFaktury, value); }
    public string? Poznamka { get => _poznamka; set => SetProperty(ref _poznamka, value); }

    // ---------- Řádky ----------
    public ObservableCollection<PrijemkaRadekViewModel> Radky { get; }
    public ObservableCollection<SkladovaKarta> DostupneZbozi { get; }
    public ObservableCollection<SazbaDPH> DostupneSazby { get; }

    // ---------- Součty (odvozené) ----------
    public decimal CelkemBezDPH => Radky.Sum(r => r.CelkemBezDPH);
    public decimal CelkemSDPH   => Radky.Sum(r => r.CelkemSDPH);

    // ---------- Commands ----------
    public ICommand PridatRadekCommand { get; }
    public ICommand OdebratRadekCommand { get; }
    public ICommand UlozitCommand { get; }

    // ---------- Logika ----------
    private void PridejPrazdnyRadek()
    {
        var radek = new PrijemkaRadekViewModel(DostupneSazby);
        radek.PropertyChanged += RadekZmenen;
        Radky.Add(radek);
    }

    private void OdeberRadek(object? param)
    {
        if (param is PrijemkaRadekViewModel r)
        {
            r.PropertyChanged -= RadekZmenen;
            Radky.Remove(r);
            OnPropertyChanged(nameof(CelkemBezDPH));
            OnPropertyChanged(nameof(CelkemSDPH));
        }
    }

    private void PrihlasRadky()
    {
        OnPropertyChanged(nameof(CelkemBezDPH));
        OnPropertyChanged(nameof(CelkemSDPH));
    }

    private void RadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrijemkaRadekViewModel.CelkemBezDPH)
                            or nameof(PrijemkaRadekViewModel.CelkemSDPH))
        {
            OnPropertyChanged(nameof(CelkemBezDPH));
            OnPropertyChanged(nameof(CelkemSDPH));
        }
    }

    private void Uloz()
    {
        var prijemka = new Prijemka
        {
            CisloDokladu = CisloDokladu,
            DatumPrijeti = DatumPrijeti,
            Dodavatel    = Dodavatel,
            CisloFaktury = CisloFaktury,
            Poznamka     = Poznamka,
            CelkemBezDPH = CelkemBezDPH,
            CelkemSDPH   = CelkemSDPH,
            Radky        = Radky.Select(r => r.ToModel()).ToList()
        };

        DatabaseService.SavePrijemka(prijemka);

        // Reset formuláře pro další doklad
        Radky.Clear();
        PridejPrazdnyRadek();
        CisloDokladu = GenerujCisloDokladu();
    }

    private static string GenerujCisloDokladu()
        => $"PR-{DateTime.Now:yyyyMMdd-HHmmss}";
}
