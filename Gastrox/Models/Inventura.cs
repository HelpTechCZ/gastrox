using System;
using System.Collections.Generic;

namespace Gastrox.Models;

public class Inventura
{
    public int Id { get; set; }
    public DateTime DatumInventury { get; set; } = DateTime.Now;
    public string Nazev { get; set; } = string.Empty;
    public bool JeUzavrena { get; set; }
    public string? Poznamka { get; set; }

    public List<InventuraRadek> Radky { get; set; } = new();
}

public class InventuraRadek
{
    public int Id { get; set; }
    public int InventuraId { get; set; }
    public int SkladovaKartaId { get; set; }

    public string NazevZbozi { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;

    public decimal TeoretickyStav { get; set; }
    public decimal FyzickyStav { get; set; }
    public decimal Rozdil => FyzickyStav - TeoretickyStav;
}
