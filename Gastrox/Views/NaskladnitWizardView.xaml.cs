using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Gastrox.ViewModels;

namespace Gastrox.Views;

public partial class NaskladnitWizardView : UserControl
{
    public event Action? Hotovo;

    public NaskladnitWizardView()
    {
        InitializeComponent();
        if (DataContext is NaskladnitWizardViewModel vm)
        {
            vm.Hotovo += () => Hotovo?.Invoke();
            vm.RadekPridan += OnRadekPridan;
        }
    }

    private void OnRadekPridan()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ScrollPolozky.ScrollToEnd();
        });
    }
}
