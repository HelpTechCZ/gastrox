using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// Správa skladových karet – seznam + editační formulář pro novou/upravovanou kartu.
/// Pattern: master-detail v jednom ViewModelu, aby to pro provozního bylo co nejjednodušší.
/// </summary>
public class SkladViewModel : ViewModelBase
{
    // ---------- Seznam ----------
    public ObservableCollection<SkladovaKarta> Karty { get; } = new();

    /// <summary>Filtrovaný/vyhledatelný pohled na Karty (binduje se DataGridem).</summary>
    public ICollectionView KartyView { get; }

    private const string FiltrVse = "(Všechny)";

    /// <summary>Možnosti pro filtr kategorií = "(Všechny)" + reálné kategorie.</summary>
    public ObservableCollection<string> KategorieFiltr { get; } = new();

    private string _vybranaFiltrKategorie = FiltrVse;
    public string VybranaFiltrKategorie
    {
        get => _vybranaFiltrKategorie;
        set { if (SetProperty(ref _vybranaFiltrKategorie, value)) KartyView?.Refresh(); }
    }

    private string _hledanyText = string.Empty;
    public string HledanyText
    {
        get => _hledanyText;
        set { if (SetProperty(ref _hledanyText, value)) KartyView?.Refresh(); }
    }

    /// <summary>Pohyby pro aktuálně vybranou kartu (deník / historie).</summary>
    public ObservableCollection<PohybSkladu> Pohyby { get; } = new();

    public bool MaPohyby => Pohyby.Count > 0;

    /// <summary>Body grafu vývoje nákupních cen (Kč za evidenční jednotku).</summary>
    public ObservableCollection<CenovyBod> HistorieCen { get; } = new();

    public bool MaHistoriiCen => HistorieCen.Count > 0;

    private SkladovaKarta? _vybranaKarta;
    public SkladovaKarta? VybranaKarta
    {
        get => _vybranaKarta;
        set
        {
            if (SetProperty(ref _vybranaKarta, value))
            {
                if (value is not null)
                {
                    NacistDoFormulare(value);
                    NacistPohyby(value.Id);
                    NacistHistoriiCen(value.Id);
                }
                else
                {
                    Pohyby.Clear();
                    OnPropertyChanged(nameof(MaPohyby));
                    HistorieCen.Clear();
                    OnPropertyChanged(nameof(MaHistoriiCen));
                }
            }
        }
    }

