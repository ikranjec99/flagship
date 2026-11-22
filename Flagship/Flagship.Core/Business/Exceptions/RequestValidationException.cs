namespace Flagship.Core.Business.Exceptions;

public sealed class RequestValidationException(
    string field,
    string message) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } =
        new Dictionary<string, string[]> { [field] = [message] };
}
