using System.Windows;
using System.Windows.Input;

namespace Gastrox.Views;

public partial class RenameWindow : Window
{
    public string NewName { get; private set; } = string.Empty;

    public RenameWindow(string current)
    {
        InitializeComponent();
        TxtNazev.Text = current;
        TxtNazev.SelectAll();
        TxtNazev.Focus();
    }

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        var n = TxtNazev.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(n))
        {
            MessageBox.Show("Název nesmí být prázdný.", "Přejmenovat",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        NewName = n;
        DialogResult = true;
    }

    private void Zrusit_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TxtNazev_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ulozit_Click(sender, e);
        else if (e.Key == Key.Escape) Zrusit_Click(sender, e);
    }
}
