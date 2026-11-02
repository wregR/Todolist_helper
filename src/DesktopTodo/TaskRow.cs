using System.Windows.Media.Animation;

namespace DesktopTodo;

internal sealed class TaskRow : Grid
{
    private readonly SwipeGesture gesture = new();
    private readonly TranslateTransform transform = new();
    private readonly Border front;
    private readonly Border back;
    private readonly Window owner;
    private readonly Action finish;
    private readonly TranslateTransform sortOffset = new();
    private readonly Border insertion = new() { Background = Ui.Brush("#4675D5"), Height = 3, CornerRadius = new CornerRadius(2), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private Point press; private bool sorting;
    public TodoItem Item { get; }
    public TaskRow(Window window, TodoItem item, ContextMenu menu, Action complete, bool showDate, int number)
    {
        owner = window; Item = item; finish = complete;
        Margin = new Thickness(0, 0, 0, 4); ClipToBounds = true; Focusable = true; ContextMenu = menu; Background = Brushes.Transparent;
        var palette = new[] { ("#4675D5", "#EAF0FC"), ("#8A63C5", "#F0EBF8"), ("#278A78", "#E6F3EF"), ("#B28232", "#FAF0DD") };
        var (accentHex, tintHex) = palette[(number - 1) % palette.Length];
        var accent = item.Completed ? Ui.Muted : Ui.Brush(accentHex);
        back = new Border { Background = Ui.Brush("#D0EADF"), CornerRadius = new CornerRadius(5), Visibility = Visibility.Hidden };
        back.Child = new TextBlock { Text = "✓", FontSize = 21, Foreground = Ui.Accent, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 0, 0, 0) };
        Children.Add(back);
        front = new Border { Background = Ui.Brush(item.Completed ? "#EDF0F4" : tintHex), BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 8, 8, 8), RenderTransform = transform, MinHeight = 38 };
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        var index = Ui.Text(item.Completed ? "✓" : number.ToString(), 17, accent);
        index.FontWeight = FontWeights.Bold; line.Children.Add(index);
        var content = new StackPanel(); Grid.SetColumn(content, 1);
        var text = Ui.Text(item.Text, 17, item.Completed ? Ui.Muted : Ui.Ink);
        text.FontWeight = FontWeights.SemiBold;
        if (item.Completed) text.TextDecorations = TextDecorations.Strikethrough;
        content.Children.Add(text);
        var details = new List<string>();
        if (showDate) details.Add(item.Date.ToString("M月d日"));
        if (item.Deadline is { } due)
            details.Add(due.LocalDateTime.Date == item.Date.ToDateTime(TimeOnly.MinValue) ? due.ToLocalTime().ToString("HH:mm") : due.ToLocalTime().ToString("M月d日 HH:mm"));
        if (details.Count > 0)
        {
            Brush timeColor = Ui.Muted;
            if (!item.Completed && item.Deadline is { } end)
                timeColor = end < DateTimeOffset.Now ? Ui.Brush("#BD4B55") : end <= DateTimeOffset.Now.AddHours(2) ? Ui.Brush("#AC6D21") : Ui.Brush("#536D9F");
            var label = Ui.Text(string.Join(" · ", details), 13, timeColor);
            label.Margin = new Thickness(0, 2, 0, 0); content.Children.Add(label);
        }
        line.Children.Add(content); front.Child = line; Children.Add(front); Children.Add(insertion); RenderTransform = sortOffset;
        MouseLeftButtonDown += (_, e) => {

            Focus(); transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = 0;
            var p = e.GetPosition(owner); press = p; gesture.Begin(p.X, p.Y, ActualWidth); CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, e) => {
            if (!IsMouseCaptured) return;
            var p = e.GetPosition(owner);
            if (!sorting && !gesture.IsDragging && Math.Abs(p.Y-press.Y)>10 && Math.Abs(p.Y-press.Y)>1.4*Math.Abs(p.X-press.X))
            { sorting=true; gesture.Cancel(); ((MainWindow)owner).BeginSort(this,press.Y); }
            if(sorting) { ((MainWindow)owner).UpdateSort(p.Y); e.Handled=true; return; }
            if(Item.Completed)return;
            gesture.Move(p.X, p.Y);
            back.Visibility = gesture.IsDragging ? Visibility.Visible : Visibility.Hidden;
            transform.X = Math.Min(ActualWidth, gesture.Offset);
            back.Background = gesture.Ready ? Ui.Brush("#279976") : Ui.Brush("#D0EADF");
            ((TextBlock)back.Child).Foreground = gesture.Ready ? Brushes.White : Ui.Accent;
        };
        MouseLeftButtonUp += (_, e) => {
            if (!IsMouseCaptured) return;
            if(sorting) { sorting=false; ReleaseMouseCapture(); ((MainWindow)owner).EndSort(true); e.Handled=true; return; }
            var p=e.GetPosition(owner); gesture.Move(p.X,p.Y);
            bool done=!Item.Completed && gesture.Release(); ReleaseMouseCapture(); e.Handled=true;
            Reset(); if(done)finish();
        };
        LostMouseCapture += (_, _) => Cancel();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Cancel(); e.Handled = true; } };
        PreviewMouseWheel += (_, _) => Cancel();
        ContextMenuOpening += (_, _) => Cancel();
        Loaded += (_, _) => owner.Deactivated += OnDeactivate;
        Unloaded += (_, _) => { owner.Deactivated -= OnDeactivate; Cancel(); };
    }
    private void OnDeactivate(object? sender, EventArgs e) => Cancel();
    private void Cancel() { if(sorting) { sorting=false; ((MainWindow)owner).EndSort(false); } gesture.Cancel(); if (IsMouseCaptured) ReleaseMouseCapture(); Reset(); }
    private void Reset()
    {
        double from = transform.X; transform.X = 0; if (from == 0) back.Visibility = Visibility.Hidden;
        var animation = new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(120)) { FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => { if (!gesture.IsDragging) back.Visibility = Visibility.Hidden; };
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
    internal void OffsetForSort(double offset, bool animate)
    {
        if(!animate) { sortOffset.BeginAnimation(TranslateTransform.YProperty,null); sortOffset.Y=offset; return; }
        double current=sortOffset.Y;
        sortOffset.BeginAnimation(TranslateTransform.YProperty,null); sortOffset.Y=offset;
        sortOffset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(current,offset,TimeSpan.FromMilliseconds(190)) {
            EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut }, FillBehavior=FillBehavior.Stop });
    }
    internal void ShowInsertion(bool visible,bool below)
    { insertion.VerticalAlignment=below?VerticalAlignment.Bottom:VerticalAlignment.Top; insertion.Visibility=visible?Visibility.Visible:Visibility.Collapsed; }}
