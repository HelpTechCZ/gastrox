using System;
using System.Collections.Generic;

namespace Gastrox.Models;

public enum TypVydeje
{
    Prodej,
    VlastniSpotreba,
    OdpisZlom,
    OdpisSanitace,
    OdpisExpirace
}

public enum Stredisko
{
    Bar,
    Kuchyne
}

public class Vydejka
{
    public int Id { get; set; }
    public string CisloDokladu { get; set; } = string.Empty;
    public DateTime DatumVydeje { get; set; } = DateTime.Now;
    public Stredisko Stredisko { get; set; } = Stredisko.Bar;
    public TypVydeje TypVydeje { get; set; } = TypVydeje.Prodej;
    public string? Poznamka { get; set; }

    /// <summary>Zdrojový sklad pro tuto výdejku.</summary>
    public int SkladId { get; set; }

    /// <summary>Zobrazovací název skladu (join).</summary>
    public string SkladNazev { get; set; } = string.Empty;

    public List<VydejkaRadek> Radky { get; set; } = new();

    // ---- formátované vlastnosti pro UI ----

    public string StrediskoLabel => Stredisko switch
    {
        Stredisko.Bar     => "Bar",
        Stredisko.Kuchyne => "Kuchyně",
        _                 => Stredisko.ToString()
    };

    public string TypVydejeLabel => TypVydeje switch
    {
        TypVydeje.Prodej         => "Prodej",
        TypVydeje.VlastniSpotreba => "Vlastní spotřeba",
        TypVydeje.OdpisZlom      => "Odpis – zlom",
        TypVydeje.OdpisSanitace  => "Odpis – sanitace",
        TypVydeje.OdpisExpirace  => "Odpis – expirace",
        _                        => TypVydeje.ToString()
    };
}

public class VydejkaRadek
{
    public int Id { get; set; }
    public int VydejkaId { get; set; }
    public int SkladovaKartaId { get; set; }

    public string NazevZbozi { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;

    /// <summary>Vždy se ukládá v evidenčních jednotkách.</summary>
    public decimal MnozstviEvidencni { get; set; }

    /// <summary>Informativně – pokud uživatel zadal v baleních.</summary>
    public decimal? PocetBaleniInfo { get; set; }

    /// <summary>Snímek nákupní ceny za EJ v okamžiku výdeje.</summary>
    public decimal? NakupniCenaBezDPH { get; set; }

    /// <summary>Snímek prodejní ceny za EJ s DPH v okamžiku výdeje.</summary>
    public decimal? ProdejniCenaSDPH { get; set; }

    /// <summary>Sazba DPH převzatá z karty v okamžiku výdeje.</summary>
    public decimal SazbaDPH { get; set; } = 21m;

    // ---- nákupní hodnoty ----
    public decimal HodnotaBezDPH => (NakupniCenaBezDPH ?? 0m) * MnozstviEvidencni;
    public decimal HodnotaSDPH => HodnotaBezDPH * (1 + SazbaDPH / 100m);

    // ---- prodejní hodnoty ----
    public decimal HodnotaProdejSDPH => (ProdejniCenaSDPH ?? 0m) * MnozstviEvidencni;
    public decimal HodnotaProdejBezDPH => SazbaDPH > 0
        ? HodnotaProdejSDPH / (1 + SazbaDPH / 100m)
        : HodnotaProdejSDPH;
}
