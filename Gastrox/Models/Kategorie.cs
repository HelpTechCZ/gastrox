namespace Gastrox.Models;

public class Kategorie
{
    public int Id { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public int Poradi { get; set; }
    public bool JeAktivni { get; set; } = true;

    public override string ToString() => Nazev;
}
