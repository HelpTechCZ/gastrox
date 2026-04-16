using Gastrox.Models;

namespace Gastrox.ViewModels;

/// <summary>
/// Řádek výdejky – uživatel zadává množství buď v evidenčních jednotkách,
/// nebo v balení (přepočítá se přes koeficient na evidenční jednotky).
/// </summary>
public class VydejkaRadekViewModel : ViewModelBase
{
    private SkladovaKarta? _vybraneZbozi;
    private decimal _mnozstviEvidencni;

    public SkladovaKarta? VybraneZbozi
    {
        get => _vybraneZbozi;
        set
        {
            if (SetProperty(ref _vybraneZbozi, value))
            {
                OnPropertyChanged(nameof(EvidencniJednotka));
                OnPropertyChanged(nameof(StavNaSkladu));
                OnPropertyChanged(nameof(JeMnozstviPlatne));
            }
        }
    }

    public decimal MnozstviEvidencni
    {
        get => _mnozstviEvidencni;
        set
        {
            if (SetProperty(ref _mnozstviEvidencni, value))
                OnPropertyChanged(nameof(JeMnozstviPlatne));
        }
    }

    public string EvidencniJednotka => _vybraneZbozi?.EvidencniJednotka ?? "-";
    public decimal StavNaSkladu     => _vybraneZbozi?.AktualniStavEvidencni ?? 0m;

    /// <summary>True pokud je řádek validní (zboží vybráno, množství > 0 a nepřekračuje stav).</summary>
    public bool JeMnozstviPlatne
        => _vybraneZbozi is not null && MnozstviEvidencni > 0 && MnozstviEvidencni <= StavNaSkladu;

    public VydejkaRadek ToModel()
    {
        return new VydejkaRadek
        {
            SkladovaKartaId   = _vybraneZbozi?.Id ?? 0,
            NazevZbozi        = _vybraneZbozi?.Nazev ?? string.Empty,
            EvidencniJednotka = EvidencniJednotka,
            MnozstviEvidencni = MnozstviEvidencni,
            NakupniCenaBezDPH = _vybraneZbozi?.NakupniCenaBezDPH,
            SazbaDPH          = _vybraneZbozi?.SazbaDPH ?? 21m
        };
    }
}
