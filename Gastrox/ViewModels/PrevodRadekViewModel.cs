using Gastrox.Models;

namespace Gastrox.ViewModels;

/// <summary>
/// Řádek převodky – převáděné množství v evidenčních jednotkách.
/// </summary>
public class PrevodRadekViewModel : ViewModelBase
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

    public bool JeMnozstviPlatne
        => _vybraneZbozi is not null && MnozstviEvidencni > 0 && MnozstviEvidencni <= StavNaSkladu;

    public PrevodkaRadek ToModel()
    {
        return new PrevodkaRadek
        {
            SkladovaKartaId   = _vybraneZbozi?.Id ?? 0,
            MnozstviEvidencni = MnozstviEvidencni
        };
    }
}
