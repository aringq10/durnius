namespace Durnius.Web.Common;

/// <summary>
/// Represents either a successful service operation containing a value,
/// or a failed operation containing an error and optional messages.
/// </summary>
public sealed class ServiceResult<TValue, TError>
{
    private ServiceResult(
        bool succeeded,
        TValue? value,
        TError? error,
        IReadOnlyList<string> messages)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        Messages = messages;
    }

    public bool Succeeded { get; }
    public TValue? Value { get; }
    public TError? Error { get; }
    public IReadOnlyList<string> Messages { get; }

    public static ServiceResult<TValue, TError> Success(TValue value) =>
        new(true, value, default, []);

    public static ServiceResult<TValue, TError> Failure(
        TError error,
        IReadOnlyList<string>? messages = null) =>
        new(false, default, error, messages ?? []);

    /// <summary>
    /// Converts either branch of the result into one common return type.
    /// The caller supplies the behavior for success and failure.
    /// </summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, IReadOnlyList<string>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return Succeeded
            ? onSuccess(Value!)
            : onFailure(Error!, Messages);
    }
}