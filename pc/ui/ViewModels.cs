using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace RigBuddy.Ui;

public class Notify : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

static class HealthBrush
{
    public static Brush Of(Health h) => (Brush)Application.Current.FindResource(h switch
    {
        Health.Ok => "Ok", Health.Busy => "Warn", Health.Bad => "Bad", _ => "Idle",
    });
}

/** One configured serial device (gauge board, LED strip, display) with its live state. */
public sealed class DeviceVm : Notify
{
    string _name = "", _detail = "", _state = "", _message = "";
    Brush _dot = HealthBrush.Of(Health.Idle);

    public string Id { get; init; } = "";
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public string Message { get => _message; set => Set(ref _message, value); }
    public Brush Dot { get => _dot; set => Set(ref _dot, value); }
}

public sealed class ServiceVm : Notify
{
    string _state = "", _title = "";
    Brush _dot = HealthBrush.Of(Health.Idle);

    public string Name { get; init; } = "";
    public string Title { get => _title; set => Set(ref _title, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public Brush Dot { get => _dot; set => Set(ref _dot, value); }
}
