using System.Globalization;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DesktopTodo;

internal sealed class MainWindow : Window
{
    private readonly DesktopApplication app;
    public BoardViewModel Model { get; }
    private readonly StackPanel body = new();
    private readonly ScrollViewer scroll;
    private readonly TextBlock title;
    private readonly DispatcherTimer geometryTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Dictionary<string, TaskRow> rows = new();
    private bool ready, exiting, fitting;
    public MainWindow(DesktopApplication application, TodoStore store)
    {
        app = application; Model = new(store);
        var settings = Model.Settings;
        if (settings.LayoutVersion < 2)
        {
            settings = settings with { Width = 340, Height = 100, LayoutVersion = 2 };
            store.SaveSettings(settings); Model.Refresh();
        }
        Title = "今日计划"; Width = Math.Max(300, settings.Width); Height = 100;
        MinWidth = 300; MinHeight = 92; MaxHeight = Math.Min(480, SystemParameters.WorkArea.Height * .7);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        Background = Ui.Paper; Foreground = Ui.Ink; FontFamily = Ui.Font;
        Topmost = settings.Topmost; ShowActivated = true; ShowInTaskbar = false;
        if (settings.Left.HasValue && settings.Top.HasValue) { Left = settings.Left.Value; Top = settings.Top.Value; WindowStartupLocation = WindowStartupLocation.Manual; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(8) });
        var outer = new Border { BorderBrush = Ui.Brush("#DDE2E5"), BorderThickness = new Thickness(1), Background = Ui.Paper, CornerRadius = new CornerRadius(8) };
        var grid = new Grid { Margin = new Thickness(14, 0, 14, 0) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        grid.RowDefinitions.Add(new RowDefinition());
        var header = new Grid { Background = Brushes.Transparent };
        title = Ui.Text("", 21, Ui.Brush("#334B72")); title.FontWeight = FontWeights.Bold; title.VerticalAlignment = VerticalAlignment.Center;
        title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap;
        title.HorizontalAlignment = HorizontalAlignment.Left;
        title.MouseLeftButtonDown += (_, e) => e.Handled = true;
        header.Children.Add(title);
        header.MouseLeftButtonDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) { DragMove(); e.Handled = true; } };
        grid.Children.Add(header);
        scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll); outer.Child = grid; Content = outer;
        ContextMenu = MainMenu();
        Model.PropertyChanged += (_, _) => Render();
        Loaded += (_, _) => { EnsureVisible(); ready = true; Render(); };
        Closing += (_, e) => { if (!exiting) { e.Cancel = true; HideToTray(); } };
        LocationChanged += (_, _) => ScheduleGeometry();
        SizeChanged += (_, e) => { if (e.WidthChanged) Dispatcher.BeginInvoke(new Action(FitHeight)); ScheduleGeometry(); };
        geometryTimer.Tick += (_, _) => { geometryTimer.Stop(); SaveGeometry(); };
        Render();
    }
    private ContextMenu MainMenu()
    {
        var pin = Ui.Menu("取消置顶", () => ChangeSettings(Model.Settings with { Topmost = !Topmost }));
        var menu = Ui.Context(
            Ui.Menu("编辑全部计划", OpenPlanner),
            Ui.Menu("新增任务", () => EditTask(null, Model.SelectedDate ?? Today)),
            Ui.Menu("选择日期", () => {
                var date = Ui.PickDate(this, Model.SelectedDate ?? Today);
                if (date.HasValue) { Model.SelectedDate = date.Value == Today ? null : date; Refresh(); }
            }),
            Ui.Menu("返回今天", () => { Model.SelectedDate = null; Refresh(); }),
            new Separator(), Ui.Menu("设置", OpenSettings), pin,
            new Separator(), Ui.Menu("收起窗口", HideToTray), Ui.Menu("退出", () => app.ExitApplication()));
        menu.Opened += (_, _) => pin.Header = Topmost ? "取消置顶" : "置顶";
        return menu;
    }
    internal void OpenPlanner()
    {
        var editor = new PlanEditorWindow(this, Model.Store, Model.SelectedDate ?? Today, () => { Refresh(); app.CheckReminders(); });
        editor.ShowDialog();
    }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    public void Refresh() => Model.Refresh();
    private void Render()
    {
        var offset = scroll.VerticalOffset;
        var date = Model.SelectedDate ?? Today;
        title.Text = date == Today ? date.ToString("M月d日") : date.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"));
        Title = date == Today ? "今日计划" : date.ToString("M月d日") + "计划";
        body.Children.Clear(); rows.Clear();
        // Current date comes first. Legacy group assignments are deliberately ignored.
        foreach (var section in Model.Sections.OrderBy(s => s.Date.HasValue ? 0 : 1))
        {
            if (!section.Date.HasValue)
            {
                var label = Ui.Text("待补办", 14, Ui.Muted);
                label.FontWeight = FontWeights.Medium; label.Margin = new Thickness(0, 12, 0, 4);
                body.Children.Add(label);
            }
            var tasks = section.Date.HasValue
                ? section.Tasks.OrderBy(t => t.Completed).ThenBy(t => t.Position).ToList()
                : section.Tasks.OrderBy(t => t.Date).ThenBy(t => t.Position).ToList();
            for (int i = 0; i < tasks.Count; i++)
            {
                var item = tasks[i];
                var row = new TaskRow(this, item, TaskMenu(item), () => Mutate(() => Model.Store.Complete(item.Id, true)), !section.Date.HasValue, i + 1);
                rows[item.Id] = row; body.Children.Add(row);
            }
        }
        scroll.ScrollToVerticalOffset(offset);
        FitHeight(); app.RefreshReminderWindow();
    }
    private void FitHeight()
    {
        if (fitting) return;
        fitting = true;
        try
        {
            double width = Math.Max(260, Width - 30);
            body.Measure(new Size(width, double.PositiveInfinity));
            double content = body.DesiredSize.Height;
            if (content + 54 > MaxHeight)
            { body.Measure(new Size(width - SystemParameters.VerticalScrollBarWidth, double.PositiveInfinity)); content = body.DesiredSize.Height; }
            Height = Math.Clamp(Math.Ceiling(content + 54), MinHeight, MaxHeight);
        }
        finally { fitting = false; }
    }
    private ContextMenu TaskMenu(TodoItem item)
    {
        var menu = Ui.Context(Ui.Menu("编辑", () => EditTask(item, item.Date)),
            Ui.Menu("修改日期", () => {
                var date = Ui.PickDate(this, item.Date);
                if (date.HasValue) Mutate(() => Model.Store.SaveTask(item with { Date = date.Value, GroupId = null }));
            }),
            Ui.Menu("上移", () => Move(item, -1)), Ui.Menu("下移", () => Move(item, 1)));
        if (item.Completed) menu.Items.Add(Ui.Menu("恢复未完成", () => Mutate(() => Model.Store.Complete(item.Id, false))));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("删除", () => { if (Ui.Confirm(this, $"删除“{item.Text}”？")) Mutate(() => Model.Store.DeleteTask(item.Id)); }));
        return menu;
    }
    private void Move(TodoItem item, int direction)
    {
        var siblings = Model.Tasks.Where(t => t.Date == item.Date && t.Completed == item.Completed).OrderBy(t => t.Position).ToList();
        int target = siblings.FindIndex(t => t.Id == item.Id) + direction;
        if (target >= 0 && target < siblings.Count) Mutate(() => Model.Store.Reorder(item, siblings[target]));
    }
    private void EditTask(TodoItem? item, DateOnly date)
    {
        bool again;
        do
        {
            TodoItem draft = item ?? new(Guid.NewGuid().ToString("N"), "", date, null, Model.NextPosition);
            TodoItem? saved = null;
            var editor = new TaskEditor(this, draft, item == null, updated => {
                bool ok = Mutate(() => saved = Model.Store.SaveTask(updated));
                if (ok && saved != null) date = saved.Date;
                return ok;
            });
            editor.ShowDialog(); again = item == null && editor.AddAnother;
        } while (again);
    }
    public bool Mutate(Action action)
    {
        try { action(); Refresh(); app.CheckReminders(); return true; }
        catch (Exception error) { Ui.Error(this, error); return false; }
    }
    private bool ChangeSettings(UserSettings settings)
    {
        var previous = Model.Store.Settings();
        try
        {
            if (settings.Autostart != previous.Autostart) app.ConfigureAutostart(settings.Autostart);
            Model.Store.SaveSettings(settings with { Width = Width, Height = Height, Left = Left, Top = Top });
            Topmost = settings.Topmost; Refresh(); return true;
        }
        catch (Exception error)
        {
            if (settings.Autostart != previous.Autostart)
                try { app.ConfigureAutostart(previous.Autostart); } catch (Exception rollback) { app.Log(rollback); }
            Ui.Error(this, error); return false;
        }
    }
    private void OpenSettings()
    {
        var window = Ui.Dialog(this, "设置", 300);
        var panel = new StackPanel { Margin = new Thickness(18) };
        var startup = new CheckBox { Content = "开机自动展开", IsChecked = Model.Settings.Autostart, Margin = new Thickness(0, 0, 0, 16) };
        var pinned = new CheckBox { Content = "窗口置顶", IsChecked = Topmost, Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(startup); panel.Children.Add(pinned);
        panel.Children.Add(Ui.Button("保存", () => {
            if (ChangeSettings(Model.Settings with { Autostart = startup.IsChecked == true, Topmost = pinned.IsChecked == true })) window.Close();
        }, true));
        window.Content = panel; window.ShowDialog();
    }
    public void Reveal(string? taskId = null)
    {
        if (taskId != null)
        {
            var task = Model.Store.Tasks().FirstOrDefault(t => t.Id == taskId);
            if (task != null) Model.SelectedDate = task.Date <= Today ? null : task.Date;
        }
        Show(); WindowState = WindowState.Normal; EnsureVisible(); Refresh(); Activate();
        if (taskId != null && rows.TryGetValue(taskId, out var row))
            Dispatcher.BeginInvoke(new Action(() => { row.BringIntoView(); row.Focus(); }));
    }
    private void HideToTray() { SaveGeometry(); Hide(); }
    public void CloseForExit() { SaveGeometry(); exiting = true; Close(); }
    private void ScheduleGeometry() { if (!ready || WindowState != WindowState.Normal) return; geometryTimer.Stop(); geometryTimer.Start(); }
    private void SaveGeometry()
    {
        if (!ready || WindowState != WindowState.Normal) return;
        try { Model.Store.SaveSettings(Model.Store.Settings() with { Width = Width, Height = Height, Left = Left, Top = Top, Topmost = Topmost }); }
        catch (Exception ex) { app.Log(ex); }
    }
    private void EnsureVisible()
    {
        var rect = new Rect(Left, Top, Width, Height);
        var dpi = VisualTreeHelper.GetDpi(this);
        bool visible = System.Windows.Forms.Screen.AllScreens.Any(s => {
            var a = s.WorkingArea;
            var screen = new Rect(a.X / dpi.DpiScaleX, a.Y / dpi.DpiScaleY, a.Width / dpi.DpiScaleX, a.Height / dpi.DpiScaleY);
            var overlap = Rect.Intersect(screen, rect);
            return !overlap.IsEmpty && overlap.Width >= 120 && overlap.Height >= 60;
        });
        if (!visible || double.IsNaN(Left) || double.IsNaN(Top))
        { var area = SystemParameters.WorkArea; Left = area.Right - Width - 24; Top = area.Top + 24; }
    }

    private TaskRow? sortingRow;
    private List<TaskRow> sortPeers = new();
    private Dictionary<TaskRow,double> sortTops = new();
    private double sortStart, sortScrollStart;
    private int sortDestination;
    internal void BeginSort(TaskRow row, double y)
    {
        sortingRow=row; sortStart=y; sortScrollStart=scroll.VerticalOffset;
        sortPeers=rows.Values.Where(r=>r.Item.Date==row.Item.Date && r.Item.Completed==row.Item.Completed)
            .OrderBy(r=>r.Item.Position).ToList();
        sortTops=sortPeers.ToDictionary(r=>r,r=>System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(r).Top);
        sortDestination=sortPeers.IndexOf(row);
        Panel.SetZIndex(row,20); row.Opacity=.93;
        row.Effect=new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=14, ShadowDepth=3, Opacity=.18 };
    }
    internal void UpdateSort(double y)
    {
        if(sortingRow==null)return;
        var pointer=Mouse.GetPosition(scroll);
        if(pointer.Y<28) scroll.ScrollToVerticalOffset(scroll.VerticalOffset-12);
        else if(pointer.Y>scroll.ActualHeight-28) scroll.ScrollToVerticalOffset(scroll.VerticalOffset+12);
        UpdateSortAt(y,Mouse.GetPosition(body).Y);
    }
    internal void UpdateSortAt(double y,double bodyY)
    {
        if(sortingRow==null)return;
        var others=sortPeers.Where(r=>r!=sortingRow).ToList();
        sortDestination=others.Count(r=>bodyY>sortTops[r]+r.ActualHeight/2);
        var ordered=others.ToList(); ordered.Insert(sortDestination,sortingRow);
        double top=sortTops.Values.Min();
        foreach(var row in ordered)
        {
            if(row==sortingRow) row.OffsetForSort(y-sortStart+scroll.VerticalOffset-sortScrollStart,false);
            else row.OffsetForSort(top-sortTops[row],true);
            row.ShowInsertion(false,false);
            top+=row.ActualHeight+row.Margin.Bottom;
        }
        if(others.Count>0)
        {
            var marker=sortDestination<others.Count?others[sortDestination]:others[^1];
            marker.ShowInsertion(true,sortDestination==others.Count);
        }
    }
    internal void EndSort(bool commit)
    {
        if(sortingRow==null)return;
        var source=sortingRow; var old=sortPeers.IndexOf(source);
        var order=sortPeers.Where(r=>r!=source).ToList(); order.Insert(sortDestination,source);
        sortingRow=null;
        foreach(var row in sortPeers)
        {
            row.ShowInsertion(false,false); row.OffsetForSort(0,true); row.Opacity=1; row.Effect=null; Panel.SetZIndex(row,0);
        }
        if(commit && old!=sortDestination)
        {
            var positions=rows.ToDictionary(p=>p.Key,p=>p.Value.TranslatePoint(new Point(0,0),body).Y);
            if(Mutate(()=>Model.Store.SetTaskOrder(order.Select(r=>r.Item.Id).ToList())))
            {
                body.UpdateLayout();
                foreach(var (id,row) in rows)
                    if(positions.TryGetValue(id,out var previous))
                    { var now=row.TranslatePoint(new Point(0,0),body).Y; row.OffsetForSort(previous-now,false); row.OffsetForSort(0,true); }
            }
        }
    }
}
