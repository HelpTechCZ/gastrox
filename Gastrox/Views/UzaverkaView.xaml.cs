using System;
using System.Windows;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class UzaverkaView : UserControl
{
    public event Action? Hotovo;

    public UzaverkaView()
    {
        InitializeComponent();
        if (DataContext is UzaverkaViewModel vm)
            vm.Hotovo += () => Hotovo?.Invoke();
    }

    private void VymazatFiltr_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is UzaverkaViewModel vm)
            vm.VybranySklad = null;
    }
}
