namespace DesktopTodo;

internal static class Ui
{
    public static readonly FontFamily Font = new("Noto Sans SC, Microsoft YaHei UI");
    public static readonly SolidColorBrush Ink = Brush("#232A30");
    public static readonly SolidColorBrush Muted = Brush("#7A8288");
    public static readonly SolidColorBrush Paper = Brush("#F4F6FB");
    public static readonly SolidColorBrush Accent = Brush("#2D7058");
    public static SolidColorBrush Brush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    public static TextBlock Text(string text, double size = 16, Brush? color = null) => new() {
        Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap
    };
    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0),
            Background = primary ? Accent : Brushes.White, Foreground = primary ? Brushes.White : Ink,
            BorderBrush = Brush("#DCE3DE"), BorderThickness = new Thickness(1), MinHeight = 38, FontSize = 15 };
        button.Click += (_, _) => action();
        return button;
    }
    public static MenuItem Menu(string text, Action action)
    {
        var item = new MenuItem { Header = text, Padding = new Thickness(10, 6, 16, 6), FontSize = 15 };
        item.Click += (_, e) => { e.Handled = true; action(); };
        return item;
    }
    public static ContextMenu Context(params object[] items)
    {
        var menu = new ContextMenu { FontFamily = Font, Background = Brushes.White, Foreground = Ink };
        foreach (var item in items) menu.Items.Add(item);
        return menu;
    }
    public static void Error(Window? owner, Exception error) =>
        MessageBox.Show(owner!, error.Message, "未能保存", MessageBoxButton.OK, MessageBoxImage.Error);
    public static bool Confirm(Window owner, string text) =>
        MessageBox.Show(owner, text, "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;
    public static Window Dialog(Window owner, string title, double width = 380)
    {
        return new Window { Owner = owner, Title = title, Width = width, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            Background = Paper, Foreground = Ink, FontFamily = Font, FontSize = 15,
            ShowInTaskbar = false, Topmost = owner.Topmost };
    }
    public static string? Prompt(Window owner, string title, string initial)
    {
        var window = Dialog(owner, title);
        var panel = new StackPanel { Margin = new Thickness(22) };
        var input = new TextBox { Text = initial, FontSize = 17, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 20) };
        string? result = null;
        void Save() { if (string.IsNullOrWhiteSpace(input.Text)) return; result = input.Text.Trim(); window.DialogResult = true; }
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button("取消", () => window.DialogResult = false));
        buttons.Children.Add(Button("保存", Save, true)); panel.Children.Add(buttons);
        window.Content = panel;
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Save(); e.Handled = true; } else if (e.Key == Key.Escape) window.DialogResult = false; };
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? result : null;
    }
    public static DateOnly? PickDate(Window owner, DateOnly initial)
    {
        var window = Dialog(owner, "选择日期", 330);
        var panel = new StackPanel { Margin = new Thickness(18) };
        var calendar = new Calendar { SelectedDate = initial.ToDateTime(TimeOnly.MinValue), DisplayDate = initial.ToDateTime(TimeOnly.MinValue) };
        panel.Children.Add(calendar);
        DateOnly? result = null;
        panel.Children.Add(Button("打开", () => { if (calendar.SelectedDate is { } date) { result = DateOnly.FromDateTime(date); window.DialogResult = true; } }, true));
        window.Content = panel;
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) window.DialogResult = false; };
        window.ShowDialog(); return result;
    }
}
