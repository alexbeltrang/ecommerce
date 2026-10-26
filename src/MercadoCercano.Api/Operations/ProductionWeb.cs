namespace MercadoCercano.Api.Operations;

public static class ProductionWeb
{
    public static void SetHeaders(HttpResponse response)
    {
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        response.Headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; " +
            "connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    }

    public static bool IsApiPath(PathString path) => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
