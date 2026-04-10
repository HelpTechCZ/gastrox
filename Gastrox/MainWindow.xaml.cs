using System;
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

    private void ShowUzaverka()
    {
        var v = new UzaverkaView();
        v.Hotovo += ShowDashboard;
        MainContent.Content = v;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)  => ShowDashboard();
    private void Sklad_Click(object sender, RoutedEventArgs e)      => MainContent.Content = new SkladView();
    private void Naskladnit_Click(object sender, RoutedEventArgs e) => ShowNaskladnit();
    private void Vyskladnit_Click(object sender, RoutedEventArgs e) => ShowVyskladnit();
    private void Uzaverka_Click(object sender, RoutedEventArgs e)   => ShowUzaverka();
    private void Pohyby_Click(object sender, RoutedEventArgs e)     => MainContent.Content = new PohybyView();
    private void Nastaveni_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new NastaveniView();
}
