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
                NotifyCeny();
            }
        }
    }

    public decimal MnozstviEvidencni
    {
        get => _mnozstviEvidencni;
        set
        {
            if (SetProperty(ref _mnozstviEvidencni, value))
            {
                OnPropertyChanged(nameof(JeMnozstviPlatne));
                NotifyCeny();
            }
        }
    }

    public string EvidencniJednotka => _vybraneZbozi?.EvidencniJednotka ?? "-";
    public decimal StavNaSkladu     => _vybraneZbozi?.AktualniStavEvidencni ?? 0m;

    // ---- ceny za EJ ----
    public decimal NakupniCenaZaEJ  => _vybraneZbozi?.NakupniCenaZaJednotkuBezDPH ?? 0m;
    public decimal ProdejniCenaZaEJ => _vybraneZbozi?.ProdejniCenaZaJednotkuSDPH ?? 0m;
    private decimal Sazba            => _vybraneZbozi?.SazbaDPH ?? 21m;

    // ---- hodnoty řádku ----
    public decimal HodnotaNakupBezDPH  => NakupniCenaZaEJ * MnozstviEvidencni;
    public decimal HodnotaNakupSDPH    => HodnotaNakupBezDPH * (1 + Sazba / 100m);
    public decimal HodnotaProdejSDPH   => ProdejniCenaZaEJ * MnozstviEvidencni;
    public decimal HodnotaProdejBezDPH => Sazba > 0
        ? HodnotaProdejSDPH / (1 + Sazba / 100m)
        : HodnotaProdejSDPH;

    private void NotifyCeny()
    {
        OnPropertyChanged(nameof(NakupniCenaZaEJ));
        OnPropertyChanged(nameof(ProdejniCenaZaEJ));
        OnPropertyChanged(nameof(HodnotaNakupBezDPH));
        OnPropertyChanged(nameof(HodnotaNakupSDPH));
        OnPropertyChanged(nameof(HodnotaProdejBezDPH));
        OnPropertyChanged(nameof(HodnotaProdejSDPH));
    }

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
            NakupniCenaBezDPH = NakupniCenaZaEJ,
            ProdejniCenaSDPH  = ProdejniCenaZaEJ,
            SazbaDPH          = Sazba
        };
    }
}
