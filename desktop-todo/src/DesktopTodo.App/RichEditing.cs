using System.Windows.Documents;
using System.Globalization;

namespace DesktopTodo.App;

internal static class RichEditing
{
    internal static RichText Read(RichTextBox box)
    {
        var result = new RichText();
        void Add(string text, TextElement element)
        {
            var brush = element.Foreground as SolidColorBrush;
            result.Runs.Add(new()
            {
                Text = text,
                Size = Math.Clamp(element.FontSize * .75, 8, 72),
                Color = brush == null ? "#333333" : $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
            });
        }
        void Inlines(InlineCollection inlines)
        {
            foreach (var inline in inlines)
                if (inline is Run run)
                    Add(run.Text, run);
                else if (inline is LineBreak line)
                    Add("\n", line);
                else if (inline is Span span)
                    Inlines(span.Inlines);
        }
        var paragraphs = box.Document.Blocks.OfType<Paragraph>().ToList();
        for (int i = 0; i < paragraphs.Count; i++)
        {
            if (i > 0)
                Add("\n", paragraphs[i]);
            Inlines(paragraphs[i].Inlines);
        }
        result.Normalize();
        return result;
    }

    internal static void Write(RichTextBox box, RichText text)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(2), FontSize = 11 * 4d / 3, FontFamily = new FontFamily("Microsoft YaHei UI"), Foreground = PlannerWindow.Brush("#333333") };
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        doc.Blocks.Add(paragraph);
        foreach (var part in text.Runs)
        {
            var lines = part.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    paragraph = new Paragraph { Margin = new Thickness(0), FontSize = part.Size * 4d / 3, Foreground = PlannerWindow.Brush(part.Color) };
                    doc.Blocks.Add(paragraph);
                }
                if (lines[i].Length > 0)
                    paragraph.Inlines.Add(new Run(lines[i]) { FontSize = part.Size * 4d / 3, Foreground = PlannerWindow.Brush(part.Color) });
            }
        }
        box.Document = doc;
    }

    internal static RichTextBox Editor(string name)
    {
        var box = new RichTextBox
        {
            Background = Brushes.Transparent,
            Foreground = PlannerWindow.Brush("#333333"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            Margin = new Thickness(0),
            MinHeight = 30,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            AcceptsTab = true,
            AllowDrop = true,
            IsUndoEnabled = true,
            UndoLimit = 100
        };
        System.Windows.Automation.AutomationProperties.SetName(box, name);
        Write(box, new());
        // Allow the checklist parent to receive internal reorder drops; reject external objects.
        box.PreviewDragOver += (_, e) => { if (!e.Data.GetDataPresent("DesktopTodo.Checklist")) { e.Effects = DragDropEffects.None; e.Handled = true; } };
        box.PreviewDrop += (_, e) => { if (!e.Data.GetDataPresent("DesktopTodo.Checklist")) e.Handled = true; };
        // Only text is accepted from external clipboard formats. Never deserialize arbitrary XAML.
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
                e.DataObject = new DataObject(DataFormats.UnicodeText, e.DataObject.GetData(DataFormats.UnicodeText));
            else
                e.CancelCommand();
        });
        return box;
    }

    internal static string Fingerprint(RichText text) => System.Text.Json.JsonSerializer.Serialize(text.Runs, Copies.Options);

    internal static bool ChooseColor(Window owner, string initial, Action<string> preview, out string selected)
    {
        var w = new FormWindow(owner, "选择颜色", 390);
        var colors = new WrapPanel();
        var palette = Palette.For(App.Instance.Appearance);
        var input = Dialogs.Color(w, initial, "颜色（# 后接 6 位十六进制字符）");
        foreach (var color in new[] { palette.Background, palette.Ink, palette.Accent }.Concat(palette.Colors).Concat(new[] { "#FFFFFF", "#000000" }).Distinct())
        {
            var button = new Button { Width = 30, Height = 30, Margin = new Thickness(3), Padding = new Thickness(0), Background = PlannerWindow.Brush(color), ToolTip = color, Content = " " };
            button.Click += (_, _) => input.Text = color;
            colors.Children.Add(button);
        }
        w.Fields.Children.Insert(0, colors);
        input.TextChanged += (_, _) =>
        {
            try
            {
                if (input.Text.Trim().Length == 7)
                {
                    Validation.Color(input.Text.Trim());
                    preview(input.Text.Trim());
                }
            }
            catch (InvalidDataException) { }
        };
        w.Commit(() =>
        {
            if (input.Text.Trim().Length != 7)
                throw new InvalidDataException("请填写完整颜色，例如 #B7ABD4。");
            Validation.Color(input.Text.Trim());
        });
        bool accepted = w.ShowDialog() == true;
        selected = accepted ? input.Text.Trim().ToUpperInvariant() : initial;
        preview(selected);
        return accepted;
    }

    internal static bool ChooseSize(Window owner, double initial, Action<double> preview)
    {
        var w = new FormWindow(owner, "调整字号", 330);
        var input = w.Input("字号（8～72 pt）", initial.ToString("0.##", CultureInfo.InvariantCulture));
        var presets = new WrapPanel();
        foreach (int size in new[] { 8, 11, 14, 18, 24, 36, 48, 72 })
        {
            var b = new Button { Content = size.ToString(), Padding = new Thickness(8, 4, 8, 4) };
            b.Click += (_, _) => input.Text = size.ToString();
            presets.Children.Add(b);
        }
        w.Fields.Children.Add(presets);
        double Value()
        {
            if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || !double.IsFinite(n) || n < 8 || n > 72)
                throw new InvalidDataException("字号需要在 8～72 pt 之间，例如 11 或 14。");
            return n;
        }
        input.TextChanged += (_, _) => { try { preview(Value()); } catch (InvalidDataException) { } };
        w.Commit(() => preview(Value()));
        return w.ShowDialog() == true;
    }
}
