using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Gastrox.Services;

namespace Gastrox;

public partial class App : Application
{
    private const int MaxZaloh = 10;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Globální handlery, aby aplikace neumřela tiše bez stopy.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        try
        {
            base.OnStartup(e);

            // QuestPDF licence – Community (zdarma pro malé firmy)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Inicializace databáze (vytvoří sklad.db vedle .exe, pokud neexistuje)
            DatabaseService.Initialize();
        }
        catch (Exception ex)
        {
            ShowError("Chyba při startu aplikace", ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            CreateBackup();
        }
        catch { /* záloha nesmí zabránit ukončení */ }
        base.OnExit(e);
    }

    private static void CreateBackup()
    {
        var dbPath = DatabaseService.DbPath;
        if (!File.Exists(dbPath)) return;

        var backupDir = Path.Combine(AppContext.BaseDirectory, "Zálohy");
        Directory.CreateDirectory(backupDir);

        var fileName = $"sklad-{DateTime.Now:yyyy-MM-dd-HHmmss}.db";
        File.Copy(dbPath, Path.Combine(backupDir, fileName), overwrite: true);

        // Ponechat jen posledních N záloh
        var files = Directory.GetFiles(backupDir, "sklad-*.db")
            .OrderByDescending(f => f)
            .Skip(MaxZaloh)
            .ToArray();
        foreach (var old in files)
            File.Delete(old);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError("Neošetřená výjimka v UI vlákně", e.Exception);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowError("Fatální chyba aplikace", ex);
    }

    private static void ShowError(string title, Exception ex)
    {
        // Zaloguj do souboru vedle .exe – pro offline diagnostiku
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "gastrox-error.log");
            File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* logování nesmí samo havarovat */ }

        MessageBox.Show(
            $"{ex.GetType().Name}: {ex.Message}\n\nDetail uložen do gastrox-error.log vedle .exe.\n\n{ex.StackTrace}",
            $"Gastrox – {title}",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
