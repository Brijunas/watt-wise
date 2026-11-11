namespace WattWise.Api.Security;

public static class OriginCheckApplicationBuilderExtensions
{
    public static IApplicationBuilder UseOriginCheck(this IApplicationBuilder app)
    {
        return app.UseMiddleware<OriginCheckMiddleware>();
    }
}
