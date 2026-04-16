namespace Gastrox.Models;

/// <summary>
/// Fyzický sklad / lokace (např. Hlavní sklad, Bar, Kuchyň).
/// </summary>
public class Sklad
{
    public int Id { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public bool JeVychozi { get; set; }
    public bool JeAktivni { get; set; } = true;
    public int Poradi { get; set; }

    public override string ToString() => Nazev;
}
