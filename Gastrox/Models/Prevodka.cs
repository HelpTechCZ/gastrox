using System;
using System.Collections.Generic;

namespace Gastrox.Models;

/// <summary>
/// Převodka – přesun zásob mezi dvěma sklady (jeden doklad, dva pohyby).
/// </summary>
public class Prevodka
{
    public int Id { get; set; }
    public string CisloDokladu { get; set; } = string.Empty;
    public DateTime DatumPrevodu { get; set; } = DateTime.Now;
    public int SkladZdrojId { get; set; }
    public int SkladCilId { get; set; }
    public string? Poznamka { get; set; }
    public DateTime Vytvoreno { get; set; } = DateTime.Now;

    // Zobrazovací data (join)
    public string SkladZdrojNazev { get; set; } = string.Empty;
    public string SkladCilNazev { get; set; } = string.Empty;

    public List<PrevodkaRadek> Radky { get; set; } = new();
}

public class PrevodkaRadek
{
    public int Id { get; set; }
    public int PrevodkaId { get; set; }
    public int SkladovaKartaId { get; set; }
    public decimal MnozstviEvidencni { get; set; }

    // Zobrazovací data
    public string NazevKarty { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;
}
