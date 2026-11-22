namespace Flagship.Core.Business.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
