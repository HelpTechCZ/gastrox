using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
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

    // ---------- Rozpracované doklady ----------
    public ObservableCollection<Rozpracovano> Rozpracovane { get; } = new();
    public bool MaRozpracovane => Rozpracovane.Count > 0;
    private int _rozpracovanoId;

    // ---------- Commands ----------
    public ICommand DalsiCommand { get; }
    public ICommand ZpetCommand { get; }
    public ICommand PridatRadekCommand { get; }
    public ICommand OdebratRadekCommand { get; }
    public ICommand UlozitCommand { get; }
    public ICommand UlozitRozpracovaneCommand { get; }
    public ICommand PokracovatVRozpracovanemCommand { get; }
    public ICommand SmazatRozpracovaneCommand { get; }

    public event Action? Hotovo;
    public event Action? RadekPridan;

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
        UlozitRozpracovaneCommand = new RelayCommand(_ => UlozRozpracovane());
        PokracovatVRozpracovanemCommand = new RelayCommand(p =>
        {
            if (p is Rozpracovano roz) NacistZRozpracovaneho(roz);
        });
        SmazatRozpracovaneCommand = new RelayCommand(p =>
        {
            if (p is Rozpracovano roz)
            {
                DatabaseService.DeleteRozpracovano(roz.Id);
                Rozpracovane.Remove(roz);
                OnPropertyChanged(nameof(MaRozpracovane));
            }
        });

        NacistRozpracovane();
        PridejRadek();
    }

    private void NacistRozpracovane()
    {
        Rozpracovane.Clear();
        foreach (var r in DatabaseService.LoadRozpracovane("Prijemka"))
            Rozpracovane.Add(r);
        OnPropertyChanged(nameof(MaRozpracovane));
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
        RadekPridan?.Invoke();
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

    // ---------- Rozpracované – serializace ----------

    private void UlozRozpracovane()
    {
        var dto = new PrijemkaDraftDto(
            Krok, CisloDokladu, DatumPrijeti,
            VybranySklad?.Id,
            Dodavatel, CisloFaktury, Poznamka,
            Radky.Select(r => new PrijemkaRadekDraftDto(
                r.VybraneZbozi?.Id,
                r.VybraneBaleni?.Id,
                r.PocetBaleni,
                r.NakupniCenaBezDPH,
                r.VybranaSazba?.Sazba
            )).ToList()
        );

        var json = JsonSerializer.Serialize(dto);
        var roz = new Rozpracovano
        {
            Id    = _rozpracovanoId,
            Typ   = "Prijemka",
            Nazev = string.IsNullOrWhiteSpace(CisloDokladu) ? "Příjemka" : CisloDokladu,
            Data  = json
        };
        _rozpracovanoId = DatabaseService.SaveRozpracovano(roz);

        MessageBox.Show("Rozpracovaná příjemka uložena.\nMůžete se k ní vrátit kdykoliv.",
            "Uloženo", MessageBoxButton.OK, MessageBoxImage.Information);

        Hotovo?.Invoke();
    }

    private void NacistZRozpracovaneho(Rozpracovano roz)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<PrijemkaDraftDto>(roz.Data);
            if (dto is null) return;

            _rozpracovanoId = roz.Id;
            CisloDokladu = dto.CisloDokladu;
            DatumPrijeti = dto.DatumPrijeti;
            Dodavatel    = dto.Dodavatel;
            CisloFaktury = dto.CisloFaktury;
            Poznamka     = dto.Poznamka;

            if (dto.SkladId is int sid)
                VybranySklad = Sklady.FirstOrDefault(s => s.Id == sid) ?? VybranySklad;

            // Řádky
            foreach (var r in Radky) r.PropertyChanged -= RadekZmenen;
            Radky.Clear();

            foreach (var rd in dto.Radky)
            {
                var r = new PrijemkaRadekViewModel(DostupneSazby);
                r.PropertyChanged += RadekZmenen;

                if (rd.ZboziId is int zid)
                    r.VybraneZbozi = DostupneZbozi.FirstOrDefault(z => z.Id == zid);

                if (rd.BaleniId is int bid && r.DostupnaBaleni.Count > 0)
                    r.VybraneBaleni = r.DostupnaBaleni.FirstOrDefault(b => b.Id == bid) ?? r.VybraneBaleni;

                r.PocetBaleni        = rd.PocetBaleni;
                r.NakupniCenaBezDPH  = rd.NakupniCenaBezDPH;

                if (rd.SazbaDPH is decimal sazba)
                    r.VybranaSazba = DostupneSazby.FirstOrDefault(s => s.Sazba == sazba) ?? r.VybranaSazba;

                Radky.Add(r);
            }

            if (Radky.Count == 0)
                PridejRadek();

            Krok = Math.Clamp(dto.Krok, 1, 2); // nikdy nenačítat přímo na souhrn
            OnPropertyChanged(nameof(CelkemBezDPH));
            OnPropertyChanged(nameof(CelkemSDPH));

            // Skrýt panel rozpracovaných
            Rozpracovane.Clear();
            OnPropertyChanged(nameof(MaRozpracovane));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nepodařilo se načíst rozpracovaný doklad:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------- Finální uložení ----------

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

            // Smazat draft, pokud existuje
            if (_rozpracovanoId > 0)
                DatabaseService.DeleteRozpracovano(_rozpracovanoId);

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
