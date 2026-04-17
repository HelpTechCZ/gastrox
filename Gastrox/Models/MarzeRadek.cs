namespace Gastrox.Models;

/// <summary>
/// Agregovaný řádek marže za období – jeden záznam = jedna skladová karta.
/// </summary>
public class MarzeRadek
{
    public int KartaId { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public string Kategorie { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;
    public decimal Mnozstvi { get; set; }
    public decimal NakupBezDPH { get; set; }
    public decimal NakupSDPH { get; set; }
    public decimal ProdejBezDPH { get; set; }
    public decimal ProdejSDPH { get; set; }

    public decimal MarzeKc => ProdejBezDPH - NakupBezDPH;
    public decimal MarzeProcent => ProdejBezDPH > 0
        ? decimal.Round(MarzeKc / ProdejBezDPH * 100m, 1)
        : 0m;
}
