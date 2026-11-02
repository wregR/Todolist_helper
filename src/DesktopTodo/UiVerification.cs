using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace DesktopTodo;

// Explicit diagnostic mode, always uses isolated --data-dir and never modifies startup registration.
internal static class UiVerification
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    public static void Run(DesktopApplication app, TodoStore store, string root)
    {
        var report = new List<string>();
        string dir = Path.Combine(root, "artifacts", "ui-verification");
        Directory.CreateDirectory(dir);
        bool success = false;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); report.Add("PASS: " + label); }
        try
        {
            var board = app.Board!;
            board.UpdateLayout();
            Check(board.IsVisible && board.WindowState == WindowState.Normal, "startup opens the complete window");
            Check(board.Topmost, "default topmost");
            Check(board.ActualWidth <= 340 && board.ActualHeight < 400, "compact content-sized window");
            Check(!Descendants<TextBlock>(board).Any(t => t.Text.StartsWith("明天") || t.Text == "我的计划" || t.Text == "研究" || t.Text == "生活"), "no tomorrow heading, oversized title or groups");
            Check(Descendants<TextBlock>(board).Any(t => t.Text == "1"), "numbered tasks");
            foreach (double scale in new[] { 1d, 1.25d, 1.5d })
            {
                board.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(board.ActualWidth * scale),
                    (int)Math.Ceiling(board.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(board);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(dir, $"main-{scale * 100:0}.png")); encoder.Save(output);
                Check(bitmap.PixelWidth == (int)Math.Ceiling(board.ActualWidth * scale), $"render at {scale * 100:0}%");
            }
            board.UpdateLayout();
            var movable=Descendants<TaskRow>(board).Where(r=>r.Item.Date==DateOnly.FromDateTime(DateTime.Today)&&!r.Item.Completed).OrderBy(r=>r.Item.Position).ToList();
            var moving=movable[2]; var first=movable[0];
            double firstTop=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(first).Top;
            board.BeginSort(moving,200); board.UpdateSortAt(90,firstTop+first.ActualHeight+1);
            board.EndSort(true);
            Check(store.Tasks().Where(t=>t.Date==moving.Item.Date&&!t.Completed).OrderBy(t=>t.Position).ElementAt(1).Id==moving.Item.Id,"drag inserts third task into second position");
            board.UpdateLayout();
            moving=Descendants<TaskRow>(board).Single(r=>r.Item.Id==moving.Item.Id);
            board.BeginSort(moving,100); board.UpdateSortAt(40,-1); board.EndSort(true);
            Check(store.Tasks().Where(t=>t.Date==moving.Item.Date&&!t.Completed).OrderBy(t=>t.Position).First().Id==moving.Item.Id,"drag inserts second task above first");
            var beforeCancel=store.Tasks(); board.UpdateLayout();
            moving=Descendants<TaskRow>(board).Single(r=>r.Item.Id==moving.Item.Id);
            board.BeginSort(moving,100); board.UpdateSortAt(400,9999); board.EndSort(false);
            Check(beforeCancel.SequenceEqual(store.Tasks()),"cancelled drag leaves database unchanged");
            board.Width = 300; board.Height = 300; board.UpdateLayout();
            Check(board.ActualWidth >= 300 && board.ActualHeight >= 92, "minimum size");
            var small = new RenderTargetBitmap(300, (int)Math.Ceiling(board.ActualHeight), 96, 96, PixelFormats.Pbgra32); small.Render(board);
            var smallPng = new PngBitmapEncoder(); smallPng.Frames.Add(BitmapFrame.Create(small));
            using (var output = File.Create(Path.Combine(dir, "minimum-size.png"))) smallPng.Save(output);
            board.Hide(); Check(!board.IsVisible, "explicit hide");
            board.Reveal(); Check(board.IsVisible && board.WindowState == WindowState.Normal, "reveal after hide");
            var original = store.Tasks().First(t => !t.Completed);
            var editor = new TaskEditor(board, original, false, updated => { store.SaveTask(updated); return true; });
            editor.Show(); editor.UpdateLayout();
            var expander = Descendants<Expander>(editor).Single();
            Check(!expander.IsExpanded, "advanced options initially collapsed");
            Check(!Descendants<ComboBox>(editor).Any() && !Descendants<TextBlock>(editor).Any(t => t.Text == "分组"), "editor has no grouping");
            expander.IsExpanded = true; editor.UpdateLayout();
            Check(Descendants<CheckBox>(editor).Any(c => Equals(c.Content, "到时提醒")), "advanced reminder checkbox");
            var editorBmp = new RenderTargetBitmap((int)Math.Ceiling(editor.ActualWidth), (int)Math.Ceiling(editor.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            editorBmp.Render(editor);
            var editorPng = new PngBitmapEncoder(); editorPng.Frames.Add(BitmapFrame.Create(editorBmp));
            using (var output = File.Create(Path.Combine(dir, "advanced-editor.png"))) editorPng.Save(output);
            editor.Close();
            var editCheck = new TaskEditor(board, original, false, updated => { store.SaveTask(updated); return true; });
            editCheck.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => {
                Descendants<TextBox>(editCheck).First().Text = "编辑保存验收";
                Descendants<Button>(editCheck).Single(b => Equals(b.Content, "保存")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            editCheck.ShowDialog();
            Check(store.Tasks().Single(t => t.Id == original.Id).Text == "编辑保存验收", "editor save persists content");
            var cancelCheck = new TaskEditor(board, store.Tasks().Single(t => t.Id == original.Id), false, updated => { store.SaveTask(updated); return true; });
            cancelCheck.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => {
                Descendants<TextBox>(cancelCheck).First().Text = "不应保存";
                Descendants<Button>(cancelCheck).Single(b => Equals(b.Content, "取消")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            cancelCheck.ShowDialog();
            Check(store.Tasks().Single(t => t.Id == original.Id).Text == "编辑保存验收", "cancel leaves previous content intact");
            var due = new TodoItem("verify-due", "定时提醒验收", DateOnly.FromDateTime(DateTime.Now), null, 100, Deadline: DateTimeOffset.Now.AddSeconds(-1), Remind: true);
            store.SaveTask(due);
            board.Hide();
            var foreground = GetForegroundWindow();
            app.CheckReminders();
            var reminder = app.Windows.OfType<ReminderWindow>().Single();
            Check(reminder.IsVisible, "reminder opens while main window hidden");
            Check(GetForegroundWindow() == foreground, "reminder does not steal focus");
            Check(!store.Tasks().Single(t => t.Id == due.Id).Completed, "reminder does not complete task");
            Check(!ReminderPolicy.Due(store.Tasks(), DateTimeOffset.Now).Any(), "reminder acknowledgment persisted");
            reminder.UpdateLayout();
            var reminderBmp = new RenderTargetBitmap((int)Math.Ceiling(reminder.ActualWidth), (int)Math.Ceiling(reminder.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            reminderBmp.Render(reminder);
            var reminderPng = new PngBitmapEncoder(); reminderPng.Frames.Add(BitmapFrame.Create(reminderBmp));
            using (var output = File.Create(Path.Combine(dir, "reminder.png"))) reminderPng.Save(output);
            store.Complete(due.Id, true); app.RefreshReminderWindow();
            Check(!app.Windows.OfType<ReminderWindow>().Any(), "completion removes active reminder");
            board.Reveal(original.Id); Check(board.IsVisible, "view task reveals window");
            board.Width = 340;

            var planner = new PlanEditorWindow(board, store, DateOnly.FromDateTime(DateTime.Today), board.Refresh);
            planner.Show(); planner.UpdateLayout();
            Check(!planner.HasUnsavedChanges, "opening planner does not mutate or dirty tasks");
            var futureMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
            planner.ChangeMonth(futureMonth);
            planner.SetMonthNotes("完成资料整理、实验复核和本月初稿。");
            planner.ImportLines([
                new(futureMonth, "整理本月资料"),
                new(futureMonth, "列出实验安排"),
                new(futureMonth.AddDays(1), "核对实验数据"),
                new(futureMonth.AddDays(2), "完成第一版图表"),
                new(futureMonth.AddMonths(1), "准备阶段汇报")
            ]);
            planner.ChangeMonth(futureMonth.AddMonths(1)); planner.SetMonthNotes("完成阶段汇报。");
            planner.ChangeMonth(futureMonth); planner.UpdateLayout();
            Check(Descendants<TextBox>(planner).Any(t => t.Text.Contains("本月初稿")), "month navigation retains goal drafts");
            Check(Descendants<DataGrid>(planner).Single().Items.Count >= DateTime.DaysInMonth(futureMonth.Year,futureMonth.Month), "entire month is editable together");
            planner.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
            planner.UpdateLayout();
            var taskColumn = Descendants<DataGrid>(planner).Single().Columns[2];
            Check(taskColumn.ActualWidth >= 220, "task editing column has usable width");
            var planBmp = new RenderTargetBitmap((int)Math.Ceiling(planner.ActualWidth), (int)Math.Ceiling(planner.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            planBmp.Render(planner);
            var planPng = new PngBitmapEncoder(); planPng.Frames.Add(BitmapFrame.Create(planBmp));
            using (var output=File.Create(Path.Combine(dir,"monthly-planner.png"))) planPng.Save(output);
            Check(planner.SaveChanges(false), "planner saves all month drafts");
            Check(store.MonthPlans()[futureMonth.ToString("yyyy-MM")].Contains("初稿") && store.MonthPlans()[futureMonth.AddMonths(1).ToString("yyyy-MM")].Contains("阶段汇报"), "multiple month goals saved together");
            Check(store.Tasks().Count(t=>t.Text is "整理本月资料" or "列出实验安排" or "核对实验数据" or "完成第一版图表" or "准备阶段汇报")==5,"multi-day import retained across months");
            planner.Close();

            success = true;
        }
        catch (Exception ex) { report.Add("FAIL: " + ex); app.Log(ex); }
        finally
        {
            File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(new { success, checks = report }, new JsonSerializerOptions { WriteIndented = true }));
            app.ExitApplication();
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
