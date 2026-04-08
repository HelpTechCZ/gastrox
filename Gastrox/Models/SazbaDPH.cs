namespace Gastrox.Models;

public class SazbaDPH
{
    public int Id { get; set; }
    public decimal Sazba { get; set; }
    public string Popis { get; set; } = string.Empty;
    public bool JeVychozi { get; set; }
    public bool JeAktivni { get; set; } = true;

    public override string ToString() => $"{Popis} ({Sazba:0.##} %)";
}
