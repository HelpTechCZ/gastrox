using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Gastrox.Behaviors;

/// <summary>
/// Attached chování pro numerické <see cref="TextBox"/>y:
/// <list type="bullet">
///   <item><description>Při získání focusu (klikem i tabem) vybere celý obsah – uživatel
///   může ihned psát a nemusí přesně mířit kurzor mezi „0,00".</description></item>
///   <item><description>Tečka zadaná z klávesnice se automaticky převede na čárku
///   (desetinný oddělovač české kultury), takže uživatel může psát „1.10" i „1,10"
///   zcela zaměnitelně.</description></item>
/// </list>
/// Použití: <c>local:NumericBoxBehavior.IsNumeric="True"</c> v XAML.
/// </summary>
public static class NumericBoxBehavior
{
    public static readonly DependencyProperty IsNumericProperty =
        DependencyProperty.RegisterAttached(
            "IsNumeric",
            typeof(bool),
            typeof(NumericBoxBehavior),
            new PropertyMetadata(false, OnIsNumericChanged));

    public static void SetIsNumeric(DependencyObject element, bool value) =>
        element.SetValue(IsNumericProperty, value);

    public static bool GetIsNumeric(DependencyObject element) =>
        (bool)element.GetValue(IsNumericProperty);

    private static void OnIsNumericChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;

        if ((bool)e.NewValue)
        {
            tb.GotKeyboardFocus       += OnGotFocus;
            tb.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            tb.PreviewTextInput       += OnPreviewTextInput;
        }
        else
        {
            tb.GotKeyboardFocus       -= OnGotFocus;
            tb.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
            tb.PreviewTextInput       -= OnPreviewTextInput;
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
    }

    // Zajistí, že kliknutí myší také vybere celý obsah (bez tohoto triku by klik
    // pouze umístil kurzor a SelectAll z GotFocus by byl vzápětí přepsán).
    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
        {
            tb.Focus();
            e.Handled = true;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox tb) return;
        // Přijímáme oba oddělovače – substitujeme na kulturní, ať binding umí parsovat.
        if (e.Text != "." && e.Text != ",") return;

        var sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (e.Text == sep) return; // už je to ten správný znak, nic nedělat

        var start = tb.SelectionStart;
        var len   = tb.SelectionLength;
        tb.Text   = (tb.Text ?? string.Empty).Remove(start, len).Insert(start, sep);
        tb.CaretIndex = start + sep.Length;
        e.Handled = true;
    }
}
