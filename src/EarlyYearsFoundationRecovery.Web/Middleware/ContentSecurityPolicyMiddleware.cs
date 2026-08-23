namespace EarlyYearsFoundationRecovery.Web.Middleware;

public static class ContentSecurityPolicyMiddleware
{
    public const string Policy =
        "default-src 'none'; " +
        "base-uri 'self'; " +
        "connect-src 'self'; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'; " +
        "frame-src https://www.youtube.com https://player.vimeo.com; " +
        "img-src 'self' https://images.ctfassets.net data:; " +
        "media-src 'self' https://player.vimeo.com https://i.vimeocdn.com; " +
        "object-src 'none'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "upgrade-insecure-requests; " +
        "block-all-mixed-content";

    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = Policy;
            await next(context);
        });
}
