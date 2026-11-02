namespace DesktopTodo;

internal sealed class ReminderWindow : Window
{
    private readonly StackPanel items = new();
    private readonly Dictionary<string, TodoItem> pending = new();
    private readonly Action<string> reveal;
    public ReminderWindow(Action<string> showTask)
    {
        reveal = showTask; Title = "待办提醒"; Width = 360; MaxHeight = 500; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.ToolWindow; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        ShowActivated = false; Topmost = true; Background = Ui.Paper; FontFamily = Ui.Font;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var header = Ui.Text("到时间了", 21); header.FontWeight = FontWeights.SemiBold; header.Margin = new Thickness(0, 0, 0, 16);
        panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer { Content = items, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(Ui.Button("关闭", Close));
        Content = panel;
        Loaded += (_, _) => { var area = SystemParameters.WorkArea; Left = area.Right - ActualWidth - 22; Top = area.Bottom - ActualHeight - 22; };
    }
    public void Add(IEnumerable<TodoItem> tasks)
    {
        foreach (var task in tasks) pending[task.Id] = task;
        Render();
    }
    public void Reconcile(IEnumerable<TodoItem> current)
    {
        var all = current.ToDictionary(t => t.Id);
        foreach (var (id, old) in pending.ToList())
        {
            if (!all.TryGetValue(id, out var task) || task.Completed || !task.Remind || task.Deadline != old.Deadline) pending.Remove(id);
            else pending[id] = task;
        }
        if (pending.Count == 0) Close(); else Render();
    }
    private void Render()
    {
        items.Children.Clear();
        foreach (var task in pending.Values)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            panel.Children.Add(Ui.Text(task.Text));
            var time = Ui.Text(task.Deadline?.ToLocalTime().ToString("M月d日 HH:mm") ?? "", 14, Ui.Muted);
            time.Margin = new Thickness(0, 5, 0, 8); panel.Children.Add(time);
            panel.Children.Add(Ui.Button("查看任务", () => reveal(task.Id)));
            items.Children.Add(panel);
        }
    }
}
