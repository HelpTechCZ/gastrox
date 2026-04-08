using System.Windows;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    /// <summary>Hlavní okno se přihlásí, aby vědělo, kam má dlaždice směrovat.</summary>
    public event RoutedEventHandler? NaskladnitClicked;
    public event RoutedEventHandler? VyskladnitClicked;
    public event RoutedEventHandler? InventuraClicked;

    private void Naskladnit_Click(object sender, RoutedEventArgs e) => NaskladnitClicked?.Invoke(this, e);
    private void Vyskladnit_Click(object sender, RoutedEventArgs e) => VyskladnitClicked?.Invoke(this, e);
    private void Inventura_Click(object sender, RoutedEventArgs e)  => InventuraClicked?.Invoke(this, e);

    public void RefreshData()
    {
        if (DataContext is DashboardViewModel vm)
            vm.Refresh();
    }
}
