using System.Windows;
using System.Windows.Controls;
using Gastrox.Views;

namespace Gastrox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new PrijemkaView();
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)  => MainContent.Content = Placeholder("Dashboard – TODO");
    private void Sklad_Click(object sender, RoutedEventArgs e)      => MainContent.Content = Placeholder("Sklad – TODO");
    private void Prijemky_Click(object sender, RoutedEventArgs e)   => MainContent.Content = new PrijemkaView();
    private void Vydejky_Click(object sender, RoutedEventArgs e)    => MainContent.Content = Placeholder("Výdejky – TODO");
    private void Inventury_Click(object sender, RoutedEventArgs e)  => MainContent.Content = Placeholder("Inventury – TODO");
    private void Reporty_Click(object sender, RoutedEventArgs e)    => MainContent.Content = Placeholder("Reporty – TODO");

    private static TextBlock Placeholder(string text) => new()
    {
        Text = text,
        FontSize = 24,
        Margin = new Thickness(24)
    };
}
