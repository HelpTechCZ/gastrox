using System;
using System.Collections.ObjectModel;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

/// <summary>
/// Hlavní rozcestník – akce + přehled "co dnes dělat".
/// Velké dlaždice spouští průvodce, widget pod limitem upozorňuje na docházející zboží.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    public ObservableCollection<SkladovaKarta> KartyPodLimitem { get; } = new();
    public ObservableCollection<SkladovaKarta> KartyExpirujici { get; } = new();

    private int _pohybyZaTyden;
    public int PohybyZaTyden
    {
        get => _pohybyZaTyden;
        set => SetProperty(ref _pohybyZaTyden, value);
    }

    // ---------- Finanční hodnota skladových zásob ----------
    private decimal _hodnotaNakupBezDph;
    public decimal HodnotaNakupBezDph { get => _hodnotaNakupBezDph; set => SetProperty(ref _hodnotaNakupBezDph, value); }

    private decimal _hodnotaNakupSDph;
    public decimal HodnotaNakupSDph { get => _hodnotaNakupSDph; set => SetProperty(ref _hodnotaNakupSDph, value); }

    private decimal _hodnotaProdejBezDph;
    public decimal HodnotaProdejBezDph { get => _hodnotaProdejBezDph; set => SetProperty(ref _hodnotaProdejBezDph, value); }

    private decimal _hodnotaProdejSDph;
    public decimal HodnotaProdejSDph { get => _hodnotaProdejSDph; set => SetProperty(ref _hodnotaProdejSDph, value); }

    public string Pozdrav
    {
        get
        {
            var h = DateTime.Now.Hour;
            if (h < 11) return "Dobré ráno";
            if (h < 17) return "Dobrý den";
            return "Dobrý večer";
        }
    }

    public string DnesniDatum => DateTime.Now.ToString("dddd d. MMMM yyyy");

    public DashboardViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        KartyPodLimitem.Clear();
        foreach (var k in DatabaseService.LoadKartyPodLimitem())
            KartyPodLimitem.Add(k);

        KartyExpirujici.Clear();
        foreach (var k in DatabaseService.LoadKartyExpirujici())
            KartyExpirujici.Add(k);

        try
        {
            PohybyZaTyden = DatabaseService.SpocitatPohybyZaTyden();
        }
        catch
        {
            PohybyZaTyden = 0;
        }

        SpocitatHodnotuSkladu();

        OnPropertyChanged(nameof(MaPodLimitem));
        OnPropertyChanged(nameof(PocetPodLimitem));
        OnPropertyChanged(nameof(MaExpirujici));
        OnPropertyChanged(nameof(PocetExpirujici));
    }

    /// <summary>
    /// Spočítá celkovou finanční hodnotu skladových zásob – v nákupu i v prodeji,
    /// s DPH i bez. Cena na kartě je za balení; přepočet na evidenční jednotku
    /// jde přes KoeficientPrepoctu.
    /// </summary>
    private void SpocitatHodnotuSkladu()
    {
        decimal nakupBez = 0m, nakupS = 0m, prodejBez = 0m, prodejS = 0m;

        try
        {
            foreach (var k in DatabaseService.LoadAktivniKarty())
            {
                if (k.KoeficientPrepoctu <= 0 || k.AktualniStavEvidencni <= 0) continue;

                var nakupZaEj  = k.NakupniCenaBezDPH  / k.KoeficientPrepoctu;
                var prodejZaEj = k.ProdejniCenaSDPH   / k.KoeficientPrepoctu;
                var dphMult    = 1m + k.SazbaDPH / 100m;

                var hodnNakupBez  = k.AktualniStavEvidencni * nakupZaEj;
                var hodnProdejS   = k.AktualniStavEvidencni * prodejZaEj;
                var hodnNakupS    = hodnNakupBez * dphMult;
                var hodnProdejBez = dphMult > 0 ? hodnProdejS / dphMult : hodnProdejS;

                nakupBez  += hodnNakupBez;
                nakupS    += hodnNakupS;
                prodejBez += hodnProdejBez;
                prodejS   += hodnProdejS;
            }
        }
        catch
        {
            // Selhání nesmí shodit dashboard
        }

        HodnotaNakupBezDph   = decimal.Round(nakupBez,  2);
        HodnotaNakupSDph     = decimal.Round(nakupS,    2);
        HodnotaProdejBezDph  = decimal.Round(prodejBez, 2);
        HodnotaProdejSDph    = decimal.Round(prodejS,   2);
    }

    public bool MaPodLimitem => KartyPodLimitem.Count > 0;
    public int PocetPodLimitem => KartyPodLimitem.Count;
    public bool MaExpirujici => KartyExpirujici.Count > 0;
    public int PocetExpirujici => KartyExpirujici.Count;
}
