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
/// Třístupňový průvodce výdejem zboží:
/// 1) Typ výdeje + středisko
/// 2) Položky (zboží + množství)
/// 3) Souhrn + uložení
/// </summary>
public class VydejkaWizardViewModel : ViewModelBase
{
    public ObservableCollection<SkladovaKarta> DostupneZbozi { get; private set; }
    public ObservableCollection<Sklad> Sklady { get; }
    public ObservableCollection<VydejkaRadekViewModel> Radky { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad
    {
        get => _vybranySklad;
        set
        {
            if (SetProperty(ref _vybranySklad, value))
            {
                NacistZboziProSklad();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

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

    // ---------- Rozpracované ----------
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

    public VydejkaWizardViewModel()
    {
        Sklady = new ObservableCollection<Sklad>(DatabaseService.LoadSklady());
        _vybranySklad = Sklady.FirstOrDefault(s => s.JeVychozi) ?? Sklady.FirstOrDefault();

        DostupneZbozi = new ObservableCollection<SkladovaKarta>();
        NacistZboziProSklad();

        Radky.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(MuzeUlozit));
            NotifyCelkem();
        };

        DalsiCommand = new RelayCommand(_ => Krok++, _ => MuzeDalsi());
        ZpetCommand  = new RelayCommand(_ => Krok--, _ => Krok > 1);
        PridatRadekCommand  = new RelayCommand(_ => PridejRadek());
        OdebratRadekCommand = new RelayCommand(p => { if (p is VydejkaRadekViewModel r) Radky.Remove(r); });
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
        foreach (var r in DatabaseService.LoadRozpracovane("Vydejka"))
            Rozpracovane.Add(r);
        OnPropertyChanged(nameof(MaRozpracovane));
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
        if (Krok == 1) return VybranySklad is not null;
        if (Krok == 2) return Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);
        return false;
    }

    private void NacistZboziProSklad()
    {
        DostupneZbozi.Clear();
        var karty = _vybranySklad is null
            ? DatabaseService.LoadAktivniKarty()
            : DatabaseService.LoadAktivniKartyProSklad(_vybranySklad.Id);
        foreach (var k in karty)
            DostupneZbozi.Add(k);
    }

    public bool MuzeUlozit => Krok == 3 && Radky.Count > 0 && Radky.All(r => r.JeMnozstviPlatne);

    // ---- cenové součty ----
    public decimal CelkemNakupBezDPH  => Radky.Sum(r => r.HodnotaNakupBezDPH);
    public decimal CelkemNakupSDPH    => Radky.Sum(r => r.HodnotaNakupSDPH);
    public decimal CelkemProdejBezDPH => Radky.Sum(r => r.HodnotaProdejBezDPH);
    public decimal CelkemProdejSDPH   => Radky.Sum(r => r.HodnotaProdejSDPH);

    private void PridejRadek()
    {
        var r = new VydejkaRadekViewModel();
        r.PropertyChanged += RadekZmenen;
        Radky.Add(r);
        RadekPridan?.Invoke();
    }

    private void RadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(MuzeUlozit));
        NotifyCelkem();
        CommandManager.InvalidateRequerySuggested();
    }

    private void NotifyCelkem()
    {
        OnPropertyChanged(nameof(CelkemNakupBezDPH));
        OnPropertyChanged(nameof(CelkemNakupSDPH));
        OnPropertyChanged(nameof(CelkemProdejBezDPH));
        OnPropertyChanged(nameof(CelkemProdejSDPH));
    }

    // ---------- Rozpracované ----------

    private void UlozRozpracovane()
    {
        var dto = new VydejkaDraftDto(
            Krok,
            VybranySklad?.Id,
            TypVydeje.ToString(),
            Stredisko.ToString(),
            Poznamka,
            Radky.Select(r => new VydejkaRadekDraftDto(
                r.VybraneZbozi?.Id,
                r.MnozstviEvidencni
            )).ToList()
        );

        var json = JsonSerializer.Serialize(dto);
        var roz = new Rozpracovano
        {
            Id    = _rozpracovanoId,
            Typ   = "Vydejka",
            Nazev = $"Výdejka – {TypVydeje} ({Stredisko})",
            Data  = json
        };
        _rozpracovanoId = DatabaseService.SaveRozpracovano(roz);

        MessageBox.Show("Rozpracovaná výdejka uložena.\nMůžete se k ní vrátit kdykoliv.",
            "Uloženo", MessageBoxButton.OK, MessageBoxImage.Information);

        Hotovo?.Invoke();
    }

    private void NacistZRozpracovaneho(Rozpracovano roz)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<VydejkaDraftDto>(roz.Data);
            if (dto is null) return;

            _rozpracovanoId = roz.Id;
            Poznamka = dto.Poznamka;

            if (Enum.TryParse<TypVydeje>(dto.TypVydeje, out var tv))  TypVydeje = tv;
            if (Enum.TryParse<Stredisko>(dto.Stredisko, out var st))  Stredisko = st;

            if (dto.SkladId is int sid)
            {
                VybranySklad = Sklady.FirstOrDefault(s => s.Id == sid) ?? VybranySklad;
            }

            foreach (var r in Radky) r.PropertyChanged -= RadekZmenen;
            Radky.Clear();

            foreach (var rd in dto.Radky)
            {
                var r = new VydejkaRadekViewModel();
                r.PropertyChanged += RadekZmenen;

                if (rd.ZboziId is int zid)
                    r.VybraneZbozi = DostupneZbozi.FirstOrDefault(z => z.Id == zid);

                r.MnozstviEvidencni = rd.MnozstviEvidencni;
                Radky.Add(r);
            }

            if (Radky.Count == 0)
                PridejRadek();

            Krok = Math.Clamp(dto.Krok, 1, 2);

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
        var v = new Vydejka
        {
            CisloDokladu = $"VY-{DateTime.Now:yyyyMMdd-HHmmss}",
            DatumVydeje  = DateTime.Now,
            Stredisko    = Stredisko,
            TypVydeje    = TypVydeje,
            Poznamka     = Poznamka,
            SkladId      = VybranySklad?.Id ?? 0,
            SkladNazev   = VybranySklad?.Nazev ?? string.Empty,
            Radky        = Radky.Select(r => r.ToModel()).ToList()
        };

        try
        {
            DatabaseService.SaveVydejka(v);

            if (_rozpracovanoId > 0)
                DatabaseService.DeleteRozpracovano(_rozpracovanoId);

            var odpoved = MessageBox.Show(
                $"Výdejka uložena: {v.CisloDokladu}\n\nVygenerovat PDF doklad?",
                "Hotovo", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (odpoved == MessageBoxResult.Yes)
            {
                try { PdfService.GenerujVydejkuPdf(v, v.Radky); }
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
            MessageBox.Show("Nepodařilo se uložit výdejku:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
