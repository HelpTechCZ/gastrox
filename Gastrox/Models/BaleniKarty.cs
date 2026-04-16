namespace Gastrox.Models;

/// <summary>
/// Varianta balení na skladové kartě. Jedna karta může mít několik variant
/// (např. „Sud 50l", „Sud 30l", „Láhev 0,7l") s vlastním koeficientem přepočtu
/// a nákupní cenou za balení. Právě jedna varianta musí být označena jako výchozí –
/// ta se předvyplňuje při naskladnění a její hodnoty se zrcadlí do polí na kartě
/// (kvůli starším reportům, které čtou přímo SkladovaKarta.Nakupni_Cena_Bez_DPH).
/// </summary>
public class BaleniKarty
{
    public int Id { get; set; }
    public int SkladovaKartaId { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public decimal KoeficientPrepoctu { get; set; } = 1m;
    public decimal NakupniCenaBezDPH { get; set; }
    public bool JeVychozi { get; set; }
    public bool JeAktivni { get; set; } = true;
    public int Poradi { get; set; }

    public string Popis => string.IsNullOrWhiteSpace(Nazev) ? "(bez názvu)" : Nazev;
}
