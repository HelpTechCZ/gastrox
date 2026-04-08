using System;

namespace Gastrox.Models;

/// <summary>
/// Skladová karta zboží – základní evidenční jednotka aplikace.
/// Jedna položka v databázi = jedna reálná skladová karta.
/// </summary>
public class SkladovaKarta
{
    public int Id { get; set; }

    public string Nazev { get; set; } = string.Empty;

    /// <summary>Tvrdý alkohol, Pivo, Víno, Maso, Nealko, …</summary>
    public string Kategorie { get; set; } = string.Empty;

    /// <summary>Volitelně – pro vyhledávání čtečkou.</summary>
    public string? EAN { get; set; }

    /// <summary>Jednotka, ve které se vede sklad (Litr, Kg, Kus).</summary>
    public string EvidencniJednotka { get; set; } = "Kus";

    /// <summary>Textový popis balení (Láhev 0,7l; Sud 50l; Karton 24 ks).</summary>
    public string TypBaleni { get; set; } = string.Empty;

    /// <summary>
    /// Koeficient přepočtu: 1 balení × koeficient = počet evidenčních jednotek.
    /// Příklad: láhev 0,7 l → koeficient 0,7; sud 50 l → 50; karton 24 ks → 24.
    /// </summary>
    public decimal KoeficientPrepoctu { get; set; } = 1m;

    /// <summary>Aktuální stav v evidenčních jednotkách (litry / kg / kusy).</summary>
    public decimal AktualniStavEvidencni { get; set; }

    public decimal NakupniCenaBezDPH { get; set; }
    public decimal SazbaDPH { get; set; } = 21m;
    public decimal ProdejniCenaSDPH { get; set; }

    /// <summary>Limit pro upozornění "dojde – objednat".</summary>
    public decimal MinimalniStav { get; set; }

    public string? Dodavatel { get; set; }

    /// <summary>Místo mazání se karta pouze deaktivuje – zůstává v historii.</summary>
    public bool JeAktivni { get; set; } = true;

    public DateTime? DatumPosledniInventury { get; set; }
    public DateTime? DatumPoslednihoNaskladneni { get; set; }

    // ---- vypočítané vlastnosti ----

    /// <summary>True pokud stav klesl na/pod minimální limit (dashboard widget).</summary>
    public bool JePodLimitem => AktualniStavEvidencni <= MinimalniStav;

    /// <summary>
    /// Přepočet evidenčního stavu na počet balení (informativně).
    /// Např. 3,5 l / 0,7 = 5 lahví.
    /// </summary>
    public decimal AktualniStavVBaleni
        => KoeficientPrepoctu > 0 ? AktualniStavEvidencni / KoeficientPrepoctu : 0m;

    /// <summary>Nákupní cena včetně DPH (vypočteno ze sazby).</summary>
    public decimal NakupniCenaSDPH => NakupniCenaBezDPH * (1 + SazbaDPH / 100m);

    /// <summary>Prodejní cena bez DPH (vypočteno ze sazby).</summary>
    public decimal ProdejniCenaBezDPH
        => SazbaDPH > 0 ? ProdejniCenaSDPH / (1 + SazbaDPH / 100m) : ProdejniCenaSDPH;

    // ---- ceny a hodnoty na úrovni evidenční jednotky (litr/kg/kus) ----

    /// <summary>Nákupní cena za 1 evidenční jednotku bez DPH (cena_za_balení / koeficient).</summary>
    public decimal NakupniCenaZaJednotkuBezDPH
        => KoeficientPrepoctu > 0 ? NakupniCenaBezDPH / KoeficientPrepoctu : 0m;

    /// <summary>Prodejní cena za 1 evidenční jednotku včetně DPH.</summary>
    public decimal ProdejniCenaZaJednotkuSDPH
        => KoeficientPrepoctu > 0 ? ProdejniCenaSDPH / KoeficientPrepoctu : 0m;

    /// <summary>Celková hodnota zásoby této karty v nákupní ceně bez DPH.</summary>
    public decimal HodnotaNakupBezDPH => AktualniStavEvidencni * NakupniCenaZaJednotkuBezDPH;

    /// <summary>Celková hodnota zásoby této karty v prodejní ceně s DPH.</summary>
    public decimal HodnotaProdejSDPH => AktualniStavEvidencni * ProdejniCenaZaJednotkuSDPH;

    /// <summary>Marže v Kč na evidenční jednotku (prodej − nákup, oboje bez DPH).</summary>
    public decimal MarzeKc => ProdejniCenaBezDPH - NakupniCenaBezDPH;

    /// <summary>Marže v procentech ze cenového rozdílu (z prodejní ceny bez DPH).</summary>
    public decimal MarzeProcent
        => ProdejniCenaBezDPH > 0 ? Math.Round((MarzeKc / ProdejniCenaBezDPH) * 100m, 1) : 0m;
}
