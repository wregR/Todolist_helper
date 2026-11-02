using DesktopTodo.Core;
using Microsoft.Data.Sqlite;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var swipe = new SwipeGesture();
swipe.Begin(0, 0, 300); Check(!swipe.Release(), "click does not complete");
swipe.Begin(0, 0, 300); swipe.Move(8, 2); Check(!swipe.IsDragging && !swipe.Release(), "jitter does not drag");
swipe.Begin(0, 0, 300); swipe.Move(20, 100); swipe.Move(250, 100); Check(!swipe.Release(), "vertical drag rejected");
swipe.Begin(0, 0, 300); swipe.Move(-250, 0); Check(!swipe.Release(), "left swipe rejected");
swipe.Begin(0, 0, 300); swipe.Move(12, 0); Check(!swipe.IsDragging, "12 DIP boundary");
swipe.Move(13, 0); Check(swipe.IsDragging && !swipe.Ready, "horizontal lock");
swipe.Move(179, 0); Check(!swipe.Release(), "under 60 percent");
swipe.Begin(0, 0, 300); swipe.Move(180, 0); Check(swipe.Ready, "ready only before release");
Check(swipe.Release(), "release commits"); Check(!swipe.Release(), "one completion per gesture");
swipe.Begin(0, 0, 200); swipe.Move(139, 0); Check(!swipe.Release(), "140 DIP minimum");
swipe.Begin(0, 0, 200); swipe.Move(140, 0); Check(swipe.Release(), "140 DIP endpoint");
foreach (var reason in new[] { "escape", "focus loss", "capture loss", "wheel" }) {
    swipe.Begin(0, 0, 300); swipe.Move(250, 0); swipe.Cancel(); Check(!swipe.Release(), reason + " cancels");
}
swipe.Begin(0, 0, 300); swipe.Move(250, 0); swipe.Move(40, 0); Check(!swipe.Release(), "retreat below threshold cancels");
var today = new DateOnly(2026, 10, 31);
var now = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(8));
var item = new TodoItem("one", "First", today, null, 0, Deadline: now, Remind: true);
Check(!ReminderPolicy.Due([item], now.AddTicks(-1)).Any(), "not early");
Check(ReminderPolicy.Due([item], now).Count() == 1, "due at exact time");
Check(ReminderPolicy.Due([item], now.AddDays(1)).Count() == 1, "resume catches overdue");
Check(!ReminderPolicy.Due([item with { Completed = true }], now).Any(), "completed not reminded");
Check(!ReminderPolicy.Due([item with { Remind = false }], now).Any(), "silent deadline");
Check(!ReminderPolicy.Due([item with { Deadline = null }], now).Any(), "plain task no reminder");
Check(!ReminderPolicy.Due([item with { NotifiedFor = now.UtcTicks }], now).Any(), "ack suppresses duplicate");
var sections = BoardBuilder.Build([item, item with { Id = "old", Date = today.AddDays(-1) },
    item with { Id = "done-old", Date = today.AddDays(-1), Completed = true },
    item with { Id = "tomorrow", Date = today.AddDays(1) }], [], today, null);
Check(sections.Count == 2 && sections[0].Title == "待补办" && sections[0].Tasks.Count == 1, "backlog keeps only unfinished old");
Check(sections[0].Tasks[0].Date == today.AddDays(-1), "backlog preserves original date");
Check(sections[1].Tasks.Count == 1 && !sections.SelectMany(s => s.Tasks).Any(t => t.Id == "tomorrow"), "only current day shown");
Check(BoardBuilder.Build([], [], today, null).Count == 1, "empty backlog hidden");
Check(BoardBuilder.Build([item], [], today, today.AddDays(5)).Count == 1, "selected date view");
var tomorrow = BoardBuilder.Build([item], [], today.AddDays(1), null);
Check(tomorrow[0].Title == "待补办", "midnight rollover");

