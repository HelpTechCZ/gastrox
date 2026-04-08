using System;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class InventuraWizardView : UserControl
{
    public event Action? Hotovo;

    public InventuraWizardView()
    {
        InitializeComponent();
        if (DataContext is InventuraWizardViewModel vm)
            vm.Hotovo += () => Hotovo?.Invoke();
    }
}
