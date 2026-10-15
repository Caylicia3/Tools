using Microsoft.Win32;

namespace DesktopTodo.App;

internal static partial class Dialogs
{
    internal static void Settings(PlannerWindow owner)
    {
        App.Instance.FlushViews();
        var p = Copies.Clone(owner.Store.Current.Settings);
        var palette = Palette.For(p);
        var w = new FormWindow(owner, "外观、提醒与数据", 550);
        bool syncing = true;
        var theme = w.Field("主题", new ComboBox { ItemsSource = Palette.All.Select(x => x.Name).ToArray(), SelectedItem = p.Theme });
        w.Hint("更换主题会更新下方三种全局颜色；单独指定了颜色的日程和任务保留原色。所有外观调整立即预览，保存后保留。");
        var background = Color(w, palette.Background, "背景色");
        var ink = Color(w, palette.Ink, "文字色");
        var accent = Color(w, palette.Accent, "强调色");
        Slider SliderField(string label, double minimum, double maximum, double value, string unit)
        {
            var slider = w.Field(label, new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 3) });
            System.Windows.Automation.AutomationProperties.SetName(slider, label);
            var number = new TextBlock
            {
                Text = Math.Round(value) + unit,
                Margin = new Thickness(0, 0, 0, 5)
            };
            w.Fields.Children.Add(number);
            slider.ValueChanged += (_, _) =>
            {
                number.Text = Math.Round(slider.Value) + unit;
            };
            return slider;
        }

        var opacity = SliderField("背景不透明度（文字保持清晰）", 1, 100, p.Opacity * 100, "%");
        var thickness = SliderField("任务条粗细（6～22）", 6, 22, p.TimelineBarHeight, "");
        var row = SliderField("任务行高（36～144，与粗细独立）", 36, 144, p.TimelineRowHeight, "");
        string ThemeColor(TextBox input, string name)
        {
            var value = input.Text.Trim();
            if (value == "")
                throw new InvalidDataException(name + "不能为空，请填写例如 #B7ABD4，或点击选色。");
            Validation.Color(value);
            return value;
        }

        void Preview()
        {
            if (syncing)
                return;
            p.Theme = theme.SelectedItem as string ?? Palette.All[0].Name;
            try
            {
                p.Background = ThemeColor(background, "背景色");
            }
            catch (InvalidDataException)
            {
            }

            try
            {
                p.Foreground = ThemeColor(ink, "文字色");
            }
            catch (InvalidDataException)
            {
            }

            try
            {
                p.Accent = ThemeColor(accent, "强调色");
            }
            catch (InvalidDataException)
            {
            }

            p.Opacity = opacity.Value / 100;
            p.TimelineBarHeight = thickness.Value;
            p.TimelineRowHeight = row.Value;
            App.Instance.PreviewAppearance(p);
        }

        void ThemeColors()
        {
            if (theme.SelectedItem is not string name)
                return;
            syncing = true;
            var a = Palette.All.Single(x => x.Name == name);
            background.Text = a.Background;
            ink.Text = a.Ink;
            accent.Text = a.Accent;
            syncing = false;
            Preview();
        }

        theme.SelectionChanged += (_, _) => ThemeColors();
        foreach (var input in new[]
        {
            background,
            ink,
            accent
        }

        )
            input.TextChanged += (_, _) => Preview();
        foreach (var slider in new[]
        {
            opacity,
            thickness,
            row
        }

        )
            slider.ValueChanged += (_, _) => Preview();
        var reset = new Button
        {
            Content = "恢复当前主题默认外观",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        reset.Click += (_, _) =>
        {
            syncing = true;
            opacity.Value = 95;
            thickness.Value = 11;
            row.Value = 36;
            syncing = false;
            ThemeColors();
        };
        w.Fields.Children.Add(reset);
        var desktop = w.Check("贴在桌面层", p.DesktopMode);
        var top = w.Check("始终置顶", p.Topmost);
        var notifications = w.Check("启用到期提醒", p.Notifications);
        var notificationMode = w.Field("提醒方式", new ComboBox { ItemsSource = new[] { "托盘提醒（推荐）", "系统通知" }, SelectedIndex = p.UseSystemNotifications ? 1 : 0, IsEnabled = p.Notifications });
        notifications.Click += (_, _) => notificationMode.IsEnabled = notifications.IsChecked == true;
        w.Hint("托盘提醒由右下角图标发出。系统通知需要向 Windows 注册通知入口，可能触发安全软件提示。保存后生效，测试按钮使用当前已保存的方式。");
        w.Hint("「—」最小化到任务栏，提醒继续运行。「×」退出整个程序和提醒。\n当前通知方式：" + App.Instance.NotificationStatus);
        var test = new Button
        {
            Content = "发送一条测试通知",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        test.Click += (_, _) => App.Instance.Notify("日程管理已就绪", "这是测试提醒。任务和节点可独立设置提前几天提醒。");
        w.Fields.Children.Add(test);
        w.Hint("数据目录：" + owner.Store.DirectoryPath + "\n每日启动自动备份；恢复前会另外备份现有数据。");
        var actions = new WrapPanel();
        w.Fields.Children.Add(actions);
        var backup = new Button
        {
            Content = "导出备份"
        };
        backup.Click += (_, _) => w.Try(() =>
        {
            var d = new SaveFileDialog
            {
                Title = "保存完整备份",
                Filter = "日程备份 (*.json)|*.json",
                FileName = "日程管理-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".backup.json",
                InitialDirectory = owner.Store.DirectoryPath
            };
            if (d.ShowDialog(w) == true)
            {
                owner.Store.Backup(d.FileName);
                MessageBox.Show(w, "备份已保存。", "备份完成");
            }
        });
        actions.Children.Add(backup);
        var restore = new Button
        {
            Content = "从备份恢复"
        };
        restore.Click += (_, _) => w.Try(() =>
        {
            var d = new OpenFileDialog
            {
                Title = "选择备份",
                Filter = "日程备份 (*.json)|*.json",
                InitialDirectory = owner.Store.DirectoryPath
            };
            if (d.ShowDialog(w) == true && MessageBox.Show(w, "恢复将替换当前日程、任务和设置，并清空撤销记录。\n恢复前会自动备份现有数据，是否继续？", "恢复备份", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                owner.Store.Restore(d.FileName);
                App.Instance.PreviewAppearance(null);
                App.Instance.SetDetached(owner.Store.Current.Settings.Detached, false);
                App.Instance.ApplyWindows();
                App.Instance.ConfigureNotifications();
                w.Close();
            }
        });
        actions.Children.Add(restore);
        var folder = new Button
        {
            Content = "打开数据文件夹"
        };
        folder.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", owner.Store.DirectoryPath) { UseShellExecute = true });
        actions.Children.Add(folder);
        var exit = new Button
        {
            Content = "退出整个程序",
            Foreground = PlannerWindow.Brush("#A43F4F")
        };
        exit.Click += (_, _) =>
        {
            w.Close();
            App.Instance.ExitApp();
        };
        w.Fields.Children.Add(exit);
        w.Commit(() =>
        {
            ThemeColor(background, "背景色");
            ThemeColor(ink, "文字色");
            ThemeColor(accent, "强调色");
            Preview();
            var a = Palette.All.First(x => x.Name == p.Theme);
            owner.Store.Update(s =>
            {
                var target = s.Settings;
                target.Theme = p.Theme;
                target.Background = p.Background.Equals(a.Background, StringComparison.OrdinalIgnoreCase) ? "" : p.Background;
                target.Foreground = p.Foreground.Equals(a.Ink, StringComparison.OrdinalIgnoreCase) ? "" : p.Foreground;
                target.Accent = p.Accent.Equals(a.Accent, StringComparison.OrdinalIgnoreCase) ? "" : p.Accent;
                target.Opacity = p.Opacity;
                target.TimelineBarHeight = p.TimelineBarHeight;
                target.TimelineRowHeight = p.TimelineRowHeight;
                target.DesktopMode = desktop.IsChecked == true;
                target.Topmost = top.IsChecked == true;
                target.Notifications = notifications.IsChecked == true;
                target.UseSystemNotifications = notificationMode.SelectedIndex == 1;
            });
            App.Instance.PreviewAppearance(null);
            App.Instance.ApplyWindows();
            App.Instance.ConfigureNotifications();
        });
        w.Closed += (_, _) => App.Instance.PreviewAppearance(null);
        syncing = false;
        w.ShowDialog();
    }
}
