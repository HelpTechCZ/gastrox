using System;
using System.Collections.Generic;

namespace Gastrox.Models;

/// <summary>
/// DTO záznamy pro JSON serializaci stavu wizardů do tabulky Rozpracovano.
/// Ukládá se pouze ID odkazů (zboží, sklad, balení, sazba) – při načtení
/// se znovu nahydratují z DB, aby data byla vždy aktuální.
/// </summary>

// ---- Příjemka ----

public record PrijemkaDraftDto(
    int Krok,
    string CisloDokladu,
    DateTime DatumPrijeti,
    int? SkladId,
    string? Dodavatel,
    string? CisloFaktury,
    string? Poznamka,
    List<PrijemkaRadekDraftDto> Radky
);

public record PrijemkaRadekDraftDto(
    int? ZboziId,
    int? BaleniId,
    decimal PocetBaleni,
    decimal NakupniCenaBezDPH,
    decimal? SazbaDPH
);

// ---- Výdejka ----

public record VydejkaDraftDto(
    int Krok,
    int? SkladId,
    string TypVydeje,
    string Stredisko,
    string? Poznamka,
    List<VydejkaRadekDraftDto> Radky
);

public record VydejkaRadekDraftDto(
    int? ZboziId,
    decimal MnozstviEvidencni
);

// ---- Převodka ----

public record PrevodkaDraftDto(
    int Krok,
    string CisloDokladu,
    DateTime DatumPrevodu,
    int? SkladZdrojId,
    int? SkladCilId,
    string? Poznamka,
    List<PrevodkaRadekDraftDto> Radky
);

public record PrevodkaRadekDraftDto(
    int? ZboziId,
    decimal MnozstviEvidencni
);
