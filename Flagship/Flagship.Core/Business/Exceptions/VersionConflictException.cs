namespace Flagship.Core.Business.Exceptions;

public sealed class VersionConflictException(int currentVersion)
    : Exception("This flag was changed by someone else. Reload before saving.")
{
    public int CurrentVersion { get; } = currentVersion;
}
