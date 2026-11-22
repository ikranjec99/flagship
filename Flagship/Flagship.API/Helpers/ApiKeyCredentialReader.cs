namespace Flagship.API.Helpers;

public static class ApiKeyCredentialReader
{
    public static string? ReadBearerToken(HttpRequest request)
    {
        var authorization = request.Headers["Authorization"].ToString();
        const string bearer = "Bearer ";
        return authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearer.Length..].Trim()
            : null;
    }
}
