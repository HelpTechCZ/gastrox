using System;

namespace Gastrox.Models;

/// <summary>
/// Rozpracovaný (nedokončený) doklad uložený jako JSON snapshot stavu wizardu.
/// Umožňuje uživateli přerušit zadávání a pokračovat později.
/// </summary>
public class Rozpracovano
{
    public int Id { get; set; }

    /// <summary>Prijemka, Vydejka, Prevodka</summary>
    public string Typ { get; set; } = string.Empty;

    /// <summary>Zobrazovaný název (typicky číslo dokladu).</summary>
    public string Nazev { get; set; } = string.Empty;

    /// <summary>JSON se stavem celého wizardu.</summary>
    public string Data { get; set; } = string.Empty;

    public DateTime Vytvoreno { get; set; }
    public DateTime Upraveno { get; set; }
}
