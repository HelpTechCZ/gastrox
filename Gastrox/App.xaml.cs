using System.Windows;
using Gastrox.Services;

namespace Gastrox;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // QuestPDF licence – Community (zdarma pro malé firmy)
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Inicializace databáze (vytvoří sklad.db vedle .exe, pokud neexistuje)
        DatabaseService.Initialize();
    }
}
