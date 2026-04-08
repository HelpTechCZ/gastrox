using System;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class VydejkaWizardView : UserControl
{
    public event Action? Hotovo;

    public VydejkaWizardView()
    {
        InitializeComponent();
        if (DataContext is VydejkaWizardViewModel vm)
            vm.Hotovo += () => Hotovo?.Invoke();
    }
}
