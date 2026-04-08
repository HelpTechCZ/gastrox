using System;
using System.Windows.Controls;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class NaskladnitWizardView : UserControl
{
    public event Action? Hotovo;

    public NaskladnitWizardView()
    {
        InitializeComponent();
        if (DataContext is NaskladnitWizardViewModel vm)
            vm.Hotovo += () => Hotovo?.Invoke();
    }
}
