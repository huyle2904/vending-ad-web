using Npgsql;

namespace VendingAdSystem.Infrastructure;

internal static class PostgresConnectionStringResolver
{
    public static string Normalize(string connectionStringOrUrl)
    {
        if (string.IsNullOrWhiteSpace(connectionStringOrUrl))
            throw new InvalidOperationException("PostgreSQL connection string is missing.");

        var value = connectionStringOrUrl.Trim();
        if (!TryParsePostgresUrl(value, out var normalized))
            return value;

        return normalized;
    }

    private static bool TryParsePostgresUrl(string value, out string normalized)
    {
        normalized = string.Empty;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
        };

        ApplyUserInfo(builder, uri.UserInfo);
        ApplyQuery(builder, uri.Query);

        normalized = builder.ConnectionString;
        return true;
    }

    private static void ApplyUserInfo(NpgsqlConnectionStringBuilder builder, string userInfo)
    {
        if (string.IsNullOrWhiteSpace(userInfo))
            return;

        var segments = userInfo.Split(':', 2);
        if (segments.Length >= 1)
            builder.Username = Uri.UnescapeDataString(segments[0]);
        if (segments.Length == 2)
            builder.Password = Uri.UnescapeDataString(segments[1]);
    }

    private static void ApplyQuery(NpgsqlConnectionStringBuilder builder, string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return;

        var pairs = queryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pair in pairs)
        {
            var pieces = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0]);
            var value = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;

            switch (key.Trim().ToLowerInvariant())
            {
                case "host":
                    builder.Host = value;
                    break;
                case "port":
                    if (int.TryParse(value, out var port))
                        builder.Port = port;
                    break;
                case "database":
                case "dbname":
                    builder.Database = value;
                    break;
                case "username":
                case "user":
                case "user id":
                case "userid":
                    builder.Username = value;
                    break;
                case "password":
                    builder.Password = value;
                    break;
                case "sslmode":
                    builder["SSL Mode"] = value;
                    break;
                case "trust server certificate":
                case "trustservercertificate":
                    builder["Trust Server Certificate"] = value;
                    break;
                default:
                    builder[key] = value;
                    break;
            }
        }
    }
}
