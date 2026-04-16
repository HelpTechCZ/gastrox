using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
using Gastrox.Models;
using Gastrox.Services;
using Gastrox.Views;
using Microsoft.Win32;

namespace Gastrox.ViewModels;

/// <summary>
/// Globální nastavení – firma (hlavička reportů), sazby DPH a aktualizace.
/// </summary>
public class NastaveniViewModel : ViewModelBase
{
    // ---------- Firma ----------
    private string? _firmaNazev;
    private string? _firmaIco;
    private string? _firmaDic;
    private string? _firmaUlice;
    private string? _firmaMesto;
    private string? _firmaPsc;
    private string? _firmaStat = "ČR";
    private string? _firmaEmail;
    private string? _firmaTelefon;

    public string? FirmaNazev   { get => _firmaNazev;   set => SetProperty(ref _firmaNazev, value); }
    public string? FirmaIco     { get => _firmaIco;     set => SetProperty(ref _firmaIco, value); }
    public string? FirmaDic     { get => _firmaDic;     set => SetProperty(ref _firmaDic, value); }
    public string? FirmaUlice   { get => _firmaUlice;   set => SetProperty(ref _firmaUlice, value); }
    public string? FirmaMesto   { get => _firmaMesto;   set => SetProperty(ref _firmaMesto, value); }
    public string? FirmaPsc     { get => _firmaPsc;     set => SetProperty(ref _firmaPsc, value); }
    public string? FirmaStat    { get => _firmaStat;    set => SetProperty(ref _firmaStat, value); }
    public string? FirmaEmail   { get => _firmaEmail;   set => SetProperty(ref _firmaEmail, value); }
    public string? FirmaTelefon { get => _firmaTelefon; set => SetProperty(ref _firmaTelefon, value); }

    // ---------- Sazby DPH ----------
    public ObservableCollection<SazbaDPH> Sazby { get; } = new();

    private SazbaDPH? _vybranaSazba;
    public SazbaDPH? VybranaSazba { get => _vybranaSazba; set => SetProperty(ref _vybranaSazba, value); }

    private decimal _novaSazba = 21m;
    public decimal NovaSazba { get => _novaSazba; set => SetProperty(ref _novaSazba, value); }

    private string _novaPopis = string.Empty;
    public string NovaPopis { get => _novaPopis; set => SetProperty(ref _novaPopis, value); }

    private bool _novaJeVychozi;
    public bool NovaJeVychozi { get => _novaJeVychozi; set => SetProperty(ref _novaJeVychozi, value); }

    // ---------- Kategorie ----------
    public ObservableCollection<Kategorie> Kategorie { get; } = new();

    private Kategorie? _vybranaKategorie;
    public Kategorie? VybranaKategorie { get => _vybranaKategorie; set => SetProperty(ref _vybranaKategorie, value); }

    private string _novaKategorieNazev = string.Empty;
    public string NovaKategorieNazev { get => _novaKategorieNazev; set => SetProperty(ref _novaKategorieNazev, value); }

    private int _novaKategoriePoradi;
    public int NovaKategoriePoradi { get => _novaKategoriePoradi; set => SetProperty(ref _novaKategoriePoradi, value); }