var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var data = Path.Combine(project, "artifacts", "tests-" + Guid.NewGuid().ToString("N"));
var store = new TodoStore(data);
Check(store.Settings().Topmost && store.Settings().Autostart, "default pinned and autostart");
store.SaveTask(item);
Check(new TodoStore(data).Tasks().Single() == item, "task roundtrip");
store.AcknowledgeReminders([item]);
Check(!ReminderPolicy.Due(new TodoStore(data).Tasks(), now.AddHours(1)).Any(), "ack survives restart");
store.SaveTask(item with { Text = "Edited" });
Check(!ReminderPolicy.Due(store.Tasks(), now).Any(), "text edit keeps ack");
store.SaveTask(item with { Deadline = now.AddMinutes(1) });
Check(ReminderPolicy.Due(store.Tasks(), now.AddMinutes(1)).Count() == 1, "deadline change resets ack");
store.Complete(item.Id, true); Check(!ReminderPolicy.Due(store.Tasks(), now.AddHours(1)).Any(), "completed cancels scheduled reminder");
store.Complete(item.Id, false); Check(!store.Tasks().Single().Completed, "restore incomplete");
var g = new TaskGroup("g", today, "Research", 0); store.SaveGroup(g);
store.SaveTask(item with { GroupId = g.Id });
Check(store.Tasks().Single().GroupId == g.Id, "group saved");
bool invalidGroup = false;
try { store.SaveTask(item with { GroupId = g.Id, Date = today.AddDays(1) }); } catch (ArgumentException) { invalidGroup = true; }
Check(invalidGroup && store.Tasks().Single().Date == today, "mismatched group transaction rejected");
var settings = new UserSettings("Plan", false, false, 500, 700, 200, 100);
store.SaveSettings(settings); Check(new TodoStore(data).Settings() == settings, "settings roundtrip");
store.SaveTask(new("two", "Second", today, null, 1));
var a = store.Tasks().First(t => t.Id == "one"); var b = store.Tasks().First(t => t.Id == "two");
store.Reorder(a, b); Check(store.Tasks().First().Id == "two", "atomic reorder");
store.DeleteGroup("g"); Check(store.Tasks().Count == 1 && store.Groups().Count == 0, "group deletion cascades");
store.DeleteTask("two"); Check(store.Tasks().Count == 0, "task deletion");
store.SaveTask(item);
using (var db = new SqliteConnection($"Data Source={Path.Combine(data, "todo.db")};Pooling=False")) {
    db.Open(); using var command = db.CreateCommand();
    command.CommandText = "CREATE TRIGGER fail_complete BEFORE UPDATE OF completed ON tasks BEGIN SELECT RAISE(ABORT,'simulated write failure'); END;";
    command.ExecuteNonQuery();
}
bool failed = false;
try { store.Complete(item.Id, true); } catch (SqliteException) { failed = true; }
Check(failed && !store.Tasks().Single().Completed, "save failure preserves incomplete state");
for (int i = 0; i < 10; i++) store.Backup(today.AddDays(i));
Check(Directory.GetFiles(Path.Combine(data, "backups"), "*.db").Length == 7, "seven daily backups retained");
store.Backup(today.AddDays(9));
Check(Directory.GetFiles(Path.Combine(data, "backups"), "*.db").Length == 7, "same-day backup idempotent");
using (var db = new SqliteConnection($"Data Source={Path.Combine(data, "backups", "2026-11-09.db")};Pooling=False")) {
    db.Open(); using var command = db.CreateCommand(); command.CommandText = "PRAGMA integrity_check";
    Check((string?)command.ExecuteScalar() == "ok", "backup integrity");
}

