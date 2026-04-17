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
        foreach (var r in DatabaseService.LoadRozpracovane("Prevodka"))
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
        RadekPridan?.Invoke();
    }

    private void RadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(MuzeUlozit));
        CommandManager.InvalidateRequerySuggested();
    }

    // ---------- Rozpracované ----------

    private void UlozRozpracovane()
    {
        var dto = new PrevodkaDraftDto(
            Krok, CisloDokladu, DatumPrevodu,
            SkladZdroj?.Id, SkladCil?.Id,
            Poznamka,
            Radky.Select(r => new PrevodkaRadekDraftDto(
                r.VybraneZbozi?.Id,
                r.MnozstviEvidencni
            )).ToList()
        );

        var json = JsonSerializer.Serialize(dto);
        var roz = new Rozpracovano
        {
            Id    = _rozpracovanoId,
            Typ   = "Prevodka",
            Nazev = string.IsNullOrWhiteSpace(CisloDokladu) ? "Převodka" : CisloDokladu,
            Data  = json
        };
        _rozpracovanoId = DatabaseService.SaveRozpracovano(roz);

        MessageBox.Show("Rozpracovaná převodka uložena.\nMůžete se k ní vrátit kdykoliv.",
            "Uloženo", MessageBoxButton.OK, MessageBoxImage.Information);

        Hotovo?.Invoke();
    }

    private void NacistZRozpracovaneho(Rozpracovano roz)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<PrevodkaDraftDto>(roz.Data);
            if (dto is null) return;

            _rozpracovanoId = roz.Id;
            CisloDokladu = dto.CisloDokladu;
            DatumPrevodu = dto.DatumPrevodu;
            Poznamka     = dto.Poznamka;

            if (dto.SkladZdrojId is int szid)
                SkladZdroj = Sklady.FirstOrDefault(s => s.Id == szid) ?? SkladZdroj;
            if (dto.SkladCilId is int scid)
                SkladCil = Sklady.FirstOrDefault(s => s.Id == scid) ?? SkladCil;

            foreach (var r in Radky) r.PropertyChanged -= RadekZmenen;
            Radky.Clear();

            foreach (var rd in dto.Radky)
            {
                var r = new PrevodRadekViewModel();
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
        if (SkladZdroj is null || SkladCil is null) return;

        var p = new Prevodka
        {
            CisloDokladu    = CisloDokladu,
            DatumPrevodu    = DatumPrevodu,
            SkladZdrojId    = SkladZdroj.Id,
            SkladZdrojNazev = SkladZdroj.Nazev,
            SkladCilId      = SkladCil.Id,
            SkladCilNazev   = SkladCil.Nazev,
            Poznamka        = Poznamka,
            Radky           = Radky.Select(r => r.ToModel()).ToList()
        };

        try
        {
            DatabaseService.SavePrevodka(p);

            if (_rozpracovanoId > 0)
                DatabaseService.DeleteRozpracovano(_rozpracovanoId);

            var odpoved = MessageBox.Show(
                $"Převodka uložena: {p.CisloDokladu}\n\nVygenerovat PDF doklad?",
                "Hotovo", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (odpoved == MessageBoxResult.Yes)
            {
                try { PdfService.GenerujPrevodkuPdf(p, p.Radky); }
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
            MessageBox.Show("Nepodařilo se uložit převodku:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
