using System.Windows;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class PohybyView : UserControl
{
    public PohybyView()
    {
        InitializeComponent();
    }

    private void VymazatFiltr_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PohybyViewModel vm)
            vm.VybranySklad = null;
    }
}
