using System;
using System.Collections.Generic;

namespace Gastrox.Models;

public class Prijemka
{
    public int Id { get; set; }
    public string CisloDokladu { get; set; } = string.Empty;
    public DateTime DatumPrijeti { get; set; } = DateTime.Now;
    public string? Dodavatel { get; set; }
    public string? CisloFaktury { get; set; }
    public string? Poznamka { get; set; }

    /// <summary>Cílový sklad pro tuto příjemku.</summary>
    public int SkladId { get; set; }

    /// <summary>Zobrazovací název skladu (join).</summary>
    public string SkladNazev { get; set; } = string.Empty;

    public decimal CelkemBezDPH { get; set; }
    public decimal CelkemSDPH { get; set; }

    public List<PrijemkaRadek> Radky { get; set; } = new();
}

public class PrijemkaRadek
{
    public int Id { get; set; }
    public int PrijemkaId { get; set; }

    public int SkladovaKartaId { get; set; }
    public string NazevZbozi { get; set; } = string.Empty;   // snímek názvu pro zobrazení
    public string TypBaleni { get; set; } = string.Empty;
    public string EvidencniJednotka { get; set; } = string.Empty;

    /// <summary>Počet balení zadaný uživatelem (např. 5 lahví).</summary>
    public decimal PocetBaleni { get; set; }

    /// <summary>Snímek koeficientu v okamžiku příjmu.</summary>
    public decimal KoeficientPrepoctu { get; set; } = 1m;

    /// <summary>Automaticky spočítaná hodnota v evidenčních jednotkách.</summary>
    public decimal MnozstviEvidencni => PocetBaleni * KoeficientPrepoctu;

    /// <summary>Nákupní cena za 1 balení, bez DPH.</summary>
    public decimal NakupniCenaBezDPH { get; set; }
    public decimal SazbaDPH { get; set; } = 21m;

    public decimal CelkemBezDPH => PocetBaleni * NakupniCenaBezDPH;
    public decimal CelkemSDPH   => CelkemBezDPH * (1 + SazbaDPH / 100m);
}
