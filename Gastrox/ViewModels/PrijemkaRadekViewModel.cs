using Gastrox.Models;

namespace Gastrox.ViewModels;

/// <summary>
/// Řádek příjemky ve formě ViewModelu – drží vlastní observable state
/// a přepočítává mezi počtem balení a evidenčními jednotkami v reálném čase.
/// </summary>
public class PrijemkaRadekViewModel : ViewModelBase
{
    private SkladovaKarta? _vybraneZbozi;
    private decimal _pocetBaleni;
    private decimal _nakupniCenaBezDPH;
    private decimal _sazbaDPH = 21m;

    public SkladovaKarta? VybraneZbozi
    {
        get => _vybraneZbozi;
        set
        {
            if (SetProperty(ref _vybraneZbozi, value) && value is not null)
            {
                // Automaticky předvyplníme cenu a sazbu z karty
                NakupniCenaBezDPH = value.NakupniCenaBezDPH;
                SazbaDPH = value.SazbaDPH;
                OnPropertyChanged(nameof(TypBaleni));
                OnPropertyChanged(nameof(EvidencniJednotka));
                OnPropertyChanged(nameof(KoeficientPrepoctu));
                OnPropertyChanged(nameof(MnozstviEvidencni));
                OnPropertyChanged(nameof(ZobrazeniPrepoctu));
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

    public decimal SazbaDPH
    {
        get => _sazbaDPH;
        set
        {
            if (SetProperty(ref _sazbaDPH, value))
                OnPropertyChanged(nameof(CelkemSDPH));
        }
    }

    // ---- odvozené (read-only) ----

    public string TypBaleni           => _vybraneZbozi?.TypBaleni ?? "-";
    public string EvidencniJednotka   => _vybraneZbozi?.EvidencniJednotka ?? "-";
    public decimal KoeficientPrepoctu => _vybraneZbozi?.KoeficientPrepoctu ?? 0m;

    /// <summary>
    /// Jádro přepočtu: počet balení × koeficient = evidenční jednotky.
    /// </summary>
    public decimal MnozstviEvidencni => PocetBaleni * KoeficientPrepoctu;

    /// <summary>
    /// Textové zobrazení přepočtu – např. "5 × 0,7 l = 3,5 l".
    /// </summary>
    public string ZobrazeniPrepoctu
    {
        get
        {
            if (_vybraneZbozi is null || PocetBaleni == 0) return "-";
            return $"{PocetBaleni:0.##} × {KoeficientPrepoctu:0.##} {EvidencniJednotka} = {MnozstviEvidencni:0.##} {EvidencniJednotka}";
        }
    }

    public decimal CelkemBezDPH => PocetBaleni * NakupniCenaBezDPH;
    public decimal CelkemSDPH   => CelkemBezDPH * (1 + SazbaDPH / 100m);

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
            SazbaDPH           = SazbaDPH
        };
    }
}
