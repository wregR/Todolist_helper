namespace DesktopTodo.Core;

public sealed record TodoItem(string Id, string Text, DateOnly Date, string? GroupId, int Position,
    bool Completed = false, DateTimeOffset? Deadline = null, bool Remind = false, long? NotifiedFor = null);
public sealed record TaskGroup(string Id, DateOnly Date, string Name, int Position);
public sealed record UserSettings(string Title = "我的计划", bool Topmost = true, bool Autostart = true,
    double Width = 340, double Height = 100, double? Left = null, double? Top = null, int LayoutVersion = 0);
public sealed record BoardSection(string Title, DateOnly? Date, IReadOnlyList<TodoItem> Tasks, IReadOnlyList<TaskGroup> Groups);

public static class BoardBuilder
{
    public static IReadOnlyList<BoardSection> Build(IEnumerable<TodoItem> tasks, IEnumerable<TaskGroup> groups, DateOnly today, DateOnly? selected)
    {
        var all = tasks.ToList();
        var gs = groups.ToList();
        var sections = new List<BoardSection>();
        if (selected is null)
        {
            var backlog = all.Where(t => t.Date < today && !t.Completed).OrderBy(t => t.Date).ThenBy(t => t.Position).ToList();
            if (backlog.Count > 0) sections.Add(new("待补办", null, backlog, gs));
        }
        var dates = selected.HasValue ? new[] { selected.Value } : new[] { today };
        foreach (var date in dates)
        {
            string title = date == today ? "今天" : date == today.AddDays(1) ? "明天" : date.ToString("M月d日");
            sections.Add(new(title, date, all.Where(t => t.Date == date).OrderBy(t => t.Completed).ThenBy(t => t.Position).ToList(),
                gs.Where(g => g.Date == date).OrderBy(g => g.Position).ToList()));
        }
        return sections;
    }
}

public static class ReminderPolicy
{
    public static IEnumerable<TodoItem> Due(IEnumerable<TodoItem> tasks, DateTimeOffset now) =>
        tasks.Where(t => !t.Completed && t.Remind && t.Deadline is { } deadline &&
            deadline <= now && t.NotifiedFor != deadline.UtcTicks);
}

// No UI or wall clock dependency: the exact interaction can be tested deterministically.
public sealed class SwipeGesture
{
    private double x, y, width, delta;
    private bool held, locked, rejected;
    public bool IsDragging => held && locked;
    public double Offset => IsDragging ? Math.Max(0, delta) : 0;
    public double Threshold => Math.Max(140, width * .6);
    public bool Ready => IsDragging && Offset >= Threshold;
    public void Begin(double startX, double startY, double rowWidth)
    { x = startX; y = startY; width = rowWidth; delta = 0; held = true; locked = false; rejected = false; }
    public void Move(double currentX, double currentY)
    {
        if (!held || rejected) return;
        double dx = currentX - x, dy = Math.Abs(currentY - y);
        if (!locked)
        {
            if (dy > 12 && dy >= Math.Abs(dx)) { rejected = true; return; }
            if (dx > 12 && dx > 1.5 * dy) locked = true;
        }
        delta = dx;
    }
    public bool Release() { bool completed = Ready; Cancel(); return completed; }
    public void Cancel() { held = locked = rejected = false; delta = 0; }
}
