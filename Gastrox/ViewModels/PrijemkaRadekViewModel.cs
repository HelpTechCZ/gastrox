using System.Collections.ObjectModel;
using System.Linq;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// Řádek příjemky ve formě ViewModelu – drží vlastní observable state
/// a přepočítává mezi počtem balení a evidenčními jednotkami v reálném čase.
/// Sazba DPH se vybírá z dostupných sazeb (sdílená kolekce z PrijemkaViewModelu).
/// </summary>
public class PrijemkaRadekViewModel : ViewModelBase
{
    private SkladovaKarta? _vybraneZbozi;
    private decimal _pocetBaleni;
    private decimal _nakupniCenaBezDPH;
    private SazbaDPH? _vybranaSazba;
    private BaleniKarty? _vybraneBaleni;

    public PrijemkaRadekViewModel(ObservableCollection<SazbaDPH> dostupneSazby)
    {
        DostupneSazby = dostupneSazby;
        _vybranaSazba = dostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? dostupneSazby.FirstOrDefault();
    }

    public ObservableCollection<SazbaDPH> DostupneSazby { get; }

    /// <summary>Varianty balení pro aktuálně vybrané zboží (Sud 50l, Sud 30l, Láhev 0,7l…).</summary>
    public ObservableCollection<BaleniKarty> DostupnaBaleni { get; } = new();

    public SkladovaKarta? VybraneZbozi
    {
        get => _vybraneZbozi;
        set
        {
            if (SetProperty(ref _vybraneZbozi, value) && value is not null)
            {
                // Načíst varianty balení pro tuto kartu
                DostupnaBaleni.Clear();
                var varianty = DatabaseService.LoadBaleniProKartu(value.Id);
                if (varianty.Count == 0)
                {
                    // Legacy karta bez variant – dopočítat z polí karty
                    varianty.Add(new BaleniKarty
                    {
                        SkladovaKartaId   = value.Id,
                        Nazev             = string.IsNullOrWhiteSpace(value.TypBaleni) ? "Výchozí" : value.TypBaleni,
                        KoeficientPrepoctu = value.KoeficientPrepoctu <= 0 ? 1m : value.KoeficientPrepoctu,
                        NakupniCenaBezDPH = value.NakupniCenaBezDPH,
                        JeVychozi         = true
                    });
                }
                foreach (var b in varianty)
                    DostupnaBaleni.Add(b);

                // Výchozí balení nebo první
                VybraneBaleni = DostupnaBaleni.FirstOrDefault(b => b.JeVychozi) ?? DostupnaBaleni.FirstOrDefault();

                // Předvyplnit sazbu z karty (najít odpovídající z dostupných)
                var match = DostupneSazby.FirstOrDefault(s => s.Sazba == value.SazbaDPH);
                if (match is not null) VybranaSazba = match;

                OnPropertyChanged(nameof(EvidencniJednotka));
                OnPropertyChanged(nameof(MnozstviEvidencni));
                OnPropertyChanged(nameof(ZobrazeniPrepoctu));
            }
        }
    }

    /// <summary>Aktuálně vybraná varianta balení této karty (pro tuto položku příjemky).</summary>
    public BaleniKarty? VybraneBaleni
    {
        get => _vybraneBaleni;
        set
        {
            if (SetProperty(ref _vybraneBaleni, value) && value is not null)
            {
                NakupniCenaBezDPH = value.NakupniCenaBezDPH;
                OnPropertyChanged(nameof(TypBaleni));
                OnPropertyChanged(nameof(KoeficientPrepoctu));
                OnPropertyChanged(nameof(MnozstviEvidencni));
                OnPropertyChanged(nameof(ZobrazeniPrepoctu));
                OnPropertyChanged(nameof(CelkemBezDPH));
                OnPropertyChanged(nameof(CelkemSDPH));
            }
        }
    }

    public decimal PocetBaleni
    {
        get => _pocetBaleni;
        set
        {
            if (SetProperty(ref _pocetBaleni, value))
            {
                OnPropertyChanged(nameof(MnozstviEvidencni));
                OnPropertyChanged(nameof(ZobrazeniPrepoctu));
                OnPropertyChanged(nameof(CelkemBezDPH));
                OnPropertyChanged(nameof(CelkemSDPH));
            }
        }
    }

    public decimal NakupniCenaBezDPH
    {
        get => _nakupniCenaBezDPH;
        set
        {
            if (SetProperty(ref _nakupniCenaBezDPH, value))
            {
                OnPropertyChanged(nameof(CelkemBezDPH));
                OnPropertyChanged(nameof(CelkemSDPH));
            }
        }
    }

    public SazbaDPH? VybranaSazba
    {
        get => _vybranaSazba;
        set
        {
            if (SetProperty(ref _vybranaSazba, value))
                OnPropertyChanged(nameof(CelkemSDPH));
        }
    }

    public decimal SazbaDphValue => _vybranaSazba?.Sazba ?? 0m;

    // ---- odvozené (read-only) ----

    public string TypBaleni           => _vybraneBaleni?.Nazev ?? "-";
    public string EvidencniJednotka   => _vybraneZbozi?.EvidencniJednotka ?? "-";
    public decimal KoeficientPrepoctu => _vybraneBaleni?.KoeficientPrepoctu ?? 0m;

    public decimal MnozstviEvidencni => PocetBaleni * KoeficientPrepoctu;

    public string ZobrazeniPrepoctu
    {
        get
        {
            if (_vybraneZbozi is null || PocetBaleni == 0) return "-";
            return $"{PocetBaleni:0.##} × {KoeficientPrepoctu:0.##} {EvidencniJednotka} = {MnozstviEvidencni:0.##} {EvidencniJednotka}";
        }
    }

    public decimal CelkemBezDPH => PocetBaleni * NakupniCenaBezDPH;
    public decimal CelkemSDPH   => CelkemBezDPH * (1 + SazbaDphValue / 100m);

    public PrijemkaRadek ToModel()
    {
        return new PrijemkaRadek
        {
            SkladovaKartaId    = _vybraneZbozi?.Id ?? 0,
            NazevZbozi         = _vybraneZbozi?.Nazev ?? string.Empty,
            TypBaleni          = TypBaleni,
            EvidencniJednotka  = EvidencniJednotka,
            PocetBaleni        = PocetBaleni,
            KoeficientPrepoctu = KoeficientPrepoctu,
            NakupniCenaBezDPH  = NakupniCenaBezDPH,
            SazbaDPH           = SazbaDphValue
        };
    }
}
