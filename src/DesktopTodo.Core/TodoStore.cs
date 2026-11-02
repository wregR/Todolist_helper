using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Globalization;

namespace DesktopTodo.Core;

public sealed class TodoStore
{
    private readonly string connectionString;
    public string DataDirectory { get; }
    public TodoStore(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(DataDirectory);
        connectionString = new SqliteConnectionStringBuilder {
            DataSource = Path.Combine(DataDirectory, "todo.db"), ForeignKeys = true, Pooling = false
        }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS groups(id TEXT PRIMARY KEY, date TEXT NOT NULL, name TEXT NOT NULL, position INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS tasks(id TEXT PRIMARY KEY, text TEXT NOT NULL, date TEXT NOT NULL,
                group_id TEXT REFERENCES groups(id) ON DELETE CASCADE, position INTEGER NOT NULL,
                completed INTEGER NOT NULL, deadline TEXT, remind INTEGER NOT NULL, notified_for INTEGER);
            CREATE INDEX IF NOT EXISTS idx_tasks_date ON tasks(date);
            CREATE TABLE IF NOT EXISTS settings(id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS month_plans(month TEXT PRIMARY KEY, notes TEXT NOT NULL);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = sql;
        foreach (var (key, value) in args) c.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return c;
    }
    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public List<TodoItem> Tasks()
    {
        using var db = Open(); using var c = Command(db, null, "SELECT id,text,date,group_id,position,completed,deadline,remind,notified_for FROM tasks ORDER BY position");
        using var r = c.ExecuteReader(); var result = new List<TodoItem>();
        while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), DateOnly.ParseExact(r.GetString(2), "yyyy-MM-dd"),
            r.IsDBNull(3) ? null : r.GetString(3), r.GetInt32(4), r.GetBoolean(5),
            r.IsDBNull(6) ? null : DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture),
            r.GetBoolean(7), r.IsDBNull(8) ? null : r.GetInt64(8)));
        return result;
    }
    public List<TaskGroup> Groups()
    {
        using var db = Open(); using var c = Command(db, null, "SELECT id,date,name,position FROM groups ORDER BY position");
        using var r = c.ExecuteReader(); var result = new List<TaskGroup>();
        while (r.Read()) result.Add(new(r.GetString(0), DateOnly.ParseExact(r.GetString(1), "yyyy-MM-dd"), r.GetString(2), r.GetInt32(3)));
        return result;
    }
    public UserSettings Settings()
    {
        using var db = Open(); using var c = Command(db, null, "SELECT json FROM settings WHERE id=1");
        var json = c.ExecuteScalar() as string;
        return json is null ? new() : JsonSerializer.Deserialize<UserSettings>(json) ?? new();
    }
    public void SaveSettings(UserSettings settings)
    {
        using var db = Open(); using var c = Command(db, null, "INSERT INTO settings VALUES(1,@json) ON CONFLICT(id) DO UPDATE SET json=@json",
            ("@json", JsonSerializer.Serialize(settings)));
        c.ExecuteNonQuery();
    }
    public TodoItem SaveTask(TodoItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Text)) throw new ArgumentException("任务内容不能为空。");
        item = item with { Text = item.Text.Trim(), Remind = item.Remind && item.Deadline.HasValue };
        using var db = Open(); using var tx = db.BeginTransaction();
        if (item.GroupId != null)
        {
            using var g = Command(db, tx, "SELECT date FROM groups WHERE id=@id", ("@id", item.GroupId));
            if (g.ExecuteScalar() as string != Day(item.Date)) throw new ArgumentException("分组与任务必须属于同一天。");
        }
        using (var old = Command(db, tx, "SELECT deadline,remind,notified_for FROM tasks WHERE id=@id", ("@id", item.Id)))
        using (var r = old.ExecuteReader())
        {
            if (r.Read())
            {
                var prior = r.IsDBNull(0) ? (DateTimeOffset?)null : DateTimeOffset.Parse(r.GetString(0), CultureInfo.InvariantCulture);
                item = item with { NotifiedFor = prior == item.Deadline && r.GetBoolean(1) == item.Remind
                    ? (r.IsDBNull(2) ? null : r.GetInt64(2)) : null };
            }
        }
        using var c = Command(db, tx, """
            INSERT INTO tasks VALUES(@id,@text,@date,@group,@position,@completed,@deadline,@remind,@notified)
            ON CONFLICT(id) DO UPDATE SET text=@text,date=@date,group_id=@group,position=@position,
                completed=@completed,deadline=@deadline,remind=@remind,notified_for=@notified
            """, ("@id", item.Id), ("@text", item.Text), ("@date", Day(item.Date)), ("@group", item.GroupId),
            ("@position", item.Position), ("@completed", item.Completed), ("@deadline", item.Deadline?.ToString("O")),
            ("@remind", item.Remind), ("@notified", item.NotifiedFor));
        c.ExecuteNonQuery(); tx.Commit(); return item;
    }
    public TaskGroup SaveGroup(TaskGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name)) throw new ArgumentException("分组名称不能为空。");
        using var db = Open(); using var c = Command(db, null,
            "INSERT INTO groups VALUES(@id,@date,@name,@pos) ON CONFLICT(id) DO UPDATE SET name=@name",
            ("@id", group.Id), ("@date", Day(group.Date)), ("@name", group.Name.Trim()), ("@pos", group.Position));
        c.ExecuteNonQuery(); return group;
    }
    public void Complete(string id, bool value)
    {
        using var db = Open(); using var c = Command(db, null, "UPDATE tasks SET completed=@value WHERE id=@id", ("@value", value), ("@id", id));
        c.ExecuteNonQuery();
    }
    public void DeleteTask(string id) { using var db = Open(); using var c = Command(db, null, "DELETE FROM tasks WHERE id=@id", ("@id", id)); c.ExecuteNonQuery(); }
    public void DeleteGroup(string id) { using var db = Open(); using var c = Command(db, null, "DELETE FROM groups WHERE id=@id", ("@id", id)); c.ExecuteNonQuery(); }
    public void Reorder(TodoItem a, TodoItem b)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        using var c = Command(db, tx, "UPDATE tasks SET position=CASE id WHEN @a THEN @bp ELSE @ap END WHERE id IN(@a,@b)",
            ("@a", a.Id), ("@b", b.Id), ("@ap", a.Position), ("@bp", b.Position));
        c.ExecuteNonQuery(); tx.Commit();
    }
    public void AcknowledgeReminders(IEnumerable<TodoItem> items)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        foreach (var item in items)
        {
            using var c = Command(db, tx, "UPDATE tasks SET notified_for=@ticks WHERE id=@id AND deadline=@deadline",
                ("@ticks", item.Deadline?.UtcTicks), ("@id", item.Id), ("@deadline", item.Deadline?.ToString("O")));
            c.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public void Backup(DateOnly today)
    {
        var dir = Path.Combine(DataDirectory, "backups"); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Day(today) + ".db");
        if (File.Exists(path)) return;
        var temporary = path + ".tmp";
        using (var source = Open())
        using (var dest = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ToString()))
        { dest.Open(); source.BackupDatabase(dest); }
        File.Move(temporary, path, true);
        foreach (var old in Directory.GetFiles(dir, "????-??-??.db").OrderDescending().Skip(7)) File.Delete(old);
    }

    public Dictionary<string, string> MonthPlans()
    {
        using var db = Open(); using var command = Command(db, null, "SELECT month,notes FROM month_plans");
        using var r = command.ExecuteReader(); var result = new Dictionary<string, string>();
        while (r.Read()) result[r.GetString(0)] = r.GetString(1);
        return result;
    }
    // Merge only user edits in one transaction. A background reminder acknowledgment is not an edit conflict.
    public void SavePlan(IReadOnlyList<TodoItem> baseline, IReadOnlyList<TodoItem> desired,
        IReadOnlyDictionary<string, string> originalNotes, IReadOnlyDictionary<string, string> notes)
    {
        if (desired.Any(t => string.IsNullOrWhiteSpace(t.Text))) throw new ArgumentException("任务内容不能为空。");
        if (desired.Select(t => t.Id).Distinct().Count() != desired.Count) throw new ArgumentException("任务标识重复。");
        var old = baseline.ToDictionary(t => t.Id);
        var wanted = desired.ToDictionary(t => t.Id);
        var changed = desired.Where(t => !old.TryGetValue(t.Id, out var previous) || t != previous).ToList();
        var removed = baseline.Where(t => !wanted.ContainsKey(t.Id)).ToList();
        using var db = Open(); using var tx = db.BeginTransaction();
        foreach (var item in changed.Concat(removed))
        {
            using var read = Command(db, tx, "SELECT text,date,group_id,position,completed,deadline,remind,notified_for FROM tasks WHERE id=@id", ("@id", item.Id));
            using var r = read.ExecuteReader();
            if (!old.TryGetValue(item.Id, out var expected))
            { if (r.Read()) throw new InvalidOperationException("计划已发生变化，请重新打开总编辑后保存。"); continue; }
            if (!r.Read()) throw new InvalidOperationException("有任务已被删除，请重新打开总编辑后保存。");
            var current = new TodoItem(item.Id, r.GetString(0), DateOnly.ParseExact(r.GetString(1), "yyyy-MM-dd"),
                r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3), r.GetBoolean(4),
                r.IsDBNull(5) ? null : DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture),
                r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetInt64(7));
            if ((current with { NotifiedFor = expected.NotifiedFor }) != expected)
                throw new InvalidOperationException("有任务已在其他位置修改，请重新打开总编辑，避免覆盖。");
        }
        foreach (var month in notes.Keys)
        {
            var previous = originalNotes.GetValueOrDefault(month, "");
            if (notes[month] == previous) continue;
            using var check = Command(db, tx, "SELECT notes FROM month_plans WHERE month=@month", ("@month", month));
            if ((check.ExecuteScalar() as string ?? "") != previous) throw new InvalidOperationException("月计划已发生变化，请重新打开总编辑后保存。");
        }
        foreach (var item in removed)
        { using var delete = Command(db, tx, "DELETE FROM tasks WHERE id=@id", ("@id", item.Id)); delete.ExecuteNonQuery(); }
        foreach (var item in changed)
        {
            long? notified = null;
            using (var read = Command(db, tx, "SELECT deadline,remind,notified_for FROM tasks WHERE id=@id", ("@id", item.Id)))
            using (var r = read.ExecuteReader())
            {
                if (r.Read() && !r.IsDBNull(0) && DateTimeOffset.Parse(r.GetString(0), CultureInfo.InvariantCulture) == item.Deadline &&
                    r.GetBoolean(1) == item.Remind && !r.IsDBNull(2)) notified = r.GetInt64(2);
            }
            using var write = Command(db, tx, """
                INSERT INTO tasks VALUES(@id,@text,@date,@group,@position,@completed,@deadline,@remind,@notified)
                ON CONFLICT(id) DO UPDATE SET text=@text,date=@date,group_id=@group,position=@position,
                    completed=@completed,deadline=@deadline,remind=@remind,notified_for=@notified
                """, ("@id", item.Id), ("@text", item.Text.Trim()), ("@date", Day(item.Date)), ("@group", item.GroupId),
                ("@position", item.Position), ("@completed", item.Completed), ("@deadline", item.Deadline?.ToString("O")),
                ("@remind", item.Remind && item.Deadline.HasValue), ("@notified", notified));
            write.ExecuteNonQuery();
        }
        foreach (var (month, text) in notes)
        {
            if (text == originalNotes.GetValueOrDefault(month, "")) continue;
            using var write = Command(db, tx, "INSERT INTO month_plans VALUES(@month,@notes) ON CONFLICT(month) DO UPDATE SET notes=@notes",
                ("@month", month), ("@notes", text));
            write.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SetTaskOrder(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new ArgumentException("无效的排序。");
        using var db = Open(); using var tx = db.BeginTransaction();
        string? date = null; bool? completed = null;
        foreach (var id in ids)
        {
            using var read = Command(db, tx, "SELECT date,completed FROM tasks WHERE id=@id", ("@id", id));
            using var r = read.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("任务已变化，请重新排序。");
            date ??= r.GetString(0); completed ??= r.GetBoolean(1);
            if (date != r.GetString(0) || completed != r.GetBoolean(1)) throw new ArgumentException("只能在同一天、同一完成状态内排序。");
        }
        using (var count = Command(db, tx, "SELECT COUNT(*) FROM tasks WHERE date=@date AND completed=@done", ("@date", date), ("@done", completed)))
            if (Convert.ToInt32(count.ExecuteScalar()) != ids.Count) throw new InvalidOperationException("任务已变化，请重新排序。");
        for (int i=0;i<ids.Count;i++)
        {
            using var update=Command(db,tx,"UPDATE tasks SET position=@position WHERE id=@id",("@position",i),("@id",ids[i]));
            update.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