var pasted = PlanPaste.Parse("2026-11-01\n1. 整理资料\n2、完成草稿\n\n2026-11-02\n- 核对数据\n2026-12-01\n准备汇报", today, today);
Check(pasted.Count == 4 && pasted[0].Date == new DateOnly(2026,11,1) && pasted[3].Date == new DateOnly(2026,12,1), "paste plans across months");
Check(pasted[0].Text == "整理资料" && pasted[1].Text == "完成草稿" && pasted[2].Text == "核对数据", "strip pasted list numbering");
Check(PlanPaste.Parse("明天\n先做这件\n后天\n再做那件", today, today)[1].Date == today.AddDays(2), "relative date headings");
Check(PlanPaste.Parse("11月3日\n按天安排", today, today).Single().Date == new DateOnly(2026,11,3), "Chinese date heading");
Check(PlanPaste.Parse("直接粘贴任务", today, today).Single().Date == today, "plain task fallback date");
bool invalidDate = false;
try { PlanPaste.Parse("2026-02-30\n任务", today, today); } catch (ArgumentException) { invalidDate = true; }
Check(invalidDate, "invalid date rejects paste");
var bulk = new TodoStore(Path.Combine(data, "bulk"));
var existing = item with { Deadline = now.AddMinutes(-1), Completed = false };
bulk.SaveTask(existing);
bulk.SaveTask(new("completed", "Done", today.AddDays(1), null, 1, Completed:true));
var basePlan = bulk.Tasks();
bulk.AcknowledgeReminders([existing]); // Reminder fires while the planning window is open.
var desiredPlan = basePlan.Select(t => t.Id == existing.Id ? t with { Text = "改写内容" } : t).ToList();
desiredPlan.Add(new("future", "下月任务", new DateOnly(2026,11,20), null, 2));
bulk.SavePlan(basePlan, desiredPlan, new Dictionary<string,string>(), new Dictionary<string,string> { ["2026-11"] = "完成本月研究计划" });
var afterPlan = bulk.Tasks();
Check(afterPlan.Count == 3 && afterPlan.Single(t=>t.Id=="future").Date == new DateOnly(2026,11,20), "bulk future date persisted");
Check(afterPlan.Single(t=>t.Id=="completed").Completed, "bulk preserves completed state");
Check(afterPlan.Single(t=>t.Id=="one").Deadline == existing.Deadline && afterPlan.Single(t=>t.Id=="one").NotifiedFor == existing.Deadline.Value.UtcTicks, "bulk preserves deadline and concurrent reminder acknowledgment");
Check(bulk.MonthPlans()["2026-11"] == "完成本月研究计划", "monthly plan persisted");
var conflictBase = bulk.Tasks();
bulk.SaveTask(conflictBase[0] with { Text = "在另一处改写" });
bool conflict = false;
try { bulk.SavePlan(conflictBase, conflictBase.Select(t=>t with { Text=t.Text+"X" }).ToList(), bulk.MonthPlans(), new Dictionary<string,string> { ["2026-11"]="不应保存" }); } catch(InvalidOperationException) { conflict=true; }
Check(conflict && bulk.MonthPlans()["2026-11"] == "完成本月研究计划", "conflict rejects atomic bulk save");
var deleteBase = bulk.Tasks();
bulk.SaveTask(new("concurrent-new", "新任务", today.AddDays(2), null, 8));
bulk.SavePlan(deleteBase, deleteBase.Where(t=>t.Id!="future").ToList(), bulk.MonthPlans(), bulk.MonthPlans());
Check(!bulk.Tasks().Any(t=>t.Id=="future") && bulk.Tasks().Any(t=>t.Id=="concurrent-new"), "explicit removals preserve unrelated new tasks");
var transactionBase=bulk.Tasks();
using(var db = new SqliteConnection($"Data Source={Path.Combine(data,"bulk","todo.db")};Pooling=False")) {
 db.Open(); using var c=db.CreateCommand(); c.CommandText="CREATE TRIGGER fail_bulk BEFORE UPDATE OF text ON tasks BEGIN SELECT RAISE(ABORT,'fail bulk'); END;"; c.ExecuteNonQuery();
}
bool rolledBack=false;
try { bulk.SavePlan(transactionBase, transactionBase.Where(t=>t.Id!="completed").Select(t=>t with{Text=t.Text+"Y"}).ToList(), bulk.MonthPlans(), bulk.MonthPlans()); }
catch(SqliteException) { rolledBack=true; }
Check(rolledBack && bulk.Tasks().Any(t=>t.Id=="completed") && bulk.Tasks().Single(t=>t.Id=="one").Text==transactionBase.Single(t=>t.Id=="one").Text,"bulk failure rolls back deletion and updates");

var ordering = new TodoStore(Path.Combine(data,"ordering"));
for(int i=1;i<=4;i++) ordering.SaveTask(new("o"+i,"Task "+i,today,null,i,Deadline:now.AddHours(i),Remind:true));
ordering.SetTaskOrder(["o1","o3","o2","o4"]);
Check(string.Join(",",new TodoStore(Path.Combine(data,"ordering")).Tasks().Select(t=>t.Id))=="o1,o3,o2,o4","insert third between first and second persists");
ordering.SetTaskOrder(["o3","o1","o2","o4"]);
Check(ordering.Tasks().First().Id=="o3","move second above first");
Check(ordering.Tasks().Single(t=>t.Id=="o3").Deadline==now.AddHours(3) && ordering.Tasks().All(t=>!t.Completed),"reordering preserves deadlines and completion");
bool invalidOrder=false;
try{ordering.SetTaskOrder(["o1","o2"]);}catch(InvalidOperationException){invalidOrder=true;}
Check(invalidOrder && ordering.Tasks().First().Id=="o3","stale partial order rejected atomically");
using (var sqliteCheck = new SqliteConnection("Data Source=:memory:")) {
    sqliteCheck.Open(); using var versionCommand=sqliteCheck.CreateCommand(); versionCommand.CommandText="select sqlite_version()";
    var nativeVersion=Version.Parse((string)versionCommand.ExecuteScalar()!);
    Check(nativeVersion>=new Version(3,50,2),"native SQLite is beyond CVE-2025-6965 affected versions");
    Console.WriteLine("Native SQLite: "+nativeVersion);
}
Console.WriteLine($"ALL {passed} CHECKS PASSED");
