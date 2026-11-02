using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopTodo;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string root = FindRoot();
        string? supplied = Arg(args, "--data-dir");
        string data = Path.GetFullPath(supplied ?? Path.Combine(root, "data"));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToUpperInvariant())))[..20];
        using var mutex = new Mutex(true, @"Local\DesktopTodo." + key, out bool first);
        using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DesktopTodo.Show." + key);
        if (!first) { wake.Set(); return; }
        try
        {
            var app = new DesktopApplication(root, data, supplied != null || args.Contains("--no-startup"), args);
            var listener = new Thread(() => {
                while (true) { wake.WaitOne(); if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(() => app.Board?.Reveal())); else return; }
            }) { IsBackground = true };
            listener.Start();
            app.Run();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(data); File.AppendAllText(Path.Combine(data, "error.log"), DateTimeOffset.Now + " " + error + Environment.NewLine);
            MessageBox.Show(error.Message, "我的计划未能启动", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { mutex.ReleaseMutex(); }
    }
    internal static string? Arg(string[] args, string key) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    private static string FindRoot()
    {
        var portableRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        if (portableRoot != null && File.Exists(Path.Combine(portableRoot.FullName, "desktop-todo.portable"))) return portableRoot.FullName;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) return dir.FullName;
        return Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? AppContext.BaseDirectory;
    }
}

internal sealed class DesktopApplication : Application
{
    private readonly string root, data;
    private readonly bool noStartup;
    private readonly string[] args;
    private TodoStore store = null!;
    private System.Windows.Forms.NotifyIcon? tray;
    private ReminderWindow? reminder;
    private readonly System.Media.SoundPlayer reminderSound = new(Path.Combine(AppContext.BaseDirectory, "Assets", "reminder.wav"));
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateOnly day;
    private bool checking, failedReminder;
    public MainWindow? Board { get; private set; }
    public DesktopApplication(string projectRoot, string dataPath, bool suppressStartup, string[] arguments)
    {
        root = projectRoot; data = dataPath; noStartup = suppressStartup; args = arguments;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) => { Log(e.Exception); e.Handled = true; MessageBox.Show(e.Exception.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Error); };
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        store = new TodoStore(data); day = DateOnly.FromDateTime(DateTime.Now);
        try { reminderSound.Load(); } catch (Exception ex) { Log(ex); }
        if (args.Contains("--qa-seed") && noStartup && store.Tasks().Count == 0) Seed();
        try { store.Backup(day); } catch (Exception ex) { Log(ex); }
        Board = new MainWindow(this, store); MainWindow = Board;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开清单", null, (_, _) => Board.Reveal());
        menu.Items.Add("编辑全部计划", null, (_, _) => { Board.Reveal(); Board.OpenPlanner(); });
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application;
        tray = new System.Windows.Forms.NotifyIcon { Icon = icon, Text = "我的计划", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Board.Reveal();
        Board.Show();
        if (args.Contains("--open-plan")) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Board.OpenPlanner));
        try { ConfigureAutostart(store.Settings().Autostart); }
        catch (Exception ex) { Log(ex); MessageBox.Show(Board, "开机启动设置失败：" + ex.Message, "开机启动", MessageBoxButton.OK, MessageBoxImage.Warning); }
        timer.Tick += (_, _) => Tick();
        timer.Start();
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.TimeChanged += TimeChanged;
        if (noStartup && args.Contains("--verify-ui")) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => UiVerification.Run(this, store, root))); else Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(CheckReminders));
    }
    public void ConfigureAutostart(bool enabled)
    {
        if (noStartup) return;
        var exe = Path.Combine(root, "app", "DesktopTodo.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("请先构建发布版本。", exe);
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("DesktopTodo.Public", "\"" + exe + "\" --startup");
        else key.DeleteValue("DesktopTodo.Public", false);
    }
    private void Tick()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != day)
        {
            day = today; Board?.Refresh();
            try { store.Backup(day); } catch (Exception ex) { Log(ex); }
        }
        CheckReminders();
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(new Action(Tick)); }
    private void TimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(Tick));
    public void CheckReminders()
    {
        if (checking || store is null) return;
        checking = true;
        try
        {
            var tasks = ReminderPolicy.Due(store.Tasks(), DateTimeOffset.Now).ToList();
            if (tasks.Count == 0) return;
            if (reminder == null)
            {
                reminder = new ReminderWindow(id => Board?.Reveal(id));
                reminder.Closed += (_, _) => reminder = null;
            }
            reminder.Add(tasks); reminder.Show();
            store.AcknowledgeReminders(tasks);
            if (noStartup) File.AppendAllText(Path.Combine(data, "reminder-check.log"), DateTimeOffset.Now.ToString("O") + " " + string.Join(",", tasks.Select(t => t.Id + ":" + (DateTimeOffset.Now - t.Deadline!.Value).TotalMilliseconds.ToString("F0"))) + Environment.NewLine);
            try { reminderSound.Play(); } catch (Exception ex) { Log(ex); }
            failedReminder = false;
        }
        catch (Exception ex)
        {
            Log(ex);
            if (!failedReminder) { failedReminder = true; MessageBox.Show(Board!, "提醒状态未能保存：" + ex.Message, "提醒", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        finally { checking = false; }
    }
    public void RefreshReminderWindow() => reminder?.Reconcile(store.Tasks());
    public void ExitApplication()
    {
        foreach (var editor in Windows.OfType<PlanEditorWindow>().ToList()) { editor.Close(); if (editor.IsVisible) return; }
        timer.Stop(); reminder?.Close(); tray?.Dispose(); Board?.CloseForExit(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        timer.Stop(); tray?.Dispose(); reminderSound.Dispose(); SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.TimeChanged -= TimeChanged; base.OnExit(e);
    }
    public void Log(Exception error)
    {
        try { Directory.CreateDirectory(data); File.AppendAllText(Path.Combine(data, "error.log"), DateTimeOffset.Now + " " + error + Environment.NewLine); } catch { }
    }
    private void Seed()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var research = new TaskGroup("qa-research", today, "研究", 0); store.SaveGroup(research);
        var life = new TaskGroup("qa-life", today, "生活", 1); store.SaveGroup(life);
        store.SaveTask(new("qa-old", "整理上次讨论记录", today.AddDays(-1), null, 0));
        store.SaveTask(new("qa-one", "梳理今天的研究思路", today, research.Id, 1));
        store.SaveTask(new("qa-long", "核对实验记录与图表，让每一条结论都能对应到实际的数据和观察", today, research.Id, 2,
            Deadline: new DateTimeOffset(DateTime.Today.AddHours(21)), Remind: false));
        store.SaveTask(new("qa-life-task", "出门走一走", today, life.Id, 3));
        store.SaveTask(new("qa-tomorrow", "准备明天要讨论的问题", today.AddDays(1), null, 4));
        if (args.Contains("--qa-reminder"))
            store.SaveTask(new("qa-reminder", "提醒功能验收", today, null, 5, Deadline: DateTimeOffset.Now.AddSeconds(30), Remind: true));
    }
}
