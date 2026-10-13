using Microsoft.AspNetCore.Builder;

namespace RewindFix.AspNetCore;

public static class RewindFixApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRewindFix(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<RewindFixExceptionMiddleware>();
    }
}