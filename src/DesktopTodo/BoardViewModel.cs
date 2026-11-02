using System.ComponentModel;

namespace DesktopTodo;

internal sealed class BoardViewModel : INotifyPropertyChanged
{
    public TodoStore Store { get; }
    public DateOnly? SelectedDate { get; set; }
    public UserSettings Settings { get; private set; }
    public IReadOnlyList<TodoItem> Tasks { get; private set; } = [];
    public IReadOnlyList<TaskGroup> Groups { get; private set; } = [];
    public IReadOnlyList<BoardSection> Sections { get; private set; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public BoardViewModel(TodoStore store) { Store = store; Settings = store.Settings(); Refresh(); }
    public void Refresh()
    {
        Settings = Store.Settings(); Tasks = Store.Tasks(); Groups = Store.Groups();
        Sections = BoardBuilder.Build(Tasks, Groups, DateOnly.FromDateTime(DateTime.Now), SelectedDate);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sections)));
    }
    public int NextPosition => Tasks.Count == 0 ? 0 : Tasks.Max(t => t.Position) + 1;
}
