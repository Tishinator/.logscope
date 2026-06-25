using LogScope.App.Mvvm;

namespace LogScope.App.ViewModels;

/// <summary>A toggleable column for the show/hide column chooser (SR-10).</summary>
public sealed class ColumnToggle : ViewModelBase
{
    private readonly Action<ColumnToggle>? _onChanged;

    public string Name { get; }

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (SetField(ref _isVisible, value)) _onChanged?.Invoke(this); }
    }

    public ColumnToggle(string name, bool isVisible, Action<ColumnToggle>? onChanged)
    {
        Name = name;
        _isVisible = isVisible;
        _onChanged = onChanged;
    }
}
