namespace RewindFix.Api.Security;

public sealed class AdminApiKeyFilter
    : IEndpointFilter
{
    private const string HeaderName =
        "X-RewindFix-Key";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator =
            context.HttpContext.RequestServices
                .GetRequiredService<ApiKeyValidator>();

        var key =
            context.HttpContext.Request.Headers[HeaderName]
                .FirstOrDefault();

        if (!validator.IsValidAdminKey(key))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}