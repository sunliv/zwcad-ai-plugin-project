using System;
using System.Collections.Generic;
using System.Linq;

namespace ZwcadAi.Core;

public sealed class ValidationResult
{
    private ValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public static ValidationResult Success()
    {
        return new ValidationResult(Array.Empty<ValidationIssue>());
    }

    public static ValidationResult Failure(IEnumerable<ValidationIssue> issues)
    {
        if (issues == null)
        {
            throw new ArgumentNullException(nameof(issues));
        }

        return new ValidationResult(issues.ToArray());
    }
}

public sealed class ValidationIssue
{
    public ValidationIssue(string code, string path, string message, ValidationSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Validation issue code is required.", nameof(code));
        }

        Code = code;
        Path = path ?? string.Empty;
        Message = message ?? string.Empty;
        Severity = severity;
    }

    public string Code { get; }

    public string Path { get; }

    public string Message { get; }

    public ValidationSeverity Severity { get; }
}

public enum ValidationSeverity
{
    Error = 0,
    Warning = 1
}
