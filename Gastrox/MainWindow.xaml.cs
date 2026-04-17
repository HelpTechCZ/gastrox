using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Gastrox.Models;
using Gastrox.Services;
using Gastrox.Views;

namespace Gastrox;

public partial class MainWindow : Window
{
    private bool _skipBackup;

    /// <summary>Nastaví přeskočení zálohy při zavření (volá se před Shutdown z update flow).</summary>
    public void SkipBackupOnClose() => _skipBackup = true;

    public MainWindow()
    {
        InitializeComponent();
        ShowDashboard();
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        TxtVerze.Text = $"Gastrox v{UpdateService.CurrentVersion}";

        var lic = LicenseService.Current;
        var n = DatabaseService.LoadNastaveni();
        var licKey = n.TryGetValue("license.key", out var k) ? k : null;

        if (lic is null || !lic.IsValid)
        {
            TxtLicenceKlic.Text = "DEMO verze";
            TxtLicenceKlic.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xd1, 0x34, 0x38));
            TxtLicenceStav.Text = string.Empty;
        }
        else
        {
            // Posledních 8 znaků licenčního klíče
            var suffix = !string.IsNullOrEmpty(licKey) && licKey!.Length >= 8
                ? $"Licence: ...{licKey[^8..]}"
                : "Licence: aktivní";
            TxtLicenceKlic.Text = suffix;
            TxtLicenceKlic.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66));

            if (!string.IsNullOrEmpty(lic.Expires) && DateTime.TryParse(lic.Expires, out var exp))
            {
                var days = (int)Math.Ceiling((exp - DateTime.Now).TotalDays);
                TxtLicenceStav.Text = days > 0
                    ? $"platná ještě {days} dní"
                    : "licence vypršela";
                TxtLicenceStav.Foreground = new System.Windows.Media.SolidColorBrush(
                    days > 30 ? System.Windows.Media.Color.FromRgb(0x10, 0x7C, 0x10)
                               : System.Windows.Media.Color.FromRgb(0xd1, 0x34, 0x38));
            }
            else
            {
                TxtLicenceStav.Text = "platná (bez omezení)";
                TxtLicenceStav.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0x7C, 0x10));
            }
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Automatická kontrola aktualizací při startu, pokud je zapnutá
        try
        {
            var n = DatabaseService.LoadNastaveni();
            var auto = n.TryGetValue(NastaveniKey.UpdateAutoCheck, out var v) ? v : "1";
            if (auto != "1") return;

            var info = await UpdateService.CheckForUpdatesAsync();
            DatabaseService.SaveNastaveni(NastaveniKey.UpdateLastCheck, DateTime.Now.ToString("o"));

            if (info is null) return;

            var res = MessageBox.Show(
                $"Je k dispozici nová verze {info.Version}.\n\nStáhnout a nainstalovat nyní?",
                "Aktualizace Gastrox",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (res != MessageBoxResult.Yes) return;

            var dir = await UpdateService.DownloadAndPrepareAsync(info);
            UpdateService.LaunchUpdaterAndExit(dir);
            _skipBackup = true;
            Application.Current.Shutdown();
        }
        catch
        {
            // Aktualizace nesmí blokovat start aplikace
        }
    }

    private void ShowDashboard()
    {
        var dash = new DashboardView();
        dash.NaskladnitClicked += (_, __) => ShowNaskladnit();
        dash.VyskladnitClicked += (_, __) => ShowVyskladnit();
        dash.UzaverkaClicked   += (_, __) => ShowUzaverka();
        dash.NovaKartaClicked  += (_, __) => ShowSkladNovaKarta();
        MainContent.Content = dash;
    }

    private void ShowSkladNovaKarta()
    {
        var v = new SkladView();
        if (v.DataContext is ViewModels.SkladViewModel svm && svm.NovaKartaCommand.CanExecute(null))
            svm.NovaKartaCommand.Execute(null);
        MainContent.Content = v;
    }

    private void ShowNaskladnit()
    {
        var v = new NaskladnitWizardView();
        v.Hotovo += ShowDashboard;
        MainContent.Content = v;
    }

    private void ShowVyskladnit()
    {
        var v = new VydejkaWizardView();
        v.Hotovo += ShowDashboard;
        MainContent.Content = v;
    }

    private void ShowPrevody()
    {
        var v = new PrevodWizardView();
        v.Hotovo += ShowDashboard;
        MainContent.Content = v;
    }

    private void ShowUzaverka()
    {
        var v = new UzaverkaView();
        v.Hotovo += ShowDashboard;
        MainContent.Content = v;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_skipBackup) return;

        try
        {
            var dbPath = DatabaseService.DbPath;
            if (!File.Exists(dbPath)) return;

            var backupDir = Path.Combine(AppContext.BaseDirectory, "backup");
            Directory.CreateDirectory(backupDir);

            // Přeskočit, pokud záloha z posledních 60 s již existuje
            var recent = Directory.GetFiles(backupDir, "backup_*.db")
                .Select(f => new FileInfo(f))
                .Any(fi => (DateTime.Now - fi.CreationTime).TotalSeconds < 60);
            if (recent) return;

            var fileName = $"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            var fullPath = Path.Combine(backupDir, fileName);
            File.Copy(dbPath, fullPath, overwrite: true);

            // Ponechat jen posledních 10 záloh
            var old = Directory.GetFiles(backupDir, "backup_*.db")
                .OrderByDescending(f => f)
                .Skip(10)
                .ToArray();
            foreach (var f in old) File.Delete(f);

            // Okno s informací o záloze – zavře se samo po 5 s
            var win = new Window
            {
                Title = "Záloha hotova",
                Width = 360, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = true,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = $"Záloha databáze byla vytvořena.\n{fileName}",
                    Margin = new Thickness(20, 20, 20, 20),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                }
            };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); win.Close(); };
            timer.Start();
            win.ShowDialog();
        }
        catch
        {
            // Záloha nesmí blokovat zavření aplikace
        }
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)  => ShowDashboard();
    private void Sklad_Click(object sender, RoutedEventArgs e)      => MainContent.Content = new SkladView();
    private void Naskladnit_Click(object sender, RoutedEventArgs e) => ShowNaskladnit();
    private void Vyskladnit_Click(object sender, RoutedEventArgs e) => ShowVyskladnit();
    private void Prevody_Click(object sender, RoutedEventArgs e)    => ShowPrevody();
    private void Uzaverka_Click(object sender, RoutedEventArgs e)   => ShowUzaverka();
    private void Pohyby_Click(object sender, RoutedEventArgs e)     => MainContent.Content = new PohybyView();
    private void Marze_Click(object sender, RoutedEventArgs e)      => MainContent.Content = new MarzeView();
    private void Nastaveni_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new NastaveniView();
}
