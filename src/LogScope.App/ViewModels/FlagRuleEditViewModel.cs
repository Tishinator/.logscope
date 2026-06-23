using LogScope.App.Mvvm;
using LogScope.Core.Persistence;
using LogScope.Core.Visualization;

namespace LogScope.App.ViewModels;

/// <summary>Editable row for the flag-rules editor (UR-11). A blank field means "any field".</summary>
public sealed class FlagRuleEditViewModel : ViewModelBase
{
    public static IReadOnlyList<FlagRule.MatchKind> Kinds { get; } =
        [FlagRule.MatchKind.FieldValue, FlagRule.MatchKind.Contains, FlagRule.MatchKind.Regex];

    private FlagRule.MatchKind _kind = FlagRule.MatchKind.Contains;
    public FlagRule.MatchKind Kind { get => _kind; set => SetField(ref _kind, value); }

    private string? _fieldName;
    public string? FieldName { get => _fieldName; set => SetField(ref _fieldName, value); }

    private string _matchValue = string.Empty;
    public string MatchValue { get => _matchValue; set => SetField(ref _matchValue, value); }

    public FlagRuleEditViewModel() { }

    public FlagRuleEditViewModel(FlagRuleDto dto)
    {
        _kind = dto.Kind;
        _fieldName = dto.FieldName;
        _matchValue = dto.MatchValue;
    }

    public FlagRuleDto ToDto() => new()
    {
        Kind = Kind,
        FieldName = string.IsNullOrWhiteSpace(FieldName) ? null : FieldName.Trim(),
        MatchValue = MatchValue,
    };
}
