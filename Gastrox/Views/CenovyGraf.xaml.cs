using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Gastrox.Models;

namespace Gastrox.Views;

/// <summary>
/// Jednoduchý line-chart pro vývoj nákupních cen skladové karty.
/// Nezávislý na knihovnách třetích stran – kreslí se ručně do WPF Canvasu,
/// aby aplikace zůstala portable (jediný .exe bez instalátoru).
///
/// Přerenderuje se při změně <see cref="Body"/> nebo velikosti kontrolky.
/// </summary>
public partial class CenovyGraf : UserControl
{
    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(
            nameof(Body),
            typeof(IEnumerable<CenovyBod>),
            typeof(CenovyGraf),
            new PropertyMetadata(null, OnBodyChanged));

    public IEnumerable<CenovyBod>? Body
    {
        get => (IEnumerable<CenovyBod>?)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    // Barvy svázané s paletou aplikace (viz App.xaml / jiné views).
    private static readonly Brush BrushLine      = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly Brush BrushFill      = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x78, 0xD4));
    private static readonly Brush BrushDot       = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
    private static readonly Brush BrushText      = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
    private static readonly Brush BrushAxis      = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly Brush BrushEmptyText = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

    public CenovyGraf()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
        Loaded      += (_, _) => Redraw();
    }

    private static void OnBodyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gr = (CenovyGraf)d;

        // Kolekce může být ObservableCollection – subscribnout se na změny,
        // aby se graf překreslil, když přibyde bod.
        if (e.OldValue is INotifyCollectionChanged oldCc)
            oldCc.CollectionChanged -= gr.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCc)
            newCc.CollectionChanged += gr.OnCollectionChanged;

        gr.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        Plocha.Children.Clear();

        var body = Body?.Cast<CenovyBod>()
                        .OrderBy(b => b.Datum)
                        .ToList();

        double w = Plocha.ActualWidth;
        double h = Plocha.ActualHeight;
        if (w < 20 || h < 20) return;

        if (body is null || body.Count == 0)
        {
            Plocha.Children.Add(new TextBlock
            {
                Text       = "Zatím žádné nákupy – po první příjemce se tu objeví graf.",
                FontSize   = 12,
                Foreground = BrushEmptyText,
                Margin     = new Thickness(8)
            });
            return;
        }

        // Okraje pro popisky – vlevo místo na cenu, dole na datum.
        const double padL = 54;
        const double padR = 8;
        const double padT = 10;
        const double padB = 22;
        double plotW = w - padL - padR;
        double plotH = h - padT - padB;
        if (plotW < 10 || plotH < 10) return;

        // Speciální případ: jeden bod = jen křížek + cena.
        if (body.Count == 1)
        {
            var jen = body[0];
            DrawAxes(padL, padT, plotW, plotH,
                     jen.CenaZaJednotkuBezDPH, jen.CenaZaJednotkuBezDPH);
            var cx = padL + plotW / 2;
            var cy = padT + plotH / 2;
            AddDot(cx, cy);
            AddText($"{jen.CenaZaJednotkuBezDPH:N2} Kč", cx + 6, cy - 18, 11, BrushDot, bold: true);
            AddText(jen.Datum.ToString("d.M.yyyy"), cx - 34, cy + 8, 10, BrushText);
            return;
        }

        // Škálování
        var minDatum = body.First().Datum;
        var maxDatum = body.Last().Datum;
        var rangeSec = Math.Max(1, (maxDatum - minDatum).TotalSeconds);

        var minCena = body.Min(b => b.CenaZaJednotkuBezDPH);
        var maxCena = body.Max(b => b.CenaZaJednotkuBezDPH);
        if (maxCena - minCena < 0.01m)
        {
            // plochá řada – přidat trochu místa nad i pod, aby nevypadalo jako spojnice u dna
            minCena -= 1m;
            maxCena += 1m;
        }
        var rangeCena = (double)(maxCena - minCena);

        // Osy + osa Y popisky
        DrawAxes(padL, padT, plotW, plotH, minCena, maxCena);

        // Polygon (fill pod čarou)
        var fill = new Polygon { Fill = BrushFill, IsHitTestVisible = false };
        fill.Points.Add(new Point(padL, padT + plotH));

        var line = new Polyline
        {
            Stroke           = BrushLine,
            StrokeThickness  = 2,
            StrokeLineJoin   = PenLineJoin.Round,
            IsHitTestVisible = false
        };

        var bodyPoints = new List<Point>(body.Count);
        foreach (var b in body)
        {
            double x = padL + ((b.Datum - minDatum).TotalSeconds / rangeSec) * plotW;
            double y = padT + (1 - (double)(b.CenaZaJednotkuBezDPH - minCena) / rangeCena) * plotH;
            bodyPoints.Add(new Point(x, y));
            line.Points.Add(new Point(x, y));
            fill.Points.Add(new Point(x, y));
        }
        fill.Points.Add(new Point(bodyPoints[^1].X, padT + plotH));

        Plocha.Children.Add(fill);
        Plocha.Children.Add(line);

        // Body s tooltipy
        for (int i = 0; i < body.Count; i++)
        {
            var b  = body[i];
            var p  = bodyPoints[i];
            var el = new Ellipse
            {
                Width     = 8,
                Height    = 8,
                Fill      = Brushes.White,
                Stroke    = BrushDot,
                StrokeThickness = 2,
                ToolTip   = $"{b.Datum:d.M.yyyy}\n{b.CenaZaJednotkuBezDPH:N2} Kč/j."
                          + (string.IsNullOrWhiteSpace(b.TypBaleni) ? "" : $"\nBalení: {b.TypBaleni}")
            };
            Canvas.SetLeft(el, p.X - 4);
            Canvas.SetTop(el,  p.Y - 4);
            Plocha.Children.Add(el);
        }

        // Popisky osy X (první a poslední datum + případně střední)
        AddText(minDatum.ToString("d.M.yyyy"), padL - 4, padT + plotH + 4, 10, BrushText);
        var lastLabel = maxDatum.ToString("d.M.yyyy");
        var lastW     = EstimateTextWidth(lastLabel, 10);
        AddText(lastLabel, padL + plotW - lastW, padT + plotH + 4, 10, BrushText);
    }

    // ------- pomocné kreslicí metody -------

    private void DrawAxes(double padL, double padT, double plotW, double plotH,
                          decimal minCena, decimal maxCena)
    {
        // Dolní a levá osa
        var axis = new Line
        {
            X1 = padL,         Y1 = padT + plotH,
            X2 = padL + plotW, Y2 = padT + plotH,
            Stroke = BrushAxis, StrokeThickness = 1
        };
        var axisY = new Line
        {
            X1 = padL, Y1 = padT,
            X2 = padL, Y2 = padT + plotH,
            Stroke = BrushAxis, StrokeThickness = 1
        };
        Plocha.Children.Add(axis);
        Plocha.Children.Add(axisY);

        // Popisky osy Y – min, max, střed
        AddText($"{maxCena:N2}",                    2, padT - 6,             10, BrushText);
        AddText($"{(minCena + maxCena) / 2m:N2}",   2, padT + plotH / 2 - 7, 10, BrushText);
        AddText($"{minCena:N2}",                    2, padT + plotH - 7,     10, BrushText);

        // Horizontální grid na střed
        var grid = new Line
        {
            X1 = padL,         Y1 = padT + plotH / 2,
            X2 = padL + plotW, Y2 = padT + plotH / 2,
            Stroke = BrushAxis, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 3 }
        };
        Plocha.Children.Add(grid);
    }

    private void AddDot(double x, double y)
    {
        var el = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brushes.White, Stroke = BrushDot, StrokeThickness = 2
        };
        Canvas.SetLeft(el, x - 5);
        Canvas.SetTop(el,  y - 5);
        Plocha.Children.Add(el);
    }

    private void AddText(string text, double x, double y, double size, Brush brush, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = size,
            Foreground = brush,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb,  y);
        Plocha.Children.Add(tb);
    }

    /// <summary>Hrubý odhad šířky textu (bez FormattedText – stačí pro zarovnání posledního popisku).</summary>
    private static double EstimateTextWidth(string text, double fontSize)
        => text.Length * fontSize * 0.55;
}
