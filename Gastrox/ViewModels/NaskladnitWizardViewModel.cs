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
/// Třístupňový průvodce naskladněním:
/// 1) Hlavička dokladu (dodavatel, faktura, datum)
/// 2) Položky příjmu
/// 3) Souhrn + uložení
/// </summary>
public class NaskladnitWizardViewModel : ViewModelBase
{
    public ObservableCollection<SkladovaKarta> DostupneZbozi { get; }
    public ObservableCollection<SazbaDPH> DostupneSazby { get; }
    public ObservableCollection<Sklad> Sklady { get; }
    public ObservableCollection<PrijemkaRadekViewModel> Radky { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad
    {
        get => _vybranySklad;
        set
        {
            if (SetProperty(ref _vybranySklad, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private int _krok = 1;
    public int Krok { get => _krok; set { if (SetProperty(ref _krok, value)) NotifyKrok(); } }
    public bool JeKrok1 => Krok == 1;
    public bool JeKrok2 => Krok == 2;
    public bool JeKrok3 => Krok == 3;

    private string _cisloDokladu;
    public string CisloDokladu { get => _cisloDokladu; set => SetProperty(ref _cisloDokladu, value); }

    private DateTime _datum = DateTime.Now;
    public DateTime DatumPrijeti { get => _datum; set => SetProperty(ref _datum, value); }

    private string? _dodavatel;
    public string? Dodavatel { get => _dodavatel; set => SetProperty(ref _dodavatel, value); }

    private string? _cisloFaktury;
    public string? CisloFaktury { get => _cisloFaktury; set => SetProperty(ref _cisloFaktury, value); }

    private string? _poznamka;
    public string? Poznamka { get => _poznamka; set => SetProperty(ref _poznamka, value); }

    public decimal CelkemBezDPH => Radky.Sum(r => r.CelkemBezDPH);
    public decimal CelkemSDPH   => Radky.Sum(r => r.CelkemSDPH);

    public ICommand DalsiCommand { get; }
    public ICommand ZpetCommand { get; }
    public ICommand PridatRadekCommand { get; }
    public ICommand OdebratRadekCommand { get; }
    public ICommand UlozitCommand { get; }

    public event Action? Hotovo;

    public NaskladnitWizardViewModel()
    {
        _cisloDokladu = $"PR-{DateTime.Now:yyyyMMdd-HHmmss}";

        DostupneZbozi = new ObservableCollection<SkladovaKarta>(DatabaseService.LoadAktivniKarty());
        DostupneSazby = new ObservableCollection<SazbaDPH>(DatabaseService.LoadAktivniSazbyDph());
        Sklady = new ObservableCollection<Sklad>(DatabaseService.LoadSklady());
        _vybranySklad = Sklady.FirstOrDefault(s => s.JeVychozi) ?? Sklady.FirstOrDefault();

        Radky.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CelkemBezDPH));
            OnPropertyChanged(nameof(CelkemSDPH));
        };

        DalsiCommand = new RelayCommand(_ => Krok++, _ => MuzeDalsi());
        ZpetCommand  = new RelayCommand(_ => Krok--, _ => Krok > 1);
        PridatRadekCommand  = new RelayCommand(_ => PridejRadek());
        OdebratRadekCommand = new RelayCommand(p =>
        {
            if (p is PrijemkaRadekViewModel r)
            {
                r.PropertyChanged -= RadekZmenen;
                Radky.Remove(r);
                OnPropertyChanged(nameof(CelkemBezDPH));
                OnPropertyChanged(nameof(CelkemSDPH));
            }
        });
        UlozitCommand = new RelayCommand(_ => Uloz(), _ => MuzeUlozit);

        PridejRadek();
    }

    private void NotifyKrok()
    {
        OnPropertyChanged(nameof(JeKrok1));
        OnPropertyChanged(nameof(JeKrok2));
        OnPropertyChanged(nameof(JeKrok3));
        CommandManager.InvalidateRequerySuggested();
    }

    private bool MuzeDalsi()
    {
        if (Krok == 1) return !string.IsNullOrWhiteSpace(CisloDokladu) && VybranySklad is not null;
        if (Krok == 2) return Radky.Count > 0 && Radky.All(r => r.VybraneZbozi is not null && r.PocetBaleni > 0);
        return false;
    }

    public bool MuzeUlozit
        => Krok == 3 && Radky.Count > 0 && Radky.All(r => r.VybraneZbozi is not null && r.PocetBaleni > 0);

    private void PridejRadek()
    {
        var r = new PrijemkaRadekViewModel(DostupneSazby);
        r.PropertyChanged += RadekZmenen;
        Radky.Add(r);
    }

    private void RadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrijemkaRadekViewModel.CelkemBezDPH)
                            or nameof(PrijemkaRadekViewModel.CelkemSDPH))
        {
            OnPropertyChanged(nameof(CelkemBezDPH));
            OnPropertyChanged(nameof(CelkemSDPH));
        }
        CommandManager.InvalidateRequerySuggested();
    }

    private void Uloz()
    {
        var p = new Prijemka
        {
            CisloDokladu = CisloDokladu,
            DatumPrijeti = DatumPrijeti,
            Dodavatel    = Dodavatel,
            CisloFaktury = CisloFaktury,
            Poznamka     = Poznamka,
            SkladId      = VybranySklad?.Id ?? 0,
            SkladNazev   = VybranySklad?.Nazev ?? string.Empty,
            CelkemBezDPH = CelkemBezDPH,
            CelkemSDPH   = CelkemSDPH,
            Radky        = Radky.Select(r => r.ToModel()).ToList()
        };

        try
        {
            DatabaseService.SavePrijemka(p);

            var odpoved = MessageBox.Show(
                $"Příjemka uložena: {p.CisloDokladu}\n\nVygenerovat PDF doklad?",
                "Hotovo", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (odpoved == MessageBoxResult.Yes)
            {
                try { PdfService.GenerujPrijemkuPdf(p, p.Radky); }
                catch (Exception pdfEx)
                {
                    MessageBox.Show("Chyba při generování PDF:\n" + pdfEx.Message, "Chyba",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            Hotovo?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nepodařilo se uložit příjemku:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
