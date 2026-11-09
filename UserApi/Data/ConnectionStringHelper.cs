using Npgsql;

namespace UserApi.Data;

public static class ConnectionStringHelper
{
    /// <summary>
    /// Accepts either a standard Npgsql key/value connection string or a
    /// postgres:// / postgresql:// URL (as provided by Neon, Heroku, etc.)
    /// and returns a key/value string Npgsql can consume.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
        };

        foreach (var pair in System.Web.HttpUtility.ParseQueryString(uri.Query).AllKeys)
        {
            if (pair is null) continue;
            var value = System.Web.HttpUtility.ParseQueryString(uri.Query)[pair];
            switch (pair.ToLowerInvariant())
            {
                case "sslmode":
                    if (Enum.TryParse<SslMode>(value, true, out var sslMode))
                        builder.SslMode = sslMode;
                    break;
                case "channel_binding":
                    // Not a supported Npgsql keyword; ignore safely.
                    break;
            }
        }

        // Neon requires SSL; default to Require when not otherwise specified.
        if (builder.SslMode == SslMode.Disable)
            builder.SslMode = SslMode.Require;

        return builder.ConnectionString;
    }
}
