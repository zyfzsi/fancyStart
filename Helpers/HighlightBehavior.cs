#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FancyStart.Helpers;

public static class HighlightBehavior
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(HighlightBehavior),
            new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty HighlightProperty =
        DependencyProperty.RegisterAttached("Highlight", typeof(string), typeof(HighlightBehavior),
            new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty HighlightBrushProperty =
        DependencyProperty.RegisterAttached("HighlightBrush", typeof(Brush), typeof(HighlightBehavior),
            new PropertyMetadata(Brushes.Yellow));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    public static string GetHighlight(DependencyObject obj) => (string)obj.GetValue(HighlightProperty);
    public static void SetHighlight(DependencyObject obj, string value) => obj.SetValue(HighlightProperty, value);

    public static Brush GetHighlightBrush(DependencyObject obj) => (Brush)obj.GetValue(HighlightBrushProperty);
    public static void SetHighlightBrush(DependencyObject obj, Brush value) => obj.SetValue(HighlightBrushProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        var text = GetText(tb) ?? string.Empty;
        var query = GetHighlight(tb) ?? string.Empty;
        var brush = GetHighlightBrush(tb);

        tb.Inlines.Clear();

        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        int pos = 0;
        while (pos < text.Length)
        {
            var idx = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                tb.Inlines.Add(new Run(text[pos..]));
                break;
            }

            if (idx > pos)
                tb.Inlines.Add(new Run(text[pos..idx]));

            tb.Inlines.Add(new Run(text[idx..(idx + query.Length)])
            {
                Foreground = brush,
                FontWeight = FontWeights.Bold
            });

            pos = idx + query.Length;
        }
    }
}
