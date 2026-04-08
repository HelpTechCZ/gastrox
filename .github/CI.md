# GitHub Actions – CI/CD pro Gastrox

Stejný pattern jako v **HelpTechCZ/ServiDesk** – Windows runner + **Azure Trusted Signing**.

## Workflow soubory

| Soubor | Spouští se | Co dělá |
|---|---|---|
| `.github/workflows/build.yml`   | push do `main`, PR, manuálně | `dotnet build` + `publish` na Windows runneru, uloží artefakt na 14 dní |
| `.github/workflows/release.yml` | git tag `v*`, manuálně       | publish single-file `.exe`, **podpis Azure Trusted Signing**, ZIP, GitHub Release |

---

## Potřebné GitHub Secrets

Stejné jako v ServiDesku – pokud už jsou nastavené na úrovni organizace `HelpTechCZ`, jsou automaticky dostupné i pro `gastrox`. Jinak je potřeba přidat ručně do **Settings → Secrets and variables → Actions**:

| Secret | Popis |
|---|---|
| `AZURE_TENANT_ID`              | Tenant ID Azure účtu |
| `AZURE_CLIENT_ID`              | App registration client ID |
| `AZURE_CLIENT_SECRET`          | App registration secret |
| `AZURE_ENDPOINT`               | Trusted Signing endpoint URL |
| `AZURE_SIGNING_ACCOUNT`        | Název Trusted Signing účtu |
| `AZURE_CERTIFICATE_PROFILE`    | Název certificate profile |

> Tip: pokud máš secrets na úrovni organizace, zkontroluj v **HelpTechCZ → Settings → Secrets → Actions**, jestli má `gastrox` repo přístup (Repository access → All / Selected).

---

## Vydání nové verze

```bash
git tag v0.1.0
git push origin v0.1.0
```

Workflow **Release** se automaticky spustí:
1. Vytáhne verzi z tagu (`v0.1.0` → `0.1.0`)
2. Nahraje verzi do `Gastrox.csproj` (`<Version>`)
3. `dotnet publish` → single-file portable `.exe` (~80 MB self-contained)
4. **Azure Trusted Signing** podepíše `.exe` (timestamp `http://timestamp.acs.microsoft.com`)
5. ZIP balíček + GitHub Release s `.exe` i ZIPem

### Manuální spuštění bez tagu
**Actions → Release → Run workflow → zadat verzi** (vytvoří jen artefakty, ne release).

---

## Stažení buildu
- **CI build:** Actions → konkrétní run → sekce *Artifacts* dole
- **Release:** záložka **Releases** → `Gastrox-x.y.z-win-x64.zip` nebo přímo `Gastrox.exe`