    // ---------- Sklady ----------
    public ObservableCollection<Sklad> Sklady { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad { get => _vybranySklad; set => SetProperty(ref _vybranySklad, value); }

    private string _novySkladNazev = string.Empty;
    public string NovySkladNazev { get => _novySkladNazev; set => SetProperty(ref _novySkladNazev, value); }

    private bool _novySkladVychozi;
    public bool NovySkladVychozi { get => _novySkladVychozi; set => SetProperty(ref _novySkladVychozi, value); }

    // ---------- Licence ----------
    private string _licenseKey = string.Empty;
    public string LicenseKey { get => _licenseKey; set => SetProperty(ref _licenseKey, value); }

    public string LicenceStav => LicenseService.IsLicensed
        ? "Licence aktivní"
        : "DEMO verze";

    public string LicenceDetail
    {
        get
        {
            var c = LicenseService.Current;
            if (c is null || !c.IsValid) return "Zadejte licenční klíč pro plnou verzi.";
            var typ = c.Type == "yearly" ? "roční" : "měsíční";
            var exp = DateTime.TryParse(c.Expires, out var d) ? d.ToString("d.M.yyyy") : "—";
            return $"Zákazník: {c.Customer} | Typ: {typ} | Vyprší: {exp}";
        }
    }

    public bool MaLicenciDetail => true;

    public string LicenceBarva => LicenseService.IsLicensed ? "#E6F4EA" : "#FFF4E5";

    // ---------- Aktualizace ----------
    private bool _autoCheck = true;
    public bool AutoCheck { get => _autoCheck; set => SetProperty(ref _autoCheck, value); }

    public string AktualniVerze => "Verze aplikace: " + UpdateService.CurrentVersion;

    private string _statusAktualizace = "Klikněte na 'Zkontrolovat nyní' pro ověření.";
    public string StatusAktualizace { get => _statusAktualizace; set => SetProperty(ref _statusAktualizace, value); }

    // ---------- Commands ----------
    public ICommand UlozitFirmuCommand { get; }
    public ICommand PridatSazbuCommand { get; }
    public ICommand DeaktivovatSazbuCommand { get; }
    public ICommand PridatKategoriiCommand { get; }
    public ICommand DeaktivovatKategoriiCommand { get; }
    public ICommand UlozitAktualizaceCommand { get; }
    public ICommand ZkontrolovatNynicommand { get; }
    public ICommand ExportZalohyCommand { get; }
    public ICommand ImportZalohyCommand { get; }
    public ICommand AktivovatLicenciCommand { get; }
    public ICommand OdebratLicenciCommand { get; }
    public ICommand PridatSkladCommand { get; }
    public ICommand DeaktivovatSkladCommand { get; }
    public ICommand NastavitVychoziSkladCommand { get; }
    public ICommand PrejmenovatSkladCommand { get; }

    public NastaveniViewModel()
    {
        Nacti();

        UlozitFirmuCommand = new RelayCommand(_ => UlozFirmu());
        PridatSazbuCommand = new RelayCommand(_ => PridejSazbu(),
            _ => !string.IsNullOrWhiteSpace(NovaPopis));
        DeaktivovatSazbuCommand = new RelayCommand(_ => DeaktivujSazbu(),
            _ => VybranaSazba is not null);
        PridatKategoriiCommand = new RelayCommand(_ => PridejKategorii(),
            _ => !string.IsNullOrWhiteSpace(NovaKategorieNazev));
        DeaktivovatKategoriiCommand = new RelayCommand(_ => DeaktivujKategorii(),
            _ => VybranaKategorie is not null);
        UlozitAktualizaceCommand = new RelayCommand(_ => UlozAutoCheck());
        ZkontrolovatNynicommand = new RelayCommand(async _ => await ZkontrolovatNyniAsync());
        ExportZalohyCommand = new RelayCommand(_ => ExportZalohu());
        ImportZalohyCommand = new RelayCommand(_ => ImportZalohu());
        AktivovatLicenciCommand = new RelayCommand(async _ => await AktivovatLicenciAsync(),
            _ => !string.IsNullOrWhiteSpace(LicenseKey));
        OdebratLicenciCommand = new RelayCommand(_ => OdebratLicenci());

        PridatSkladCommand = new RelayCommand(_ => PridejSklad(),
            _ => !string.IsNullOrWhiteSpace(NovySkladNazev));
        DeaktivovatSkladCommand = new RelayCommand(_ => DeaktivujSklad(),
            _ => VybranySklad is not null && !VybranySklad.JeVychozi);
        NastavitVychoziSkladCommand = new RelayCommand(_ => NastavVychoziSklad(),
            _ => VybranySklad is not null && !VybranySklad.JeVychozi);
        PrejmenovatSkladCommand = new RelayCommand(_ => PrejmenujSklad(),
            _ => VybranySklad is not null);
    }

    public void Nacti()
    {
        var n = DatabaseService.LoadNastaveni();
        FirmaNazev   = Get(n, NastaveniKey.FirmaNazev);
        FirmaIco     = Get(n, NastaveniKey.FirmaIco);
        FirmaDic     = Get(n, NastaveniKey.FirmaDic);
        FirmaUlice   = Get(n, NastaveniKey.FirmaUlice);
        FirmaMesto   = Get(n, NastaveniKey.FirmaMesto);
        FirmaPsc     = Get(n, NastaveniKey.FirmaPsc);
        FirmaStat    = Get(n, NastaveniKey.FirmaStat) ?? "ČR";
        FirmaEmail   = Get(n, NastaveniKey.FirmaEmail);
        FirmaTelefon = Get(n, NastaveniKey.FirmaTel);

        LicenseKey = Get(n, "license.key") ?? string.Empty;

        var auto = Get(n, NastaveniKey.UpdateAutoCheck);
        AutoCheck = auto is null || auto == "1";

        Sazby.Clear();
        foreach (var s in DatabaseService.LoadAktivniSazbyDph())
            Sazby.Add(s);

        Kategorie.Clear();
        foreach (var k in DatabaseService.LoadAktivniKategorie())
            Kategorie.Add(k);

        // Po načtení doporučit další pořadí o 10 výš než maximum
        NovaKategoriePoradi = Kategorie.Count == 0 ? 10 : (Kategorie.Max(k => k.Poradi) + 10);

        Sklady.Clear();
        foreach (var s in DatabaseService.LoadSklady(jenAktivni: false))
            Sklady.Add(s);
    }

    // ---------- Sklady ----------
    private void PridejSklad()
    {
        DatabaseService.SaveSklad(new Sklad
        {
            Nazev = NovySkladNazev.Trim(),
            JeVychozi = NovySkladVychozi,
            JeAktivni = true,
            Poradi = Sklady.Count == 0 ? 10 : (Sklady.Max(s => s.Poradi) + 10)
        });
        NovySkladNazev = string.Empty;
        NovySkladVychozi = false;
        Nacti();
    }

    private void DeaktivujSklad()
    {
        if (VybranySklad is null || VybranySklad.JeVychozi) return;
        var res = MessageBox.Show(
            $"Deaktivovat sklad '{VybranySklad.Nazev}'?\n\nHistorie zůstane zachována, ale sklad nebude možné vybrat v nových dokladech.",
            "Deaktivace skladu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;
        DatabaseService.DeactivateSklad(VybranySklad.Id);
        Nacti();
    }

    private void NastavVychoziSklad()
    {
        if (VybranySklad is null) return;
        VybranySklad.JeVychozi = true;
        DatabaseService.SaveSklad(VybranySklad);
        Nacti();
    }

    private void PrejmenujSklad()
    {
        if (VybranySklad is null) return;
        var dlg = new RenameWindow(VybranySklad.Nazev) { Owner = Application.Current?.MainWindow };
        if (dlg.ShowDialog() != true) return;
        VybranySklad.Nazev = dlg.NewName;
        DatabaseService.SaveSklad(VybranySklad);
        Nacti();
    }

    private static string? Get(Dictionary<string, string> n, string klic)
        => n.TryGetValue(klic, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    private void UlozFirmu()
    {
        DatabaseService.SaveNastaveniBulk(new Dictionary<string, string?>
        {
            [NastaveniKey.FirmaNazev] = FirmaNazev,
            [NastaveniKey.FirmaIco]   = FirmaIco,
            [NastaveniKey.FirmaDic]   = FirmaDic,
            [NastaveniKey.FirmaUlice] = FirmaUlice,
            [NastaveniKey.FirmaMesto] = FirmaMesto,
            [NastaveniKey.FirmaPsc]   = FirmaPsc,
            [NastaveniKey.FirmaStat]  = FirmaStat,
            [NastaveniKey.FirmaEmail] = FirmaEmail,
            [NastaveniKey.FirmaTel]   = FirmaTelefon,
        });
        MessageBox.Show("Údaje o firmě uloženy.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PridejSazbu()
    {
        DatabaseService.SaveSazbaDph(new SazbaDPH
        {
            Sazba = NovaSazba,
            Popis = NovaPopis,
            JeVychozi = NovaJeVychozi
        });
        NovaSazba = 21;
        NovaPopis = string.Empty;
        NovaJeVychozi = false;
        Nacti();
    }

    private void DeaktivujSazbu()
    {
        if (VybranaSazba is null) return;
        DatabaseService.DeactivateSazbaDph(VybranaSazba.Id);
        Nacti();
    }

    private void PridejKategorii()
    {
        DatabaseService.SaveKategorie(new Kategorie
        {
            Nazev = NovaKategorieNazev.Trim(),
            Poradi = NovaKategoriePoradi
        });
        NovaKategorieNazev = string.Empty;
        Nacti();
    }

    private void DeaktivujKategorii()
    {
        if (VybranaKategorie is null) return;
        DatabaseService.DeactivateKategorie(VybranaKategorie.Id);
        Nacti();
    }

    private void UlozAutoCheck()
    {
        DatabaseService.SaveNastaveni(NastaveniKey.UpdateAutoCheck, AutoCheck ? "1" : "0");
        MessageBox.Show("Nastavení aktualizací uloženo.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task ZkontrolovatNyniAsync()
    {
        StatusAktualizace = "Kontroluji…";
        var info = await UpdateService.CheckForUpdatesAsync();
        DatabaseService.SaveNastaveni(NastaveniKey.UpdateLastCheck, DateTime.Now.ToString("o"));

        if (info is null)
        {
            StatusAktualizace = $"Aktuální verze {UpdateService.CurrentVersion} je nejnovější.";
            return;
        }

        StatusAktualizace = $"K dispozici je nová verze {info.Version}.";
        var res = MessageBox.Show(
            $"Je k dispozici nová verze {info.Version}.\n\nStáhnout a nainstalovat?",
            "Aktualizace", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                var dir = await UpdateService.DownloadAndPrepareAsync(info);
                UpdateService.LaunchUpdaterAndExit(dir);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Aktualizace selhala:\n" + ex.Message, "Chyba",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ---------- Licence ----------

    private async Task AktivovatLicenciAsync()
    {
        var info = await LicenseService.ValidateAsync(LicenseKey.Trim());
        NotifyLicence();

        if (info.IsValid)
        {
            MessageBox.Show(
                $"Licence aktivována!\n\nZákazník: {info.Customer}\nVyprší: {info.Expires}",
                "Licence", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                $"Aktivace se nezdařila:\n{info.Error}",
                "Licence", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OdebratLicenci()
    {
        LicenseService.ClearLicense();
        LicenseKey = string.Empty;
        NotifyLicence();
        MessageBox.Show("Licence odebrána. Aplikace běží v DEMO režimu.",
            "Licence", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void NotifyLicence()
    {
        OnPropertyChanged(nameof(LicenceStav));
        OnPropertyChanged(nameof(LicenceDetail));
        OnPropertyChanged(nameof(LicenceBarva));
    }

    // ---------- Zálohy ----------

    private void ExportZalohu()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exportovat zálohu databáze",
            FileName = $"gastrox-zaloha-{DateTime.Now:yyyy-MM-dd}.db",
            Filter = "SQLite databáze (*.db)|*.db|Všechny soubory (*.*)|*.*",
            DefaultExt = ".db"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.Copy(DatabaseService.DbPath, dlg.FileName, overwrite: true);
            MessageBox.Show($"Záloha uložena:\n{dlg.FileName}",
                "Export hotov", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při exportu:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportZalohu()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importovat zálohu databáze",
            Filter = "SQLite databáze (*.db)|*.db|Všechny soubory (*.*)|*.*",
            DefaultExt = ".db"
        };
        if (dlg.ShowDialog() != true) return;

        var res = MessageBox.Show(
            "Opravdu chcete nahradit aktuální databázi zvolenou zálohou?\n\n" +
            "Aktuální data budou přepsána a aplikace se restartuje.",
            "Import zálohy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            File.Copy(dlg.FileName, DatabaseService.DbPath, overwrite: true);

            // Restart aplikace
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is not null)
                Process.Start(exe);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při importu:\n" + ex.Message,
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
