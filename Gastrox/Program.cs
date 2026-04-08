using System;
using System.IO;
using System.Windows;

namespace Gastrox;

/// <summary>
/// Vlastní entry point – aby bylo možné zachytit i chyby, které vznikají
/// dřív, než WPF stihne načíst App.xaml (např. selhání JIT, chybějící DLL,
/// rozbité ResourceDictionary). Vše loguje do gastrox-startup.log vedle .exe.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log("=== Gastrox start ===");

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
