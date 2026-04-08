# Gastrox — Kompletní dokumentace

**Portable skladový software pro restauraci.** Windows 11 · .NET 8 · WPF · SQLite · MVVM.

> Aktuální verze: viz `Gastrox/Gastrox.csproj` → `<Version>`
> Repo: <https://github.com/HelpTechCZ/gastrox>
> Autor: HelpTech.cz

---

## Obsah

1. [Účel a cílový uživatel](#1-účel-a-cílový-uživatel)
2. [Technologický stack](#2-technologický-stack)
3. [Architektura (MVVM)](#3-architektura-mvvm)
4. [Struktura projektu](#4-struktura-projektu)
5. [Databázové schéma](#5-databázové-schéma)
6. [Moduly aplikace](#6-moduly-aplikace)
7. [Klíčové koncepty](#7-klíčové-koncepty)
8. [Build a spuštění](#8-build-a-spuštění)
9. [Release a distribuce](#9-release-a-distribuce)
10. [Auto-update](#10-auto-update)
11. [Development workflow](#11-development-workflow)

---

## 1. Účel a cílový uživatel

Desktopová aplikace pro **komplexní správu skladu malé restaurace**. Obsluhuje ji jedna osoba (provozní) — **bez přihlašování uživatelů**, bez rolí, bez víceuživatelského režimu.

**Klíčové požadavky:**
- 100 % portable (žádná instalace, žádný SQL server)
- Databáze se vytvoří automaticky vedle `.exe`
- Zkopírováním složky aplikace → máš i všechna data
- Fluent/Windows 11 vzhled (zaoblené rohy, čisté DataGridy)

---

## 2. Technologický stack

| Vrstva | Technologie |
|---|---|
| **UI** | WPF (XAML), Segoe UI, vlastní styly |
| **Jazyk** | C# 12 |
| **Runtime** | .NET 8 (`net8.0-windows`) |
| **Architektura** | striktně MVVM (žádný code-behind logic) |
| **Databáze** | SQLite (`Microsoft.Data.Sqlite` 8.0.8) |
| **PDF** | QuestPDF 2024.7.3 (Community licence) |
| **CI/CD** | GitHub Actions (`build.yml`, `release.yml`) |
| **Code signing** | Azure Trusted Signing |

---

## 3. Architektura (MVVM)

```
┌─────────────┐        ┌──────────────────┐        ┌─────────────┐
│    View     │◄──────►│    ViewModel     │◄──────►│    Model    │
│  (XAML)     │ binding│ (ObservableObject)│ LINQ   │ (POCO)      │
└─────────────┘        └──────────────────┘        └─────────────┘
                              │
                              ▼
                       ┌──────────────┐
                       │  Services    │
                       │ (DB, Update) │
                       └──────────────┘
```

**Pravidla:**
- **View** — pouze XAML + minimální code-behind (navigace, click handlery volající ViewModel)
- **ViewModel** — dědí z `ViewModelBase` (implementuje `INotifyPropertyChanged`), ovládá stav přes properties, akce přes `RelayCommand`
- **Model** — jednoduché POCO třídy, žádná logika
- **Service** — statické třídy s DB/síťovými operacemi (stateless)
- **Commands** — `RelayCommand` pro MVVM bindovatelné akce
- **Converters** — hodnotové konvertory pro XAML (např. `BoolToVisibilityConverters`)

---

## 4. Struktura projektu

```
Gastrox/
├── Gastrox.sln
├── README.md
├── DOKUMENTACE.md              ← tento dokument
├── CHANGELOG.md                ← log úprav
├── SPECIFIKACE.md              ← původní zadání
├── Database/
│   └── init.sql                ← schéma (embedded resource)
├── .github/workflows/
│   ├── build.yml               ← CI build na každý push
│   └── release.yml             ← release na tag v*
└── Gastrox/
    ├── Gastrox.csproj          ← <Version>, NuGet balíčky
    ├── App.xaml / App.xaml.cs
    ├── Program.cs              ← vlastní entry point (logování chyb)
    ├── MainWindow.xaml(.cs)    ← boční menu + ContentControl
    │
    ├── Models/                 ← POCO třídy (Jedna třída = jedna DB tabulka)
    │   ├── SkladovaKarta.cs
    │   ├── Prijemka.cs
    │   ├── Vydejka.cs
    │   ├── Inventura.cs
    │   ├── PohybSkladu.cs
    │   ├── Kategorie.cs
    │   ├── SazbaDPH.cs
    │   └── Nastaveni.cs
    │
    ├── ViewModels/             ← logika UI + property binding
    │   ├── ViewModelBase.cs            (INotifyPropertyChanged)
    │   ├── DashboardViewModel.cs
    │   ├── SkladViewModel.cs           (seznam karet + formulář)
    │   ├── NaskladnitWizardViewModel.cs
    │   ├── VydejkaWizardViewModel.cs
    │   ├── InventuraWizardViewModel.cs
    │   ├── NastaveniViewModel.cs
    │   ├── PrijemkaViewModel.cs
    │   ├── PrijemkaRadekViewModel.cs   ★ jádro přepočtu balení→EJ
    │   └── VydejkaRadekViewModel.cs
    │
    ├── Views/                  ← XAML
    │   ├── DashboardView.xaml
    │   ├── SkladView.xaml
    │   ├── NaskladnitWizardView.xaml
    │   ├── VydejkaWizardView.xaml
    │   ├── InventuraWizardView.xaml
    │   ├── NastaveniView.xaml
    │   └── PrijemkaView.xaml
    │
    ├── Services/
    │   ├── DatabaseService.cs   ← SQLite wrapper, Initialize, CRUD
    │   └── UpdateService.cs     ← GitHub Releases API polling
    │
    ├── Commands/
    │   └── RelayCommand.cs      ← ICommand implementace
    │
    └── Converters/
        └── BoolToVisibilityConverters.cs
```

---

## 5. Databázové schéma

**Soubor:** `sklad.db` (SQLite, WAL journal mode)
**Umístění:** vedle `Gastrox.exe`
**Init skript:** `Database/init.sql` zabalený jako **embedded resource** (`Gastrox.Database.init.sql`) — aby byla aplikace 100 % portable jako single `.exe`

### Tabulky

| Tabulka | Účel |
|---|---|
| `SkladovaKarta` | Katalog zboží + aktuální stav |
| `Kategorie` | Uživatelsky spravované kategorie (Tvrdý alkohol, Pivo, …) |
| `SazbaDPH` | Ceníkové sazby DPH (21, 12, 0) |
| `Prijemka` / `PrijemkaRadek` | Hlavička + řádky příjemky |
| `Vydejka` / `VydejkaRadek` | Hlavička + řádky výdejky (typy: Prodej, Vlastní spotřeba, Odpis-*) |
| `Inventura` / `InventuraRadek` | Fyzická inventura + rozdíly |
| `Uzaverka` / `UzaverkaRadek` | Snapshoty stavů (denní/týdenní/měsíční/roční) |
| `PohybSkladu` | **Auditní deník** všech změn skladu (+/−) |
| `Nastaveni` | Key/value — firemní údaje, config |

### Referenční integrita

- `PrijemkaRadek.Prijemka_Id` → `Prijemka.Id` **ON DELETE CASCADE**
- `PrijemkaRadek.SkladovaKarta_Id` → `SkladovaKarta.Id` **ON DELETE RESTRICT**
- Stejné principy pro `VydejkaRadek`, `InventuraRadek`, `UzaverkaRadek`, `PohybSkladu`

**Pragma:** `foreign_keys = ON`, `journal_mode = WAL`

### Klíčová pole `SkladovaKarta`

| Pole | Typ | Účel |
|---|---|---|
| `Evidencni_Jednotka` | TEXT | Litr / Kg / Kus — jednotka pro reporting |
| `Typ_Baleni` | TEXT | „Láhev 0,7l", „Sud 50l" — lidsky čitelný popis |
| `Koeficient_Prepoctu` | REAL | 1 balení × koef = evidenční jednotky |
| `Aktualni_Stav_Evidencni` | REAL | Aktuální stav v EJ (upraveno automaticky při každém pohybu) |
| `Je_Aktivni` | INTEGER | 0/1 — deaktivace místo DELETE (zachová historii) |

---

## 6. Moduly aplikace

Boční menu `MainWindow` → `ContentControl` přepínající UserControly:

### 🏠 Dashboard (`DashboardView`)
- Widget **„Zboží pod minimálním limitem"**
- Rychlý přehled pohybů za aktuální týden
- Celková hodnota skladových zásob (nákupní + prodejní)
- Tlačítko „+ Nová karta" přímo z nástěnky

### 📦 Sklad (`SkladView`)
- Levá strana — `DataGrid` s aktivními kartami
  - Fulltext hledání (název / EAN)
  - Filtr podle kategorie
  - Sloupce: Název, Kategorie, Stav, EJ, Min., Nákup za j., Nákup celkem, Prodej za j., Prodej celkem (vše bez/s DPH)
- Pravá strana — formulář karty (nová / editace)
  - Název, Kategorie, EAN, EJ, Typ balení, Koeficient
  - Ceny (sazba DPH, nákupní bez DPH, prodejní s DPH) + **automatický výpočet marže**
  - Minimální stav, Dodavatel
  - Tlačítka **Uložit** / **Deaktivovat** (soft delete)
  - **Historie pohybů** karty

### 📥 Naskladnit (`NaskladnitWizardView`)
- Wizard pro vytvoření příjemky
- Hlavička: číslo dokladu (auto), datum, dodavatel, číslo faktury
- Řádky: našeptávač zboží, počet balení, automatický přepočet na EJ
- Možnost upravit nákupní cenu → aktualizuje se cena na kartě

### 📤 Vyskladnit (`VydejkaWizardView`)
- Wizard pro vytvoření výdejky
- Typ: **Prodej / Vlastní spotřeba / Odpis-Zlom / Odpis-Sanitace / Odpis-Expirace**
- Středisko: **Bar / Kuchyně**
- Zadávání v baleních i přímo v EJ
- **Ochrana proti minusovému skladu**

### 📋 Inventura (`InventuraWizardView`)
- Vytvoření inventury k datu → seznam aktivních položek
- Zadání fyzického stavu
- Výpočet **rozdílu** (manko/přebytek)
- Uzávěrka inventury → zápis do `InventuraRadek` + korekční `PohybSkladu`

### ⚙ Nastavení (`NastaveniView`)
- Firemní údaje (na PDF hlavičky)
- Správa kategorií (CRUD + pořadí + aktivace)
- Sazby DPH
- Info o verzi + **Kontrola aktualizací**

---

## 7. Klíčové koncepty

### Přepočet balení → evidenční jednotky

**Jádro:** `ViewModels/PrijemkaRadekViewModel.cs` + `Views/PrijemkaView.xaml`

Karta má `Koeficient_Prepoctu` (např. Jack Daniels = 0,7 — protože 1 láhev = 0,7 litru EJ).

Při zápisu příjemky:
```
Vyberu "Jack Daniels" (koef. 0,7)
Zadám počet balení: 5
→ ZobrazeniPrepoctu ihned zobrazí: "5 × 0,7 l = 3,5 l"
→ Mnozstvi_Evidencni = 3.5
```

**Jak to technicky funguje:** `PocetBaleni` a `VybraneZbozi` jsou bindable properties; při změně vyvolají `OnPropertyChanged` pro závislé read-only properties (`MnozstviEvidencni`, `ZobrazeniPrepoctu`, `CelkemBezDPH`). `DataGrid` používá `UpdateSourceTrigger=PropertyChanged` → uživatel vidí výsledek live při psaní.

### Transakční zápis příjemky

`DatabaseService.SavePrijemka()` běží v jedné SQLite transakci:

1. `INSERT INTO Prijemka` (hlavička)
2. `INSERT INTO PrijemkaRadek` (všechny řádky — se **snímkem koeficientu** v době naskladnění)
3. `UPDATE SkladovaKarta SET Aktualni_Stav_Evidencni = Aktualni_Stav_Evidencni + :prirustek`
4. `INSERT INTO PohybSkladu` (auditní stopa)

Pokud kterýkoli krok selže → rollback celé transakce.

### Auditní deník (`PohybSkladu`)

**Každá změna stavu** na skladě zapisuje řádek do `PohybSkladu`:
- `+` pro příjem
- `−` pro výdej
- `±` pro inventurní korekci

Součet všech pohybů pro danou kartu = `Aktualni_Stav_Evidencni`. Slouží jako **sanity check** a zdroj historie v Sklad detailu.

### Soft delete

Karty se nikdy fyzicky nemažou — pouze `Je_Aktivni = 0`. Důvod: zachování FK integrity historických dokladů a reporting odpisů.

---

## 8. Build a spuštění

### Předpoklady (dev)
- Windows 10/11
- Visual Studio 2022 Community ≥ 17.8 (workload **.NET desktop development**)
- Nebo jen .NET 8 SDK + jakýkoli editor

### Otevření ve VS
1. Dvojklik `Gastrox.sln`
2. VS obnoví NuGet balíčky automaticky
3. `Ctrl+Shift+B` — Build Solution
4. `F5` — spuštění s debugem

### Build z terminálu (PowerShell)

```powershell
dotnet restore Gastrox.sln
dotnet build Gastrox.sln -c Debug
dotnet run --project Gastrox/Gastrox.csproj
```

### Publish pro distribuci (self-contained portable)

```powershell
dotnet publish Gastrox/Gastrox.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  -p:PublishSingleFile=false `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o publish
```

Výstup: složka `publish/` obsahuje `Gastrox.exe` + runtime. Zkopíruj celou složku na cílový PC — DB se vytvoří při prvním spuštění.

### První spuštění
- Aplikace si vedle `.exe` založí `sklad.db`
- Init script (`Gastrox.Database.init.sql`) se načte z embedded resource a vytvoří tabulky
- Vloží se defaultní sazby DPH (21/12/0) a kategorie (Tvrdý alkohol, Pivo, Víno, …)

---

## 9. Release a distribuce

### Workflow
**Soubor:** `.github/workflows/release.yml`
**Trigger:** push tagu `v*` (např. `v0.4.4`) **nebo** manuální `workflow_dispatch`

**Kroky:**
1. Setup .NET 8
2. Extrahovat verzi z tagu (`v0.4.4` → `0.4.4`)
3. Přepsat `<Version>` v `Gastrox.csproj` podle tagu
4. `dotnet publish` (self-contained, win-x64)
5. **Code signing** přes Azure Trusted Signing (vyžaduje secrets: `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_ENDPOINT`, `AZURE_SIGNING_ACCOUNT`, `AZURE_CERTIFICATE_PROFILE`)
6. Zazipovat publish složku → `Gastrox-{VERSION}-win-x64.zip`
7. Vytvořit **GitHub Release** s auto-generated release notes + přiložit ZIP

### Release proces (postup)

```bash
# 1. Upravit Version v Gastrox/Gastrox.csproj (bump patch)
# 2. Commit + push
git add -A
git commit -m "fix: popis změny (+ bump 0.4.4)"
git push

# 3. Tag + push
git tag v0.4.4
git push origin v0.4.4

# 4. Sledovat workflow
gh run list --workflow=release.yml --limit 3
```

### Verzování
**SemVer: `MAJOR.MINOR.PATCH`**
- **MAJOR** — breaking změny DB schématu nebo API
- **MINOR** — nové funkce / modul
- **PATCH** — bugfixy, UI drobnosti

Verze se udržuje **na jednom místě**: `Gastrox/Gastrox.csproj` → `<Version>`. Release workflow ji při buildu přepíše podle tagu.

---

## 10. Auto-update

**Služba:** `Services/UpdateService.cs`
**Endpoint:** `https://api.github.com/repos/HelpTechCZ/gastrox/releases/latest`

### Jak to funguje
1. Při startu (nebo přes **Nastavení → Zkontrolovat aktualizace**) aplikace zavolá GitHub Releases API
2. Porovná `tag_name` releasu s `CurrentVersion` (načteno z `Assembly.GetExecutingAssembly().GetName().Version`)
3. Pokud je release novější → zobrazí dialog s release notes
4. Uživatel klikne „Stáhnout" → otevře se download URL nebo stáhne ZIP a nabídne rozbalení

### Semver porovnání
`UpdateService.IsNewer(remote, local)` — porovnává major/minor/patch po složkách.

---

## 11. Development workflow

### Branch strategie
**Trunk-based:** všechno do `main`. Release přes tagy, ne přes branch.

### Conventional commits (volně)
```
feat: nový feature
fix: bugfix
chore: údržba, bump verze, deps
ci: změny v GitHub Actions
docs: dokumentace
```

### Commit + bump + release (česká notace)
```bash
git add <files>
git commit -m "fix: popis (+ bump 0.4.4)"
git push
git tag v0.4.4
git push origin v0.4.4
```

### Styl kódu
- **Namespace:** file-scoped (`namespace Gastrox.ViewModels;`)
- **Nullable:** `<Nullable>enable</Nullable>` v csproj
- **Implicitní usings:** povoleno
- **XAML:** 4-space indent, atributy per-line u delších tagů
- **CZ komentáře** — kód je pro českého zákazníka/operátora
- **České názvy tříd** (`SkladovaKarta`, `Vydejka`) — jednotné s DB tabulkami

### Testování
Projekt nemá unit testy — primární kontrola je **manuální smoke test** na Windows po build:
1. Zapnout aplikaci
2. Vytvořit kartu → naskladnit → vyskladnit → inventura
3. Zkontrolovat `PohybSkladu` v DB
4. Export PDF

### Debug tipy
- **DB inspekce:** DB Browser for SQLite nad `sklad.db`
- **Crash logy:** Program.cs má top-level error handler, chyby se zapisují vedle `.exe` do `crash.log`
- **XAML designer:** pozor na `x:Type` odkazy na third-party assembly (může crashnout designer, ne runtime)

---

## Kontakty

- **Repo:** <https://github.com/HelpTechCZ/gastrox>
- **Issues:** <https://github.com/HelpTechCZ/gastrox/issues>
- **Autor:** HelpTech.cz

---

> Tento dokument je živý. Při každé podstatné změně architektury / DB schématu / build procesu ho aktualizuj. Chronologický log úprav viz [`CHANGELOG.md`](./CHANGELOG.md).
