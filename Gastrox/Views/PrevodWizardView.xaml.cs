using System;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class PrevodWizardView : UserControl
{
    public event Action? Hotovo;

    public PrevodWizardView()
    {
        InitializeComponent();
        if (DataContext is PrevodWizardViewModel vm)
            vm.Hotovo += () => Hotovo?.Invoke();
    }
}
