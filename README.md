# Gastrox

Portable skladový software pro restauraci. **Windows 11 / .NET 8 / WPF / SQLite / MVVM.**

> Aplikace je plně přenosná: stačí zkopírovat složku s `.exe` na jiný počítač s .NET 8 Desktop Runtime. Databáze `sklad.db` se vytvoří automaticky vedle spustitelného souboru.

---

## Struktura projektu

```
Gastrox/
├── Gastrox.sln
├── Database/
│   └── init.sql                    # CREATE TABLE skript (vytvoří se při prvním spuštění)
├── Gastrox/
│   ├── Gastrox.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs       # boční menu + ContentControl
│   ├── Models/
│   │   ├── SkladovaKarta.cs
│   │   ├── Prijemka.cs (+ PrijemkaRadek)
│   │   └── Vydejka.cs (+ enums TypVydeje, Stredisko)
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   ├── PrijemkaViewModel.cs    # logika hlavičky + součtů
│   │   └── PrijemkaRadekViewModel.cs   # ★ JÁDRO: live přepočet balení → evid. jednotky
│   ├── Views/
│   │   └── PrijemkaView.xaml       # ★ XAML s automatickým přepočtem
│   ├── Services/
│   │   └── DatabaseService.cs      # init DB + uložení příjemky v transakci
│   └── Commands/
│       └── RelayCommand.cs
├── SPECIFIKACE.md
└── .gitignore
```

---

## NuGet balíčky (uvedené v `Gastrox.csproj`)

| Balíček                     | Verze     | K čemu |
|---|---|---|
| `Microsoft.Data.Sqlite`     | 8.0.8     | SQLite klient bez nutnosti instalace serveru |
| `QuestPDF`                  | 2024.7.3  | Generování PDF reportů (Community licence) |
| `CommunityToolkit.Mvvm`     | 8.3.2     | (volitelně) `ObservableObject`, `RelayCommand` |
| `MaterialDesignThemes`      | 5.1.0     | Fluent / Material UI styl pro Windows 11 vzhled |

---

## Postup kompilace ve Visual Studio Community (krok za krokem)

### 1. Předpoklady
- **Windows 10/11**
- **Visual Studio 2022 Community** (verze ≥ 17.8) — zdarma na <https://visualstudio.microsoft.com/cs/vs/community/>
- Při instalaci VS zaškrtnout workload **„.NET desktop development"** (obsahuje WPF i .NET 8 SDK)

### 2. Otevření projektu
1. V Průzkumníku otevři složku `Gastrox` a dvojklikem spusť `Gastrox.sln`.
2. VS automaticky obnoví NuGet balíčky (`Microsoft.Data.Sqlite`, `QuestPDF`, …).
   - Pokud ne: pravým tlačítkem na Solution → **Restore NuGet Packages**.

### 3. První build
1. Nahoře nastav `Debug | Any CPU`.
2. **Build → Build Solution** (`Ctrl+Shift+B`).
3. Spustit lze přes **F5** (debug) nebo `Ctrl+F5` (bez debugu).

### 4. První spuštění
- Aplikace si vedle `Gastrox.exe` automaticky založí soubor `sklad.db` (SQLite).
- Init skript běží z `Database/init.sql`, který je nastavený jako `CopyToOutputDirectory=PreserveNewest`.

### 5. Build pro distribuci (Portable .exe)
V terminálu (PowerShell) ve složce `Gastrox/Gastrox`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Výstup najdeš v `Gastrox\bin\Release\net8.0-windows\win-x64\publish\`.
Stačí zkopírovat celou složku publish na cílový stroj — DB se vytvoří při prvním spuštění.

> Pokud nechceš závislost na .NET runtime na cílovém PC, použij `--self-contained true`.

---

## ★ Jak funguje automatický přepočet jednotek

Jádro je ve dvou souborech:

- **`ViewModels/PrijemkaRadekViewModel.cs`** — drží `PocetBaleni` a propojené read-only vlastnosti `MnozstviEvidencni`, `ZobrazeniPrepoctu`, `CelkemBezDPH`. Při změně `PocetBaleni` nebo `VybraneZbozi` se přes `OnPropertyChanged` automaticky aktualizují všechny závislé hodnoty.
- **`Views/PrijemkaView.xaml`** — sloupec **„Přepočet"** v `DataGrid` je svázaný s `ZobrazeniPrepoctu` a používá `UpdateSourceTrigger=PropertyChanged` → uživatel vidí výsledek přímo při psaní.

Příklad:
```
Vyberu „Jack Daniels" (koef. 0,7)
Zadám počet balení: 5
→ Sloupec Přepočet okamžitě zobrazí: "5 × 0,7 l = 3,5 l"
```

Při uložení (`UlozitCommand` → `DatabaseService.SavePrijemka`) běží vše v jedné SQLite transakci:
1. INSERT do `Prijemka`
2. INSERT všech řádků do `PrijemkaRadek`
3. UPDATE `Aktualni_Stav_Evidencni` na kartě (přičte se přírůstek)
4. INSERT do `PohybSkladu` (auditní stopa)
