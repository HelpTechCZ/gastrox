using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class VydejkaWizardView : UserControl
{
    public event Action? Hotovo;

    public VydejkaWizardView()
    {
        InitializeComponent();
        if (DataContext is VydejkaWizardViewModel vm)
        {
            vm.Hotovo += () => Hotovo?.Invoke();
            vm.RadekPridan += OnRadekPridan;
        }
    }

    private void OnRadekPridan()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (DataContext is not VydejkaWizardViewModel vm || vm.Radky.Count == 0) return;
            var last = vm.Radky[^1];
            GridPolozky.ScrollIntoView(last);
            GridPolozky.SelectedItem = last;
        });
    }
}
