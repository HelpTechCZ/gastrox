using System;

namespace Gastrox.Models;

/// <summary>
/// Záznam v deníku skladu – auditní stopa všech změn na skladové kartě
/// (příjmy, výdeje, inventurní korekce).
/// </summary>
public class PohybSkladu
{
    public int Id { get; set; }
    public DateTime Datum { get; set; }
    public int SkladovaKartaId { get; set; }

    /// <summary>Prijem, Vydej, InventurniKorekce</summary>
    public string TypPohybu { get; set; } = string.Empty;

    /// <summary>+ pro příjem, − pro výdej, ± pro korekci.</summary>
    public decimal MnozstviEvidencni { get; set; }
    public decimal StavPoPohybu { get; set; }

    /// <summary>Prijemka, Vydejka, Inventura</summary>
    public string? DokladTyp { get; set; }
    public int? DokladId { get; set; }

    // ---- pomocné formátované vlastnosti pro UI ----

    public string TypPohybuLabel => TypPohybu switch
    {
        "Prijem"             => "Příjem",
        "Vydej"              => "Výdej",
        "InventurniKorekce"  => "Inventura",
        _                     => TypPohybu
    };

    /// <summary>Znaménko pro zobrazení (+/−).</summary>
    public string MnozstviText
        => MnozstviEvidencni >= 0 ? $"+{MnozstviEvidencni:N2}" : $"{MnozstviEvidencni:N2}";
}
