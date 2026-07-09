using System;
using System.Windows.Controls;

namespace OpenStickyMemos.Desktop.Services;

public interface INavigationService
{
    void NavigateTo<T>() where T : UserControl;
    void NavigateTo(Type viewType);
    void GoBack();
    event Action<Type>? NavigationChanged;
    object? NavigationParameter { get; set; }
}

public class NavigationService : INavigationService
{
    private readonly Stack<Type> _history = new();
    private readonly Dictionary<Type, UserControl> _cache = new();

    public event Action<Type>? NavigationChanged;
    public object? NavigationParameter { get; set; }

    public void NavigateTo<T>() where T : UserControl => NavigateTo(typeof(T));

    public void NavigateTo(Type viewType)
    {
        _history.Push(viewType);
        NavigationChanged?.Invoke(viewType);
    }

    public void GoBack()
    {
        if (_history.Count > 1)
        {
            _history.Pop(); // Remove current
            NavigationParameter = null;
            var previous = _history.Peek();
            NavigationChanged?.Invoke(previous);
        }
    }
}
