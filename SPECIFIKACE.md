# Gastrox – Skladový software pro provoz restaurace

## Role a cíl
Portable desktopová aplikace pro Windows 11 pro komplexní správu skladu malé restaurace. Aplikaci obsluhuje výhradně jedna osoba (provozní), bez přihlašování uživatelů.

## 1. Architektura a technologický stack
- **Aplikace:** C# WPF, architektura striktně MVVM (Model-View-ViewModel)
- **Framework:** .NET 8
- **Databáze:** SQLite, soubor `sklad.db` vytvářen dynamicky vedle `.exe`
- **Portable:** žádná instalace, žádný SQL server
- **Design:** Windows 11 Fluent Design (zaoblené rohy, čisté DataGridy, velká tlačítka)

## 2. Databázový model – Skladová karta

| Pole | Typ | Popis |
|---|---|---|
| Id | PK | |
| Nazev | string | |
| Kategorie | string | Tvrdý alkohol, Pivo, Víno, Maso, Nealko |
| EAN | string (opt.) | Pro vyhledávání |
| Evidencni_Jednotka | string | Litr, Kg, Kus |
| Typ_Baleni | string | Láhev 0,7l, Sud 50l |
| Koeficient_Prepoctu | decimal | 1 balení × koef. = evid. jednotky |
| Aktualni_Stav_Evidencni | decimal | Automatický stav |
| Nakupni_Cena_Bez_DPH | decimal | |
| Sazba_DPH | decimal | |
| Prodejni_Cena_S_DPH | decimal | |
| Minimalni_Stav | decimal | Limit pro notifikaci |
| Dodavatel | string | |
| Je_Aktivni | bool | Místo mazání se deaktivuje |
| Datum_Posledni_Inventury | DateTime | |
| Datum_Posledniho_Naskladneni | DateTime | |

## 3. Moduly

### A) PŘÍJEMKA
- **Hlavička:** Číslo dokladu (auto), datum, dodavatel, číslo faktury
- **Řádky:** našeptávač zboží, zadání počtu balení, automatický přepočet na evidenční jednotky
- Možnost upravit nákupní cenu – aktualizuje se karta

### B) VÝDEJKA
- **Hlavička:** číslo, datum, středisko (Bar, Kuchyně)
- **Typy výdeje:** Prodej, Vlastní spotřeba, Odpis-Zlom, Odpis-Sanitace, Odpis-Expirace
- Zadání v baleních i přímo v jednotkách
- Ochrana proti minusovému skladu

### C) INVENTURY A UZÁVĚRKY
- Vytvoření inventury k datu → seznam aktivních položek
- Zadání fyzického stavu (balení + zbytky)
- Zobrazení rozdílu (manko/přebytek)
- Uzávěrky: denní, týdenní, měsíční, roční

## 4. UI / Dashboard
- Widget "Zboží pod minimálním limitem"
- Graf pohybů za aktuální týden
- DataGridy s fulltextem a filtry

## 5. Reporting a exporty
- PDF: příjemka, výdejka, inventurní protokol, seznam k objednání (QuestPDF)
- XML export stavu skladu pro účetnictví
