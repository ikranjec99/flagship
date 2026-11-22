namespace Flagship.Core.Business.Exceptions;

public sealed class EntityNotFoundException(string message) : Exception(message);
