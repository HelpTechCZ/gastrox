using System.Windows;
using Gastrox.Views;

namespace Gastrox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new PrijemkaView();
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new TextBlock { Text = "Dashboard – TODO" };
    private void Sklad_Click(object sender, RoutedEventArgs e)      => MainContent.Content = new TextBlock { Text = "Sklad – TODO" };
    private void Prijemky_Click(object sender, RoutedEventArgs e)   => MainContent.Content = new PrijemkaView();
    private void Vydejky_Click(object sender, RoutedEventArgs e)    => MainContent.Content = new TextBlock { Text = "Výdejky – TODO" };
    private void Inventury_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new TextBlock { Text = "Inventury – TODO" };
    private void Reporty_Click(object sender, RoutedEventArgs e)    => MainContent.Content = new TextBlock { Text = "Reporty – TODO" };
}

// Dočasný stub, aby se projekt přeložil i bez vytvořených ostatních views
internal class TextBlock : System.Windows.Controls.TextBlock
{
    public TextBlock() { FontSize = 24; Margin = new Thickness(24); }
}
