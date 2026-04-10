using System;
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
}
