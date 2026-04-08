# Changelog

Všechny podstatné změny projektu Gastrox. Formát vychází z [Keep a Changelog](https://keepachangelog.com/cs/1.1.0/), verzování podle [SemVer](https://semver.org/lang/cs/).

> **Jak přispívat do tohoto souboru:** Každá úprava, která jde do release, patří pod odpovídající verzi. Před release (`git tag v…`) přesuň rozpracované položky z `## [Unreleased]` pod novou verzi s dnešním datem.

---

## [Unreleased]

---

## [0.4.7] — 2026-04-08

### Změněno
- **Sklad:** finální zarovnání sloupců DataGridu:
  - **Textové sloupce** (Název, Kategorie, EJ) — zarovnány **vlevo** (buňky i záhlaví)
  - **Numerické sloupce** (Stav, Min., Nákup za j., Nákup celkem, Prodej za j., Prodej celkem) — zarovnány **vpravo** (buňky i záhlaví)
  - Vertikální zarovnání: všechny buňky na středu (zachováno z 0.4.6)
- Rozdělený `CellText` styl (TextAlignment=Left, margin 2 vlevo) a `NumericCell` styl (TextAlignment=Right, margin 8 vpravo) pro čistý vizuální dojem.

### Soubory
- `Gastrox/Views/SkladView.xaml`
- `Gastrox/Gastrox.csproj`

---

## [0.4.6] — 2026-04-08

### Opraveno
- **Sklad:** skutečně fungující vertikální zarovnání textových buněk. Předchozí pokus (0.4.5) s implicit `TextBlock` stylem v `DataGrid.Resources` **nefungoval** — WPF generuje buňky `DataGridTextColumn` mimo logical tree DataGridu, takže implicit styly je minou. Správné řešení: pojmenovaný sdílený styl `CellText` aplikovaný přes `ElementStyle` na **každý** `DataGridTextColumn` (Název, Kategorie, EJ i všechny numerické).

### Soubory
- `Gastrox/Views/SkladView.xaml`
- `Gastrox/Gastrox.csproj`

---

## [0.4.5] — 2026-04-08

### Opraveno
- **Sklad:** vertikální zarovnání textových buněk (Název, Kategorie, EJ) — texty padaly nahoru, zatímco numerické sloupce byly na středu. Příčina: WPF default `VerticalAlignment` na `TextBlock` je `Top`, takže `VerticalContentAlignment="Center"` na `DataGridCell` se neprojeví, pokud `TextBlock` uvnitř má vlastní alignment.
- Řešení: přidán **implicit `TextBlock` style** v `DataGrid.Resources` (`VerticalAlignment="Center"` + `TextAlignment="Center"`), který se aplikuje na všechny buňky DataGridu najednou.
- `NumericCell` styl zachován jako prázdný alias s `BasedOn` na implicit styl (kvůli zpětné kompatibilitě sloupců, které ho referencují).

### Přidáno
- **Dokumentace:** `DOKUMENTACE.md` — kompletní popis aplikace (architektura, DB schéma, moduly, build, release, auto-update, dev workflow).
- **Changelog:** `CHANGELOG.md` — chronologický log úprav (zpětně z git historie v0.1.0 → v0.4.4).

### Soubory
- `Gastrox/Views/SkladView.xaml`
- `Gastrox/Gastrox.csproj`
- `DOKUMENTACE.md` (nový)
- `CHANGELOG.md` (nový)

---

## [0.4.4] — 2026-04-08

### Opraveno
- **Sklad:** centrování textů ve sloupcích `DataGrid`. Záhlaví i buňky jsou nyní zarovnané na střed přes `HorizontalContentAlignment` na `DataGridCell` / `DataGridColumnHeader` (místo nespolehlivého `HorizontalAlignment` na `TextBlock`, který v DataGridu kvůli stretch nefunguje).
- Styly `NumericCell` a `NumericHeader` přepnuty z `Right` na `Center`.
- Inline `TextBlock` v headers (Stav, Min., Nákup za j…, Prodej za j…, Nákup celkem, Prodej celkem) přepnuty z `TextAlignment="Right"` na `Center`.

### Soubory
- `Gastrox/Views/SkladView.xaml`
- `Gastrox/Gastrox.csproj` (Version bump)

### Commity
- `fecc636` — fix: centrování textů ve sloupcích DataGridu Skladu (+ bump 0.4.4)

---

## [0.4.3] — 2026-04-08

### Opraveno
- **Sklad:** dvouřádkové pravo-zarovnané hlavičky numerických sloupců.

### Commity
- `5627fcd` — fix: dvouřádkové pravo-zarovnané hlavičky numerických sloupců Skladu

---

## [0.4.2] — 2026-04-08

### Opraveno
- **Sklad:** vertikální zarovnání řádků `DataGrid`.
- **Sklad:** zkrácený nadpis stránky.

### Commity
- `0908d57` — fix: vertikální zarovnání řádků DataGridu Skladu + zkrácený nadpis

---

## [0.4.1] — 2026-04-08

### Přidáno
- **Sklad:** sloupce s cenami (nákupní a prodejní za jednotku i celkem, bez/s DPH) ve výpisu skladových karet.
- **Sklad:** zobrazení hodnoty zásoby.

### Commity
- `6951942` — feat: ceny a hodnoty zásoby ve výpisu skladových karet (+ bump 0.4.1)

---

## [0.4.0] — 2026-04-08

### Přidáno
- **Sklad:** filtr a fulltext hledání karet (název / EAN).
- **Dashboard:** celková hodnota skladových zásob.

### Commity
- `d47a93e` — chore: bump verze na 0.4.0
- `9c747fc` — feat: filtr/hledání karet ve Skladu + hodnota skladových zásob na nástěnce

---

## [0.3.0] — 2026-04-08

### Přidáno
- **Nastavení:** správa kategorií zboží (CRUD, pořadí, aktivace).
- **Sklad:** historie pohybů přímo na kartě zboží.
- **Dashboard:** tlačítko „Nová karta" přímo z nástěnky.
- **Naskladnění:** redesign UI průvodce.

### Commity
- `c0f6dfd` — chore: bump verze na 0.3.0
- `ce1eb3e` — feat: kategorie v Nastavení, pohyby na kartě, nová karta z nástěnky, redesign Naskladnění

---

## [0.2.0] — 2026-04-08

### Přidáno
- **Dashboard** (nástěnka s přehledem).
- **Sklad** (seznam karet + formulář).
- **Průvodci** — Naskladnit, Vyskladnit, Inventura.
- **Nastavení** — firemní údaje, sazby DPH.
- **Auto-update** přes GitHub Releases API (`Services/UpdateService.cs`).

### Commity
- `e0d8b83` — feat(v0.2.0): Dashboard, Sklad, průvodci, Nastavení, auto-update

---

## [0.1.2] — 2026-04-08

### Opraveno
- Tichý crash při startu v `0.1.1` — odstraněna závislost `MaterialDesignThemes` (způsobovala crash na čistém systému bez designeru). Přechod na folder-based publish místo single-file.

### Commity
- `89234af` — fix: tichý crash v0.1.1 – odstraněno MaterialDesign + folder publish

---

## [0.1.1] — 2026-04-08

### Opraveno
- Aplikace nestartovala — `init.sql` zabalen jako **embedded resource** (dřív se hledal jako soubor vedle `.exe`, což po publish selhávalo).
- Přidány top-level error handlery v `Program.cs`.

### Commity
- `f91b386` — fix: aplikace nestartovala – init.sql jako embedded resource + error handlery

---

## [0.1.0] — 2026-04-08

### Přidáno
- **Scaffold projektu** WPF/.NET 8 (Gastrox.sln, MVVM struktura).
- Základní Models, ViewModels, Views, Services, Commands.
- `DatabaseService` — SQLite inicializace.
- SQL schéma (`Database/init.sql`) — SkladovaKarta, Prijemka, Vydejka, Inventura, PohybSkladu, Nastaveni, SazbaDPH, Kategorie.
- **GitHub Actions CI/CD** — `build.yml` (build na push), `release.yml` (release na tag `v*` s Azure Trusted Signing).

### Commity
- `9be8a36` — init: scaffold WPF/.NET 8 skladového software Gastrox
- `8ab01b6` — ci: GitHub Actions build + release s Azure Trusted Signing

---

[Unreleased]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.7...HEAD
[0.4.7]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.6...v0.4.7
[0.4.6]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.5...v0.4.6
[0.4.5]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.4...v0.4.5
[0.4.4]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.3...v0.4.4
[0.4.3]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.2...v0.4.3
[0.4.2]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/HelpTechCZ/gastrox/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/HelpTechCZ/gastrox/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/HelpTechCZ/gastrox/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/HelpTechCZ/gastrox/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/HelpTechCZ/gastrox/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/HelpTechCZ/gastrox/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/HelpTechCZ/gastrox/releases/tag/v0.1.0
