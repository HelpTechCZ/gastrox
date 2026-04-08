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

    private int _pohybyZaTyden;
    public int PohybyZaTyden
    {
        get => _pohybyZaTyden;
        set => SetProperty(ref _pohybyZaTyden, value);
    }

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

        try
        {
            PohybyZaTyden = DatabaseService.SpocitatPohybyZaTyden();
        }
        catch
        {
            PohybyZaTyden = 0;
        }

        OnPropertyChanged(nameof(MaPodLimitem));
        OnPropertyChanged(nameof(PocetPodLimitem));
    }

    public bool MaPodLimitem => KartyPodLimitem.Count > 0;
    public int PocetPodLimitem => KartyPodLimitem.Count;
}
