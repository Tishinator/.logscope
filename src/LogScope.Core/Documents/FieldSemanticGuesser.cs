namespace LogScope.Core.Documents;

/// <summary>Guesses a field's semantic type from its name (UR-06), e.g. "Timestamp" → Timestamp.</summary>
public static class FieldSemanticGuesser
{
    public static FieldSemanticType Guess(string fieldName)
    {
        var n = fieldName.Trim().ToLowerInvariant();
        if (n.Contains("time") || n.Contains("date") || n == "ts") return FieldSemanticType.Timestamp;
        if (n.Contains("level") || n == "lvl" || n == "severity") return FieldSemanticType.Level;
        if (n.Contains("message") || n == "msg" || n == "text") return FieldSemanticType.Message;
        if (n.Contains("module") || n.Contains("logger") || n.Contains("tag") || n.Contains("component")) return FieldSemanticType.Module;
        if (n.Contains("thread") || n == "tid") return FieldSemanticType.Thread;
        if (n.Contains("device")) return FieldSemanticType.DeviceId;
        if (n.Contains("test")) return FieldSemanticType.TestCase;
        if (n.Contains("run")) return FieldSemanticType.RunId;
        if (n.Contains("result")) return FieldSemanticType.Result;
        return FieldSemanticType.Generic;
    }

    /// <summary>Assigns guessed types to every named field that isn't already typed.</summary>
    public static void ApplyGuessedTypes(LogProfile profile)
    {
        foreach (var name in profile.FieldNames)
        {
            if (!profile.FieldTypes.ContainsKey(name))
            {
                var guess = Guess(name);
                if (guess != FieldSemanticType.Generic)
                    profile.SetFieldType(name, guess);
            }
        }
    }
}
