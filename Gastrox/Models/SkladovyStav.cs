namespace Gastrox.Models;

/// <summary>
/// Stav konkrétní karty v konkrétním skladu.
/// </summary>
public class SkladovyStav
{
    public int SkladovaKartaId { get; set; }
    public int SkladId { get; set; }
    public decimal StavEvidencni { get; set; }
}
