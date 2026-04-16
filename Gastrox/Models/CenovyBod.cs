using System;

namespace Gastrox.Models;

/// <summary>
/// Jeden bod v grafu vývoje nákupních cen skladové karty.
/// Cena je přepočtena na evidenční jednotku (Kč/l, Kč/ks…), aby se dala
/// srovnávat napříč různými variantami balení.
/// </summary>
public class CenovyBod
{
    public DateTime Datum { get; set; }

    /// <summary>Nákupní cena za evidenční jednotku bez DPH.</summary>
    public decimal CenaZaJednotkuBezDPH { get; set; }

    /// <summary>Název balení (pro tooltip – „Sud 50l", „Láhev 0,7l" atd.).</summary>
    public string TypBaleni { get; set; } = string.Empty;
}
