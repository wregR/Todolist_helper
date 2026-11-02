using System.Globalization;
using System.Windows.Threading;

namespace DesktopTodo;

internal sealed class TaskEditor : Window
{
    private readonly TextBox text;
    private readonly DatePicker date;
    private readonly DatePicker deadline;
    private readonly TextBox time;
    private readonly CheckBox remind;
    private readonly TextBlock error;
    private TodoItem original;
    private readonly Func<TodoItem, bool> save;
    private bool finished, initializing = true;
    public bool AddAnother { get; private set; }

    public TaskEditor(Window owner, TodoItem item, bool isNew, Func<TodoItem, bool> saveItem)
    {
        original = item; save = saveItem;
        Owner = owner; Title = isNew ? "新增任务" : "编辑任务";
        Width = 340; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Topmost = owner.Topmost;
        FontFamily = Ui.Font; FontSize = 15; Background = Ui.Paper;
        var panel = new StackPanel { Margin = new Thickness(16) };
        text = new TextBox { Text = item.Text, FontSize = 15, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(10), MinHeight = 56 };
        panel.Children.Add(text);
        panel.Children.Add(Label("日期"));
        date = new DatePicker { SelectedDate = item.Date.ToDateTime(TimeOnly.MinValue), FontSize = 15 };
        panel.Children.Add(date);
        var advanced = new Expander { Header = "高级选项", Margin = new Thickness(0, 18, 0, 12), IsExpanded = false };
        var options = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        options.Children.Add(Ui.Text("截止时间", 15));
        var line = new Grid { Margin = new Thickness(0, 7, 0, 10) };
        line.ColumnDefinitions.Add(new ColumnDefinition()); line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        deadline = new DatePicker { SelectedDate = item.Deadline?.LocalDateTime.Date, FontSize = 15, Margin = new Thickness(0, 0, 8, 0) };
        time = new TextBox { Text = item.Deadline?.ToLocalTime().ToString("HH:mm") ?? "20:00", FontSize = 15, Padding = new Thickness(6) };
        Grid.SetColumn(time, 1); line.Children.Add(deadline); line.Children.Add(time); options.Children.Add(line);
        remind = new CheckBox { Content = "到时提醒", IsChecked = item.Remind, IsEnabled = item.Deadline.HasValue, FontSize = 15 };
        options.Children.Add(remind);
        deadline.SelectedDateChanged += (_, _) => {
            remind.IsEnabled = deadline.SelectedDate.HasValue;
            if (!initializing) remind.IsChecked = deadline.SelectedDate.HasValue;
        };
        advanced.Content = options; panel.Children.Add(advanced);
        error = Ui.Text("", 14, Ui.Brush("#AC403D")); error.Visibility = Visibility.Collapsed; panel.Children.Add(error);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(Ui.Button("取消", Cancel));
        if (isNew) actions.Children.Add(Ui.Button("保存并继续", () => Commit(true)));
        actions.Children.Add(Ui.Button("保存", () => Commit(false), true));
        panel.Children.Add(actions); Content = panel;
        PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
            else if (e.Key == Key.Enter && !date.IsDropDownOpen && !deadline.IsDropDownOpen)
            { e.Handled = true; Commit(false); }
        };
        Loaded += (_, _) => { text.Focus(); text.SelectAll(); };
        // Commit when leaving the editor, not when moving between its date/time fields.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {
            if (!finished && IsVisible && !IsActive && !date.IsDropDownOpen && !deadline.IsDropDownOpen &&
                error.Visibility != Visibility.Visible && !string.IsNullOrWhiteSpace(text.Text)) Commit(false, false);
        }));
        Closing += (_, _) => finished = true;
        initializing = false;
    }
    private static TextBlock Label(string value) { var label = Ui.Text(value, 15); label.Margin = new Thickness(0, 15, 0, 6); return label; }
    private void Cancel() { finished = true; DialogResult = false; }
    private void Commit(bool another, bool close = true)
    {
        if (finished) return;
        try
        {
            if (string.IsNullOrWhiteSpace(text.Text)) throw new ArgumentException("请输入任务内容。");
            if (date.SelectedDate is null) throw new ArgumentException("请选择日期。");
            DateTimeOffset? due = null;
            if (deadline.SelectedDate is { } dueDay)
            {
                if (!TimeOnly.TryParseExact(time.Text.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tod))
                    throw new ArgumentException("时间请使用 24 小时制，例如 20:30。");
                var local = DateTime.SpecifyKind(dueDay.Date + tod.ToTimeSpan(), DateTimeKind.Unspecified);
                if (TimeZoneInfo.Local.IsInvalidTime(local)) throw new ArgumentException("这个时间在本地时区中不存在。");
                due = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            }
            var updated = original with { Text = text.Text.Trim(), Date = DateOnly.FromDateTime(date.SelectedDate.Value),
                GroupId = null, Deadline = due, Remind = remind.IsChecked == true && due.HasValue };
            finished = true;
            if (!save(updated)) { finished = false; return; }
            original = updated; AddAnother = another; if (close) DialogResult = true; else finished = false;
        }
        catch (Exception ex) { finished = false; error.Text = ex.Message; error.Visibility = Visibility.Visible; }
    }
}
