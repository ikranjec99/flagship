using System.Text;

namespace Flagship.Core.Business.Helpers;

public static class Keys
{
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static bool IsValid(string value) =>
        value.Length is >= 1 and <= 100
        && value.All(character => char.IsAsciiLetterLower(character)
            || char.IsDigit(character) || character == '-')
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal);
}
