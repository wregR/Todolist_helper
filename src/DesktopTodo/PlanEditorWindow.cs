using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Threading;

namespace DesktopTodo;

internal sealed class PlanEditorWindow : Window
{
    private readonly TodoStore store;
    private readonly Action saved;
    private readonly List<TodoItem> baseline;
    private readonly Dictionary<string, string> originalNotes;
    private readonly Dictionary<string, string> notes;
    private readonly List<PlanRow> drafts;
    private readonly ObservableCollection<PlanRow> visible = new();
    private readonly DataGrid table;
    private readonly TextBox monthNotes;
    private readonly TextBlock monthLabel;
    private readonly TextBlock error;
    private DateOnly month;
    private bool switching, committed;
    private string MonthKey => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public PlanEditorWindow(Window owner, TodoStore repository, DateOnly initial, Action afterSave)
    {
        store = repository; saved = afterSave;
        baseline = store.Tasks(); originalNotes = store.MonthPlans(); notes = new(originalNotes);
        drafts = baseline.Select(t => new PlanRow(t)).ToList();
        month = new DateOnly(initial.Year, initial.Month, 1);
        Owner = owner; Title = "编辑全部计划"; Width = 760; Height = 660; MinWidth = 620; MinHeight = 470;
        MaxHeight = SystemParameters.WorkArea.Height - 30; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Ui.Paper; FontFamily = Ui.Font; FontSize = 15; Topmost = owner.Topmost; ShowInTaskbar = false;
        var root = new Grid { Margin = new Thickness(20, 10, 20, 14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        monthLabel = Ui.Text("", 22); monthLabel.FontWeight = FontWeights.Bold; monthLabel.VerticalAlignment = VerticalAlignment.Center;
        nav.Children.Add(monthLabel);
        nav.Children.Add(Ui.Button("上月", () => ChangeMonth(month.AddMonths(-1))));
        nav.Children.Add(Ui.Button("下月", () => ChangeMonth(month.AddMonths(1))));
        nav.Children.Add(Ui.Button("本月", () => ChangeMonth(DateOnly.FromDateTime(DateTime.Today))));
        toolbar.Children.Add(nav); root.Children.Add(toolbar);

        var monthly = new Expander { Header = "月计划", IsExpanded = true, Margin = new Thickness(0, 0, 0, 12),
            FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#334B72") };
        monthNotes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 58, MaxHeight = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10), FontSize = 16,
            FontWeight = FontWeights.Normal, Background = Brushes.White, BorderBrush = Ui.Brush("#DDE3EF"),
            Margin = new Thickness(0, 8, 0, 0) };
        monthNotes.TextChanged += (_, _) => { if (!switching) notes[MonthKey] = monthNotes.Text; };
        monthly.Content = monthNotes; Grid.SetRow(monthly, 1); root.Children.Add(monthly);

        var center = new Grid();
        center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); center.RowDefinitions.Add(new RowDefinition());
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-8, 0, 0, 9) };
        actions.Children.Add(Ui.Button("添加任务", AddRow));
        actions.Children.Add(Ui.Button("粘贴多日计划", Paste));
        actions.Children.Add(Ui.Button("移除选中", RemoveRows));
        center.Children.Add(actions);
        table = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow, HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Ui.Brush("#E9EDF4"),
            BorderBrush = Ui.Brush("#DDE3EF"), BorderThickness = new Thickness(1), Background = Brushes.White,
            RowBackground = Brushes.White, AlternatingRowBackground = Ui.Brush("#F4F7FD"), FontSize = 15,
            MinRowHeight = 39, ItemsSource = visible, EnableRowVirtualization = true };
        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Brush("#E9EFF9")));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Brush("#334B72")));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        table.ColumnHeaderStyle = headerStyle;
        table.Columns.Add(new DataGridTextColumn { Header = "", Binding = new Binding(nameof(PlanRow.CompletedMark)), Width = 28, IsReadOnly = true });
        var dateDisplay = new FrameworkElementFactory(typeof(TextBlock));
        dateDisplay.SetBinding(TextBlock.TextProperty, new Binding(nameof(PlanRow.Day)) { StringFormat = "{0:M月d日}" });
        dateDisplay.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        dateDisplay.SetValue(TextBlock.MarginProperty, new Thickness(7));
        dateDisplay.SetValue(TextBlock.ForegroundProperty, Ui.Brush("#4675D5"));
        var dateEdit = new FrameworkElementFactory(typeof(DatePicker));
        dateEdit.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(PlanRow.Day)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        table.Columns.Add(new DataGridTemplateColumn { Header = "日期", Width = 132, CellTemplate = new DataTemplate { VisualTree = dateDisplay },
            CellEditingTemplate = new DataTemplate { VisualTree = dateEdit } });
        var content = new FrameworkElementFactory(typeof(TextBox));
        content.SetBinding(TextBox.TextProperty, new Binding(nameof(PlanRow.Text)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        content.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        content.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        content.SetValue(Control.PaddingProperty, new Thickness(8));
        content.SetValue(Control.FontSizeProperty, 16d); content.SetValue(Control.FontWeightProperty, FontWeights.Medium);
        content.SetValue(TextBox.TextWrappingProperty, TextWrapping.Wrap);
        table.Columns.Add(new DataGridTemplateColumn { Header = "任务", MinWidth = 220, Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new DataTemplate { VisualTree = content } });
        table.Columns.Add(new DataGridTextColumn { Header = "截止", Width = 72, IsReadOnly = true, Binding = new Binding(nameof(PlanRow.DeadlineLabel)) });
        table.ContextMenu = Ui.Context(Ui.Menu("高级选项", EditAdvanced), Ui.Menu("移除选中", RemoveRows));
        Grid.SetRow(table, 1); center.Children.Add(table); Grid.SetRow(center, 2); root.Children.Add(center);

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("取消", () => Close()));
        buttons.Children.Add(Ui.Button("保存全部", () => SaveChanges(true), true));
        DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        error = Ui.Text("", 14, Ui.Brush("#BD4B55")); error.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(error);
        Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
        Closing += (_, e) => {
            if (committed || !Dirty()) return;
            var choice = MessageBox.Show(this, "保存对计划的修改？", "编辑全部计划", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) e.Cancel = true;
            else if (choice == MessageBoxResult.Yes && !SaveChanges(false)) e.Cancel = true;
        };
        PreviewKeyDown += (_, e) => {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { SaveChanges(true); e.Handled = true; }
        };
        RefreshMonth();
        Loaded += (_, _) => {
            var row = visible.FirstOrDefault(r => r.Day.HasValue && DateOnly.FromDateTime(r.Day.Value) >= initial);
            if (row != null) { table.SelectedItem = row; table.ScrollIntoView(row); }
        };
    }
    internal void ChangeMonth(DateOnly target)
    {
        table.CommitEdit(); notes[MonthKey] = monthNotes.Text;
        month = new DateOnly(target.Year, target.Month, 1); RefreshMonth();
    }
    private void RefreshMonth()
    {
        switching = true;
        monthLabel.Text = month.ToString("yyyy年M月");
        monthNotes.Text = notes.GetValueOrDefault(MonthKey, "");
        for (int d = 1; d <= DateTime.DaysInMonth(month.Year, month.Month); d++)
        {
            var day = new DateOnly(month.Year, month.Month, d).ToDateTime(TimeOnly.MinValue);
            if (!drafts.Any(r => r.Day == day)) drafts.Add(new PlanRow(day));
        }
        visible.Clear();
        foreach (var row in drafts.Where(r => r.Day.HasValue && r.Day.Value.Year == month.Year && r.Day.Value.Month == month.Month)
            .OrderBy(r => r.Day).ThenBy(r => r.Order)) visible.Add(row);
        switching = false;
        if (visible.Count > 0) table.ScrollIntoView(visible[0]);
    }
    private void AddRow()
    {
        var day = (table.SelectedItem as PlanRow)?.Day ?? month.ToDateTime(TimeOnly.MinValue);
        var row = new PlanRow(day) { Order = drafts.Count == 0 ? 0 : drafts.Max(r => r.Order) + 1 };
        drafts.Add(row); RefreshMonth(); table.SelectedItem = row; table.ScrollIntoView(row);
        table.CurrentCell = new DataGridCellInfo(row, table.Columns[2]); table.Focus();
    }
    private void RemoveRows()
    {
        table.CommitEdit();
        foreach (var row in table.SelectedItems.Cast<PlanRow>().ToList()) drafts.Remove(row);
        RefreshMonth();
    }
    private void EditAdvanced()
    {
        if (table.SelectedItem is not PlanRow row) return;
        if (string.IsNullOrWhiteSpace(row.Text)) { error.Text = "先写下任务内容。"; return; }
        try
        {
            var editor = new TaskEditor(this, row.ToTask(row.Order), false, task => { row.Apply(task); return true; });
            editor.ShowDialog(); RefreshMonth();
        }
        catch (Exception ex) { error.Text = ex.Message; }
    }
    private void Paste()
    {
        var dialog = Ui.Dialog(this, "粘贴多日计划", 580); dialog.SizeToContent = SizeToContent.Manual; dialog.Height = 450; dialog.ResizeMode = ResizeMode.CanResize;
        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var hint = Ui.Text("日期单独一行，例如 2026-11-01；下面每行一项任务。", 14, Ui.Muted); hint.Margin = new Thickness(0, 0, 0, 10); grid.Children.Add(hint);
        var input = new TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, FontSize = 16, Padding = new Thickness(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(input, 1); grid.Children.Add(input);
        var button = Ui.Button("加入计划", () => {
            try {
                var fallback = (table.SelectedItem as PlanRow)?.Day is { } day ? DateOnly.FromDateTime(day) : month;
                var parsed = PlanPaste.Parse(input.Text, fallback, DateOnly.FromDateTime(DateTime.Today));
                ImportLines(parsed); dialog.DialogResult = true;
            } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "检查日期", MessageBoxButton.OK, MessageBoxImage.Information); }
        }, true);
        button.Margin = new Thickness(0, 12, 0, 0); button.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(button, 2); grid.Children.Add(button); dialog.Content = grid;
        dialog.Loaded += (_, _) => input.Focus(); dialog.ShowDialog();
    }
    internal void ImportLines(IReadOnlyList<PlannedLine> lines)
    {
        int order = drafts.Count == 0 ? 0 : drafts.Max(r => r.Order) + 1;
        foreach (var line in lines)
        {
            var date = line.Date.ToDateTime(TimeOnly.MinValue);
            var placeholder = drafts.FirstOrDefault(r => r.Original == null && r.Day == date && string.IsNullOrWhiteSpace(r.Text));
            if (placeholder != null) { placeholder.Text = line.Text; placeholder.Order = order++; }
            else drafts.Add(new PlanRow(date) { Text = line.Text, Order = order++ });
        }
        if (lines.Count > 0) ChangeMonth(lines[0].Date);
    }
    private List<TodoItem> Desired()
    {
        if (!table.CommitEdit(DataGridEditingUnit.Cell, true) || !table.CommitEdit(DataGridEditingUnit.Row, true))
            throw new ArgumentException("请先修正未完成的日期输入。");
        return drafts.Where(r => !string.IsNullOrWhiteSpace(r.Text)).OrderBy(r => r.Day).ThenBy(r => r.Order)
            .Select((r, i) => r.ToTask(r.Original?.Position ?? (baseline.Count == 0 ? 0 : baseline.Max(t => t.Position) + 1) + i)).ToList();
    }
    internal bool HasUnsavedChanges => Dirty();
    private bool Dirty()
    {
        notes[MonthKey] = monthNotes.Text;
        try {
            var wanted = Desired();
            return !wanted.OrderBy(t => t.Id).SequenceEqual(baseline.OrderBy(t => t.Id)) ||
                notes.Any(pair => pair.Value != originalNotes.GetValueOrDefault(pair.Key, ""));
        } catch { return true; }
    }
    internal bool SaveChanges(bool close)
    {
        try
        {
            notes[MonthKey] = monthNotes.Text;
            var wanted = Desired();
            int deleted = baseline.Count(t => !wanted.Any(x => x.Id == t.Id));
            if (deleted > 0 && MessageBox.Show(this, $"将移除 {deleted} 项原有任务，确定保存？", "保存全部计划", MessageBoxButton.OKCancel,
                MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return false;
            store.SavePlan(baseline, wanted, originalNotes, notes);
            committed = true; saved(); if (close) Close(); return true;
        }
        catch (Exception ex) { error.Text = ex.Message; return false; }
    }
    internal void SetMonthNotes(string value) => monthNotes.Text = value;

    private sealed class PlanRow : INotifyPropertyChanged
    {
        public TodoItem? Original { get; private set; }
        public string Id { get; } = Guid.NewGuid().ToString("N");
        private string text = "";
        private DateTime? day;
        public int Order { get; set; }
        public DateTime? Day { get => day; set { day = value?.Date; Changed(nameof(Day)); } }
        public string Text { get => text; set { text = value; Changed(nameof(Text)); } }
        public string CompletedMark => Original?.Completed == true ? "✓" : "";
        public string DeadlineLabel => Original?.Deadline?.ToLocalTime().ToString("HH:mm") ?? "";
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public PlanRow(DateTime? date) { Day = date; }
        public PlanRow(TodoItem item) { Original = item; Id = item.Id; Text = item.Text; Day = item.Date.ToDateTime(TimeOnly.MinValue); Order = item.Position; }
        public TodoItem ToTask(int position)
        {
            if (!Day.HasValue) throw new ArgumentException("任务日期不能为空。");
            var date = DateOnly.FromDateTime(Day.Value);
            return (Original ?? new TodoItem(Id, Text, date, null, position)) with { Text = Text.Trim(), Date = date, Position = position,
                GroupId = Original?.Date == date ? Original.GroupId : null };
        }
        public void Apply(TodoItem item)
        {
            Original = item; Text = item.Text; Day = item.Date.ToDateTime(TimeOnly.MinValue); Changed(nameof(DeadlineLabel));
        }
    }
}
