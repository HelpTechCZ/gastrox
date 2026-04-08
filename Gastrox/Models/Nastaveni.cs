namespace Gastrox.Models;

/// <summary>
/// Předdefinované klíče do tabulky Nastaveni (key/value).
/// </summary>
public static class NastaveniKey
{
    // ---- Firma (hlavička PDF reportů) ----
    public const string FirmaNazev = "firma.nazev";
    public const string FirmaIco   = "firma.ico";
    public const string FirmaDic   = "firma.dic";
    public const string FirmaUlice = "firma.ulice";
    public const string FirmaMesto = "firma.mesto";
    public const string FirmaPsc   = "firma.psc";
    public const string FirmaStat  = "firma.stat";
    public const string FirmaEmail = "firma.email";
    public const string FirmaTel   = "firma.telefon";

    // ---- Aktualizace ----
    public const string UpdateAutoCheck = "update.auto_check";       // "1"/"0"
    public const string UpdateLastCheck = "update.last_check";       // ISO datetime
}

public class FirmaInfo
{
    public string? Nazev { get; set; }
    public string? Ico { get; set; }
    public string? Dic { get; set; }
    public string? Ulice { get; set; }
    public string? Mesto { get; set; }
    public string? Psc { get; set; }
    public string? Stat { get; set; } = "ČR";
    public string? Email { get; set; }
    public string? Telefon { get; set; }
}
