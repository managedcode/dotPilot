using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace ManagedCode.Communication;

public sealed class Problem
{
    private readonly Dictionary<string, IReadOnlyList<string>> _validationErrors = new(StringComparer.Ordinal);

    private Problem(string errorCode, string detail, int statusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        ErrorCode = errorCode;
        Detail = detail;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }

    public string Detail { get; }

    public int StatusCode { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors => new ReadOnlyDictionary<string, IReadOnlyList<string>>(_validationErrors);

    public static Problem Create<TCode>(TCode code, string detail, int statusCode)
        where TCode : struct, Enum
    {
        return new Problem(code.ToString(), detail, statusCode);
    }

    public void AddValidationError(string fieldName, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (_validationErrors.TryGetValue(fieldName, out var existingErrors))
        {
            _validationErrors[fieldName] = [.. existingErrors, errorMessage];
            return;
        }

        _validationErrors[fieldName] = [errorMessage];
    }

    public bool HasErrorCode<TCode>(TCode code)
        where TCode : struct, Enum
    {
        return string.Equals(ErrorCode, code.ToString(), StringComparison.Ordinal);
    }

    public bool InvalidField(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return _validationErrors.ContainsKey(fieldName);
    }
}

[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "The result contract intentionally exposes static success/failure factories to preserve the existing lightweight communication API.")]
public sealed class Result<T>
{
    private Result(T? value, Problem? problem)
    {
        Value = value;
        Problem = problem;
    }

    public T? Value { get; }

    public Problem? Problem { get; }

    public bool IsSuccess => Problem is null;

    public bool IsFailed => !IsSuccess;

    public bool HasProblem => Problem is not null;

    public static Result<T> Succeed(T value)
    {
        return new Result<T>(value, problem: null);
    }

    public static Result<T> Fail(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new Result<T>(value: default, problem);
    }
}
