using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace Gastrox;

/// <summary>
/// Vlastní entry point – aby bylo možné zachytit i chyby, které vznikají
/// dřív, než WPF stihne načíst App.xaml (např. selhání JIT, chybějící DLL,
/// rozbité ResourceDictionary). Vše loguje do gastrox-startup.log vedle .exe.
/// </summary>
public static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        Log("=== Gastrox start ===");

        // Zamezení duplicitnímu spuštění
        const string mutexName = "Global\\Gastrox_B7A3F2E1_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            Log("Již běží jiná instance – ukončuji.");
            MessageBox.Show(
                "Gastrox už běží.\n\nNelze spustit dvě instance současně.",
                "Gastrox", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 0;
        }

        try
        {
            Log("Vytvářím App instanci...");
            var app = new App();

            Log("Inicializuji komponenty...");
            app.InitializeComponent();

            Log("Spouštím Run()...");
            return app.Run();
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            try
            {
                MessageBox.Show(
                    $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Gastrox – fatální chyba startu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { /* MessageBox může být nedostupný */ }
            return 1;
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "gastrox-startup.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* logování nesmí samo havarovat */ }
    }
}
