using LogScope.App.Mvvm;
using LogScope.Core.Persistence;
using LogScope.Core.Visualization;

namespace LogScope.App.ViewModels;

/// <summary>Editable row for the color-rules editor (UR-10).</summary>
public sealed class ColorRuleEditViewModel : ViewModelBase
{
    public static IReadOnlyList<ColorRule.MatchKind> Kinds { get; } =
        [ColorRule.MatchKind.FieldValue, ColorRule.MatchKind.MessageContaining, ColorRule.MatchKind.Regex];

    private ColorRule.MatchKind _kind;
    public ColorRule.MatchKind Kind { get => _kind; set => SetField(ref _kind, value); }

    private string? _fieldName;
    public string? FieldName { get => _fieldName; set => SetField(ref _fieldName, value); }

    private string _matchValue = string.Empty;
    public string MatchValue { get => _matchValue; set => SetField(ref _matchValue, value); }

    private string _background = "#5A1F1F";
    public string Background { get => _background; set { if (SetField(ref _background, value)) OnPropertyChanged(nameof(Background)); } }

    private int _priority;
    public int Priority { get => _priority; set => SetField(ref _priority, value); }

    public ColorRuleEditViewModel() { }

    public ColorRuleEditViewModel(ColorRuleDto dto)
    {
        _kind = dto.Kind;
        _fieldName = dto.FieldName;
        _matchValue = dto.MatchValue;
        _background = dto.Background ?? dto.FieldHighlight ?? "#5A1F1F";
        _priority = dto.Priority;
    }

    public ColorRuleDto ToDto() => new()
    {
        Kind = Kind,
        FieldName = FieldName,
        MatchValue = MatchValue,
        Background = Background,
        Priority = Priority,
    };
}
