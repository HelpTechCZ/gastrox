using System.Collections.ObjectModel;
using System.Linq;
using Gastrox.Models;

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

    public PrijemkaRadekViewModel(ObservableCollection<SazbaDPH> dostupneSazby)
    {
        DostupneSazby = dostupneSazby;
        _vybranaSazba = dostupneSazby.FirstOrDefault(s => s.JeVychozi) ?? dostupneSazby.FirstOrDefault();
    }

    public ObservableCollection<SazbaDPH> DostupneSazby { get; }

    public SkladovaKarta? VybraneZbozi
    {
        get => _vybraneZbozi;
        set
        {
            if (SetProperty(ref _vybraneZbozi, value) && value is not null)
            {
                NakupniCenaBezDPH = value.NakupniCenaBezDPH;
                // Předvyplnit sazbu z karty (najít odpovídající z dostupných)
                var match = DostupneSazby.FirstOrDefault(s => s.Sazba == value.SazbaDPH);
                if (match is not null) VybranaSazba = match;

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

    public string TypBaleni           => _vybraneZbozi?.TypBaleni ?? "-";
    public string EvidencniJednotka   => _vybraneZbozi?.EvidencniJednotka ?? "-";
    public decimal KoeficientPrepoctu => _vybraneZbozi?.KoeficientPrepoctu ?? 0m;

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
