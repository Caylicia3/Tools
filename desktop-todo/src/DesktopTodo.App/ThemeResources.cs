namespace DesktopTodo.App;

internal static class ThemeResources
{
    internal static void Apply(Preferences settings)
    {
        var p = Palette.For(settings);
        var r = Application.Current.Resources;
        r["PanelBackground"] = PlannerWindow.Brush(p.Background);
        r["PanelForeground"] = PlannerWindow.Brush(p.Ink);
        r["PanelAccent"] = PlannerWindow.Brush(p.Accent);
        r["AccentInk"] = PlannerWindow.Brush(PlannerWindow.InkFor(p.Accent));
        r["PanelBorder"] = PlannerWindow.Brush(p.Ink, .22);
        r["MutedForeground"] = PlannerWindow.Brush(p.Ink, .78);
        r["FieldBackground"] = PlannerWindow.Brush(p.Background);
        r["SelectionBackground"] = PlannerWindow.Brush(p.Accent, .3);
    }
}
