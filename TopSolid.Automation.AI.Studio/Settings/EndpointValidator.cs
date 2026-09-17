namespace TopSolid.Automation.AI.Studio.Settings;

public static class EndpointValidator
{
    public static Uri Validate(string? value, string label = "Server URL", bool allowRemoteHttp = false)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException($"{label} must be an absolute HTTP or HTTPS URL.");

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException($"{label} must not contain credentials, a query, or a fragment.");

        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback && !allowRemoteHttp)
            throw new ArgumentException($"{label} must use HTTPS. HTTP is supported only for a loopback server such as localhost.");

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    internal static string KeyScope(string? value)
    {
        try { return Validate(value, "Cloud base URL").AbsoluteUri; }
        catch (ArgumentException) { return value?.Trim() ?? ""; }
    }
}
