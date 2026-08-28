using System.Collections.Generic;

namespace AtlasNOC.Domain.Entities;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; } = new List<string>();

    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            ((List<string>)Errors).Add(error);
    }

    public static ValidationResult Success() => new();

    public static ValidationResult Fail(params string[] errors)
    {
        var result = new ValidationResult();
        foreach (var error in errors) result.AddError(error);
        return result;
    }
}
