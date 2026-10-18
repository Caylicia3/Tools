namespace DesktopTodo.Core;

public sealed class TextRun
{
    public string Text { get; set; } = "";
    public double Size { get; set; } = 11;
    public string Color { get; set; } = "#333333";
}

public sealed class RichText
{
    public List<TextRun> Runs { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string PlainText => string.Concat(Runs.Select(r => r.Text));
    public static RichText From(string text) => new() { Runs = [new() { Text = text }] };
    public void Normalize()
    {
        var result = new List<TextRun>();
        foreach (var run in Runs)
        {
            if (run.Text.Length == 0)
                continue;
            var previous = result.LastOrDefault();
            if (previous != null && previous.Size == run.Size && previous.Color == run.Color)
                previous.Text += run.Text;
            else
                result.Add(new()
                {
                    Text = run.Text,
                    Size = run.Size,
                    Color = run.Color
                });
        }
        Runs = result;
    }
}

public sealed class ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public RichText Title { get; set; } = new();
    public RichText Notes { get; set; } = new();
    public bool Completed
    {
        get; set;
    }
}

public sealed class SidebarContent
{
    public List<ChecklistItem> Items { get; set; } = [];
    public RichText Note { get; set; } = new();
}

public sealed class SidebarPreferences
{
    public double Width { get; set; } = 320;
    public double Split { get; set; } = .5;
    public double ListZoom { get; set; } = 1;
    public double NoteZoom { get; set; } = 1;
    public string ListBackground { get; set; } = "#FFFFFF";
    public string NoteBackground { get; set; } = "#FFFFFF";
    public bool ShowCompleted { get; set; } = true;
    public double Left { get; set; } = 200;
    public double Top { get; set; } = 100;
    public double FloatingWidth { get; set; } = 360;
    public double FloatingHeight { get; set; } = 640;
}

public sealed class SidebarState
{
    public int Version { get; set; } = 1;
    public SidebarContent Content { get; set; } = new();
    public SidebarPreferences Settings { get; set; } = new();
}

public static class SidebarValidation
{
    static void Check(bool valid, string message)
    {
        if (!valid)
            throw new InvalidDataException(message);
    }
    public static void Text(RichText text)
    {
        Check(text != null && text.Runs != null, "文字内容缺失。");
        foreach (var run in text!.Runs!)
        {
            Check(run != null && run.Text != null, "文字段内容缺失。");
            Check(double.IsFinite(run!.Size) && run.Size >= 8 && run.Size <= 72, "字号需要在 8～72 pt 之间。");
            Check(!string.IsNullOrEmpty(run.Color), "文字颜色不能为空。");
            Validation.Color(run.Color);
        }
    }
    public static void State(SidebarState state)
    {
        Check(state != null && state.Version == 1 && state.Content != null && state.Settings != null, "侧栏数据版本或内容无效。");
        var c = state!.Content!;
        Check(c.Items != null, "待办清单缺失。");
        var ids = new HashSet<string>();
        foreach (var item in c.Items!)
        {
            Check(item != null && !string.IsNullOrWhiteSpace(item.Id) && ids.Add(item.Id), "待办标识重复或缺失。");
            Text(item!.Title);
            Text(item.Notes);
            Check(!string.IsNullOrWhiteSpace(item.Title.PlainText), "待办标题不能为空。");
        }
        Text(c.Note);
        var p = state.Settings!;
        Check(double.IsFinite(p.Width) && p.Width >= 240 && p.Width <= 640, "侧栏宽度需要在 240～640 之间。");
        Check(double.IsFinite(p.Split) && p.Split >= .15 && p.Split <= .85, "侧栏分区比例无效。");
        Check(new[] { p.ListZoom, p.NoteZoom }.All(x => double.IsFinite(x) && x >= .5 && x <= 2), "侧栏缩放需要在 50%～200% 之间。");
        Check(new[] { p.Left, p.Top, p.FloatingWidth, p.FloatingHeight }.All(double.IsFinite) && p.FloatingWidth >= 240 && p.FloatingHeight >= 290, "独立清单窗口尺寸无效。");
        Check(!string.IsNullOrEmpty(p.ListBackground) && !string.IsNullOrEmpty(p.NoteBackground), "书写区背景色不能为空。");
        Validation.Color(p.ListBackground);
        Validation.Color(p.NoteBackground);
    }
}