    // ---------- Sklady (výběr aktivního skladu) ----------
    public ObservableCollection<Sklad> Sklady { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad
    {
        get => _vybranySklad;
        set
        {
            if (SetProperty(ref _vybranySklad, value) && value is not null)
                NacistSeznam();
        }
    }

    // ---------- Číselníky pro drop-downy ----------
    public ObservableCollection<Kategorie> Kategorie { get; } = new();

    public string[] EvidencniJednotky { get; } = new[] { "Litr", "Kg", "Kus", "ml", "g" };

    public ObservableCollection<SazbaDPH> DostupneSazby { get; } = new();

    // ---------- Formulář (editovaná karta) ----------
    private int _editId;
    private string _nazev = string.Empty;
    private string _kategorie = "Tvrdý alkohol";
    private string? _ean;
    private string _evidencniJednotka = "Litr";
    private SazbaDPH? _vybranaSazba;
    private decimal _prodejniCenaSDPH;
    private decimal _minimalniStav;
    private string? _dodavatel;

    public string Nazev                 { get => _nazev;             set => SetProperty(ref _nazev, value); }
    public string Kategorie_Sel         { get => _kategorie;         set => SetProperty(ref _kategorie, value); }
    public string? EAN                  { get => _ean;               set => SetProperty(ref _ean, value); }
    public string EvidencniJednotka_Sel { get => _evidencniJednotka; set => SetProperty(ref _evidencniJednotka, value); }

    /// <summary>Varianty balení editované karty (např. „Sud 50l" + „Sud 30l").</summary>
    public ObservableCollection<BaleniRadekViewModel> Baleni { get; } = new();

    /// <summary>Nákupní cena z výchozí varianty (slouží pro výpočet marže a SDPH náhledu).</summary>
    public decimal NakupniCenaBezDPH
        => Baleni.FirstOrDefault(b => b.JeVychozi)?.NakupniCenaBezDPH ?? 0m;

    public SazbaDPH? VybranaSazba
    {
        get => _vybranaSazba;
        set
        {
            if (SetProperty(ref _vybranaSazba, value))
            {
                OnPropertyChanged(nameof(NakupniCenaSDPH));
                OnPropertyChanged(nameof(ProdejniCenaBezDPH));
                OnPropertyChanged(nameof(MarzeKc));
                OnPropertyChanged(nameof(MarzeProcent));
            }
        }
    }

    public decimal ProdejniCenaSDPH
    {
        get => _prodejniCenaSDPH;
        set
        {
            if (SetProperty(ref _prodejniCenaSDPH, value))
            {
                OnPropertyChanged(nameof(ProdejniCenaBezDPH));
                OnPropertyChanged(nameof(MarzeKc));
                OnPropertyChanged(nameof(MarzeProcent));
            }
        }
    }

    public decimal MinimalniStav { get => _minimalniStav; set => SetProperty(ref _minimalniStav, value); }
    public string? Dodavatel     { get => _dodavatel;     set => SetProperty(ref _dodavatel, value); }

    // ---------- Vypočítané (read-only pro UI) ----------
    private decimal SazbaValue => _vybranaSazba?.Sazba ?? 0m;
    public decimal NakupniCenaSDPH => NakupniCenaBezDPH * (1 + SazbaValue / 100m);
    public decimal ProdejniCenaBezDPH
        => SazbaValue > 0 ? ProdejniCenaSDPH / (1 + SazbaValue / 100m) : ProdejniCenaSDPH;
    public decimal MarzeKc => ProdejniCenaBezDPH - NakupniCenaBezDPH;
    public decimal MarzeProcent
        => ProdejniCenaBezDPH > 0 ? System.Math.Round((MarzeKc / ProdejniCenaBezDPH) * 100m, 1) : 0m;

    // ---------- Commands ----------
    public ICommand NovaKartaCommand { get; }
    public ICommand UlozitCommand { get; }
    public ICommand DeaktivovatCommand { get; }
    public ICommand PridatBaleniCommand { get; }
    public ICommand OdebratBaleniCommand { get; }
    public ICommand NastavitVychoziBaleniCommand { get; }

    public SkladViewModel()
    {
        NacistCiselniky();
        NacistSeznam();

        KartyView = CollectionViewSource.GetDefaultView(Karty);
        KartyView.Filter = FiltrujKartu;
        NaplnKategorieFiltr();

        NovaKartaCommand = new RelayCommand(_ =>
        {
            VybranaKarta = null;
            VycistitFormular();
        });

        UlozitCommand = new RelayCommand(_ => Uloz(),
            _ => !string.IsNullOrWhiteSpace(Nazev)
              && VybranaSazba is not null
              && Baleni.Count > 0
              && Baleni.Count(b => b.JeVychozi) == 1
              && Baleni.All(b => b.KoeficientPrepoctu > 0 && !string.IsNullOrWhiteSpace(b.Nazev)));

        DeaktivovatCommand = new RelayCommand(_ => Deaktivuj(),
            _ => VybranaKarta is not null);

        PridatBaleniCommand = new RelayCommand(_ =>
        {
            var nove = new BaleniRadekViewModel
            {
                Nazev = string.Empty,
                KoeficientPrepoctu = 1m,
                NakupniCenaBezDPH = 0m,
                JeVychozi = Baleni.Count == 0
            };
            Baleni.Add(nove);
            OnPropertyChanged(nameof(NakupniCenaBezDPH));
            OnPropertyChanged(nameof(NakupniCenaSDPH));
            OnPropertyChanged(nameof(MarzeKc));
            OnPropertyChanged(nameof(MarzeProcent));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        OdebratBaleniCommand = new RelayCommand(p =>
        {
            if (p is not BaleniRadekViewModel r) return;
            if (Baleni.Count <= 1)
            {
                MessageBox.Show("Karta musí mít alespoň jednu variantu balení.", "Upozornění",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var byloVychozi = r.JeVychozi;
            Baleni.Remove(r);
            if (byloVychozi && Baleni.Count > 0)
                Baleni[0].JeVychozi = true;
            OnPropertyChanged(nameof(NakupniCenaBezDPH));
            OnPropertyChanged(nameof(NakupniCenaSDPH));
            OnPropertyChanged(nameof(MarzeKc));
            OnPropertyChanged(nameof(MarzeProcent));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        NastavitVychoziBaleniCommand = new RelayCommand(p =>
        {
            if (p is not BaleniRadekViewModel r) return;
            foreach (var b in Baleni) b.JeVychozi = false;
            r.JeVychozi = true;
            OnPropertyChanged(nameof(NakupniCenaBezDPH));
            OnPropertyChanged(nameof(NakupniCenaSDPH));
            OnPropertyChanged(nameof(MarzeKc));
            OnPropertyChanged(nameof(MarzeProcent));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });
    }

    private void NacistCiselniky()
    {
        DostupneSazby.Clear();
        foreach (var s in DatabaseService.LoadAktivniSazbyDph())
            DostupneSazby.Add(s);

        Kategorie.Clear();
        foreach (var k in DatabaseService.LoadAktivniKategorie())
            Kategorie.Add(k);

        Sklady.Clear();
        foreach (var s in DatabaseService.LoadSklady())
            Sklady.Add(s);
        _vybranySklad = Sklady.FirstOrDefault(s => s.JeVychozi) ?? Sklady.FirstOrDefault();
        OnPropertyChanged(nameof(VybranySklad));

        // Defaultně předvybrat výchozí sazbu a první kategorii
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? DostupneSazby.FirstOrDefault();
        if (string.IsNullOrEmpty(_kategorie) || Kategorie.All(k => k.Nazev != _kategorie))
            Kategorie_Sel = Kategorie.FirstOrDefault()?.Nazev ?? string.Empty;
    }

    private void NacistSeznam()
    {
        Karty.Clear();
        var list = _vybranySklad is null
            ? DatabaseService.LoadAktivniKarty()
            : DatabaseService.LoadAktivniKartyProSklad(_vybranySklad.Id);
        foreach (var k in list)
            Karty.Add(k);
    }

    private void NaplnKategorieFiltr()
    {
        KategorieFiltr.Clear();
        KategorieFiltr.Add(FiltrVse);
        foreach (var k in Kategorie)
            KategorieFiltr.Add(k.Nazev);

        // Pokud zmizela aktuálně zvolená, vrátit se na "(Všechny)"
        if (!KategorieFiltr.Contains(_vybranaFiltrKategorie))
            VybranaFiltrKategorie = FiltrVse;
    }

    private bool FiltrujKartu(object obj)
    {
        if (obj is not SkladovaKarta k) return false;

        if (_vybranaFiltrKategorie != FiltrVse && k.Kategorie != _vybranaFiltrKategorie)
            return false;

        if (!string.IsNullOrWhiteSpace(_hledanyText))
        {
            var q = _hledanyText.Trim();
            var nazevMatch = (k.Nazev?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            var eanMatch   = (k.EAN?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            if (!nazevMatch && !eanMatch) return false;
        }

        return true;
    }

    private void NacistPohyby(int kartaId)
    {
        Pohyby.Clear();
        foreach (var p in DatabaseService.LoadPohybyKarty(kartaId))
            Pohyby.Add(p);
        OnPropertyChanged(nameof(MaPohyby));
    }

    private void NacistHistoriiCen(int kartaId)
    {
        HistorieCen.Clear();
        foreach (var b in DatabaseService.LoadHistorieNakupnichCen(kartaId))
            HistorieCen.Add(b);
        OnPropertyChanged(nameof(MaHistoriiCen));
    }

    private void NacistDoFormulare(SkladovaKarta k)
    {
        _editId = k.Id;
        Nazev = k.Nazev;
        Kategorie_Sel = k.Kategorie;
        EAN = k.EAN;
        EvidencniJednotka_Sel = k.EvidencniJednotka;
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.Sazba == k.SazbaDPH) ?? DostupneSazby.FirstOrDefault();
        ProdejniCenaSDPH = k.ProdejniCenaSDPH;
        MinimalniStav = k.MinimalniStav;
        Dodavatel = k.Dodavatel;

        // Načíst varianty balení karty
        Baleni.Clear();
        var varianty = DatabaseService.LoadBaleniProKartu(k.Id);
        if (varianty.Count == 0)
        {
            // Legacy karta bez variant – vytvořit výchozí z polí karty
            varianty.Add(new BaleniKarty
            {
                Nazev = string.IsNullOrWhiteSpace(k.TypBaleni) ? "Výchozí" : k.TypBaleni,
                KoeficientPrepoctu = k.KoeficientPrepoctu <= 0 ? 1m : k.KoeficientPrepoctu,
                NakupniCenaBezDPH = k.NakupniCenaBezDPH,
                JeVychozi = true
            });
        }
        foreach (var b in varianty)
        {
            var vm = new BaleniRadekViewModel(b);
            vm.PropertyChanged += BaleniRadekZmenen;
            Baleni.Add(vm);
        }
        OnPropertyChanged(nameof(NakupniCenaBezDPH));
        OnPropertyChanged(nameof(NakupniCenaSDPH));
        OnPropertyChanged(nameof(MarzeKc));
        OnPropertyChanged(nameof(MarzeProcent));
    }

    private void BaleniRadekZmenen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BaleniRadekViewModel.NakupniCenaBezDPH)
                            or nameof(BaleniRadekViewModel.JeVychozi))
        {
            OnPropertyChanged(nameof(NakupniCenaBezDPH));
            OnPropertyChanged(nameof(NakupniCenaSDPH));
            OnPropertyChanged(nameof(MarzeKc));
            OnPropertyChanged(nameof(MarzeProcent));
        }
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void VycistitFormular()
    {
        _editId = 0;
        Nazev = string.Empty;
        Kategorie_Sel = Kategorie.FirstOrDefault()?.Nazev ?? string.Empty;
        EAN = null;
        EvidencniJednotka_Sel = "Litr";
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? DostupneSazby.FirstOrDefault();
        ProdejniCenaSDPH = 0;
        MinimalniStav = 0;
        Dodavatel = null;

        Baleni.Clear();
        var vychozi = new BaleniRadekViewModel
        {
            Nazev = string.Empty,
            KoeficientPrepoctu = 1m,
            NakupniCenaBezDPH = 0m,
            JeVychozi = true
        };
        vychozi.PropertyChanged += BaleniRadekZmenen;
        Baleni.Add(vychozi);

        OnPropertyChanged(nameof(NakupniCenaBezDPH));
        OnPropertyChanged(nameof(NakupniCenaSDPH));
        OnPropertyChanged(nameof(MarzeKc));
        OnPropertyChanged(nameof(MarzeProcent));
    }

    private void Uloz()
    {
        // Demo limit: max 20 karet bez licence (pouze při zakládání nové)
        if (_editId == 0 && !LicenseService.IsLicensed && Karty.Count >= LicenseService.DemoMaxKaret)
        {
            MessageBox.Show(
                $"DEMO verze je omezena na {LicenseService.DemoMaxKaret} skladových karet.\n\nPro neomezenou verzi zadejte licenční klíč v Nastavení → Licence.",
                "DEMO omezení", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Výchozí varianta zrcadlí pole na kartě (zpětná kompatibilita reportů)
        var vychozi = Baleni.FirstOrDefault(b => b.JeVychozi);
        if (vychozi is null)
        {
            MessageBox.Show("Označte jednu variantu balení jako výchozí.", "Upozornění",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var k = new SkladovaKarta
        {
            Id = _editId,
            Nazev = Nazev.Trim(),
            Kategorie = Kategorie_Sel,
            EAN = string.IsNullOrWhiteSpace(EAN) ? null : EAN!.Trim(),
            EvidencniJednotka = EvidencniJednotka_Sel,
            TypBaleni = (vychozi.Nazev ?? string.Empty).Trim(),
            KoeficientPrepoctu = vychozi.KoeficientPrepoctu,
            NakupniCenaBezDPH = vychozi.NakupniCenaBezDPH,
            SazbaDPH = SazbaValue,
            ProdejniCenaSDPH = ProdejniCenaSDPH,
            MinimalniStav = MinimalniStav,
            Dodavatel = string.IsNullOrWhiteSpace(Dodavatel) ? null : Dodavatel!.Trim()
        };

        var kartaId = DatabaseService.SaveKarta(k);
        try
        {
            DatabaseService.SaveBaleniProKartu(kartaId, Baleni.Select(b => b.ToModel()));
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Nepodařilo se uložit varianty balení:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        NacistSeznam();
        VycistitFormular();
    }

    private void Deaktivuj()
    {
        if (VybranaKarta is null) return;
        DatabaseService.DeactivateKarta(VybranaKarta.Id);
        NacistSeznam();
        VycistitFormular();
    }
}
