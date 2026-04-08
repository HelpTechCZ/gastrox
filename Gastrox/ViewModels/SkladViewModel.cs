using System.Collections.ObjectModel;
using System.Linq;
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

    /// <summary>Pohyby pro aktuálně vybranou kartu (deník / historie).</summary>
    public ObservableCollection<PohybSkladu> Pohyby { get; } = new();

    public bool MaPohyby => Pohyby.Count > 0;

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
                }
                else
                {
                    Pohyby.Clear();
                    OnPropertyChanged(nameof(MaPohyby));
                }
            }
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
    private string _typBaleni = string.Empty;
    private decimal _koeficientPrepoctu = 1m;
    private decimal _nakupniCenaBezDPH;
    private SazbaDPH? _vybranaSazba;
    private decimal _prodejniCenaSDPH;
    private decimal _minimalniStav;
    private string? _dodavatel;

    public string Nazev                 { get => _nazev;             set => SetProperty(ref _nazev, value); }
    public string Kategorie_Sel         { get => _kategorie;         set => SetProperty(ref _kategorie, value); }
    public string? EAN                  { get => _ean;               set => SetProperty(ref _ean, value); }
    public string EvidencniJednotka_Sel { get => _evidencniJednotka; set => SetProperty(ref _evidencniJednotka, value); }
    public string TypBaleni             { get => _typBaleni;         set => SetProperty(ref _typBaleni, value); }
    public decimal KoeficientPrepoctu   { get => _koeficientPrepoctu;set => SetProperty(ref _koeficientPrepoctu, value); }

    public decimal NakupniCenaBezDPH
    {
        get => _nakupniCenaBezDPH;
        set
        {
            if (SetProperty(ref _nakupniCenaBezDPH, value))
            {
                OnPropertyChanged(nameof(NakupniCenaSDPH));
                OnPropertyChanged(nameof(MarzeKc));
                OnPropertyChanged(nameof(MarzeProcent));
            }
        }
    }

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

    public SkladViewModel()
    {
        NacistCiselniky();
        NacistSeznam();

        NovaKartaCommand = new RelayCommand(_ =>
        {
            VybranaKarta = null;
            VycistitFormular();
        });

        UlozitCommand = new RelayCommand(_ => Uloz(),
            _ => !string.IsNullOrWhiteSpace(Nazev) && KoeficientPrepoctu > 0 && VybranaSazba is not null);

        DeaktivovatCommand = new RelayCommand(_ => Deaktivuj(),
            _ => VybranaKarta is not null);
    }

    private void NacistCiselniky()
    {
        DostupneSazby.Clear();
        foreach (var s in DatabaseService.LoadAktivniSazbyDph())
            DostupneSazby.Add(s);

        Kategorie.Clear();
        foreach (var k in DatabaseService.LoadAktivniKategorie())
            Kategorie.Add(k);

        // Defaultně předvybrat výchozí sazbu a první kategorii
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? DostupneSazby.FirstOrDefault();
        if (string.IsNullOrEmpty(_kategorie) || Kategorie.All(k => k.Nazev != _kategorie))
            Kategorie_Sel = Kategorie.FirstOrDefault()?.Nazev ?? string.Empty;
    }

    private void NacistSeznam()
    {
        Karty.Clear();
        foreach (var k in DatabaseService.LoadAktivniKarty())
            Karty.Add(k);
    }

    private void NacistPohyby(int kartaId)
    {
        Pohyby.Clear();
        foreach (var p in DatabaseService.LoadPohybyKarty(kartaId))
            Pohyby.Add(p);
        OnPropertyChanged(nameof(MaPohyby));
    }

    private void NacistDoFormulare(SkladovaKarta k)
    {
        _editId = k.Id;
        Nazev = k.Nazev;
        Kategorie_Sel = k.Kategorie;
        EAN = k.EAN;
        EvidencniJednotka_Sel = k.EvidencniJednotka;
        TypBaleni = k.TypBaleni;
        KoeficientPrepoctu = k.KoeficientPrepoctu;
        NakupniCenaBezDPH = k.NakupniCenaBezDPH;
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.Sazba == k.SazbaDPH) ?? DostupneSazby.FirstOrDefault();
        ProdejniCenaSDPH = k.ProdejniCenaSDPH;
        MinimalniStav = k.MinimalniStav;
        Dodavatel = k.Dodavatel;
    }

    private void VycistitFormular()
    {
        _editId = 0;
        Nazev = string.Empty;
        Kategorie_Sel = Kategorie.FirstOrDefault()?.Nazev ?? string.Empty;
        EAN = null;
        EvidencniJednotka_Sel = "Litr";
        TypBaleni = string.Empty;
        KoeficientPrepoctu = 1m;
        NakupniCenaBezDPH = 0;
        VybranaSazba = DostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? DostupneSazby.FirstOrDefault();
        ProdejniCenaSDPH = 0;
        MinimalniStav = 0;
        Dodavatel = null;
    }

    private void Uloz()
    {
        var k = new SkladovaKarta
        {
            Id = _editId,
            Nazev = Nazev.Trim(),
            Kategorie = Kategorie_Sel,
            EAN = string.IsNullOrWhiteSpace(EAN) ? null : EAN!.Trim(),
            EvidencniJednotka = EvidencniJednotka_Sel,
            TypBaleni = TypBaleni?.Trim() ?? string.Empty,
            KoeficientPrepoctu = KoeficientPrepoctu,
            NakupniCenaBezDPH = NakupniCenaBezDPH,
            SazbaDPH = SazbaValue,
            ProdejniCenaSDPH = ProdejniCenaSDPH,
            MinimalniStav = MinimalniStav,
            Dodavatel = string.IsNullOrWhiteSpace(Dodavatel) ? null : Dodavatel!.Trim()
        };

        DatabaseService.SaveKarta(k);
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
