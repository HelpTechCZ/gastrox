using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Gastrox.Models;
using Gastrox.Services;
using Gastrox.Views;

namespace Gastrox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowDashboard();
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        TxtVerze.Text = $"v{UpdateService.CurrentVersion}";

        var lic = LicenseService.Current;
        if (lic is null || !lic.IsValid)
        {
            TxtLicence.Text = "DEMO verze";
            TxtLicence.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xd1, 0x34, 0x38));
        }
        else if (!string.IsNullOrEmpty(lic.Expires) && DateTime.TryParse(lic.Expires, out var exp))
        {
            var days = (int)Math.Ceiling((exp - DateTime.Now).TotalDays);
            TxtLicence.Text = days > 0
                ? $"Licence: {exp:dd.MM.yyyy} ({days} dní)"
                : "Licence vypršela";
            TxtLicence.Foreground = new System.Windows.Media.SolidColorBrush(
                days > 30 ? System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)
                           : System.Windows.Media.Color.FromRgb(0xd1, 0x34, 0x38));
        }
        else
        {
            TxtLicence.Text = "Licence: aktivní";
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
        try
        {
            var dbPath = DatabaseService.DbPath;
            if (!File.Exists(dbPath)) return;

            var backupDir = Path.Combine(AppContext.BaseDirectory, "backup");
            Directory.CreateDirectory(backupDir);

            var fileName = $"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            var fullPath = Path.Combine(backupDir, fileName);
            File.Copy(dbPath, fullPath, overwrite: true);

            // Ponechat jen posledních 10 záloh
            var old = Directory.GetFiles(backupDir, "backup_*.db")
                .OrderByDescending(f => f)
                .Skip(10)
                .ToArray();
            foreach (var f in old) File.Delete(f);

            MessageBox.Show(
                $"Záloha databáze byla vytvořena.\n\n{fileName}",
                "Záloha hotova", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Záloha se nepodařila:\n" + ex.Message,
                "Chyba zálohy", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)  => ShowDashboard();
    private void Sklad_Click(object sender, RoutedEventArgs e)      => MainContent.Content = new SkladView();
    private void Naskladnit_Click(object sender, RoutedEventArgs e) => ShowNaskladnit();
    private void Vyskladnit_Click(object sender, RoutedEventArgs e) => ShowVyskladnit();
    private void Prevody_Click(object sender, RoutedEventArgs e)    => ShowPrevody();
    private void Uzaverka_Click(object sender, RoutedEventArgs e)   => ShowUzaverka();
    private void Pohyby_Click(object sender, RoutedEventArgs e)     => MainContent.Content = new PohybyView();
    private void Nastaveni_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new NastaveniView();
}
