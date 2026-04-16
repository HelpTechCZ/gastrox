using Gastrox.Models;

namespace Gastrox.ViewModels;

/// <summary>
/// Wrapper kolem <see cref="BaleniKarty"/> pro inline editaci v DataGridu –
/// drží observable stav a propisuje změny na podkladový model přes SetProperty.
/// </summary>
public class BaleniRadekViewModel : ViewModelBase
{
    private int _id;
    private string _nazev;
    private decimal _koeficientPrepoctu;
    private decimal _nakupniCenaBezDPH;
    private bool _jeVychozi;

    public BaleniRadekViewModel() : this(new BaleniKarty { Nazev = string.Empty, KoeficientPrepoctu = 1m }) { }

    public BaleniRadekViewModel(BaleniKarty model)
    {
        _id                  = model.Id;
        _nazev               = model.Nazev;
        _koeficientPrepoctu  = model.KoeficientPrepoctu;
        _nakupniCenaBezDPH   = model.NakupniCenaBezDPH;
        _jeVychozi           = model.JeVychozi;
    }

    public int Id { get => _id; set => SetProperty(ref _id, value); }
    public string Nazev { get => _nazev; set => SetProperty(ref _nazev, value); }
    public decimal KoeficientPrepoctu { get => _koeficientPrepoctu; set => SetProperty(ref _koeficientPrepoctu, value); }
    public decimal NakupniCenaBezDPH { get => _nakupniCenaBezDPH; set => SetProperty(ref _nakupniCenaBezDPH, value); }
    public bool JeVychozi { get => _jeVychozi; set => SetProperty(ref _jeVychozi, value); }

    public BaleniKarty ToModel() => new()
    {
        Id                 = _id,
        Nazev              = (_nazev ?? string.Empty).Trim(),
        KoeficientPrepoctu = _koeficientPrepoctu,
        NakupniCenaBezDPH  = _nakupniCenaBezDPH,
        JeVychozi          = _jeVychozi,
        JeAktivni          = true
    };
}
