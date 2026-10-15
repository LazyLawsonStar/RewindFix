using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RewindFix.AspNetCore.Models;

namespace RewindFix.AspNetCore;

public sealed class RewindFixExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RewindFixExceptionMiddleware> _logger;
    private readonly CrashReportQueue _queue;

    public RewindFixExceptionMiddleware(
        RequestDelegate next,
        ILogger<RewindFixExceptionMiddleware> logger,
        CrashReportQueue queue)
    {
        _next = next;
        _logger = logger;
        _queue = queue;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var report = CreateCrashReport(
                context,
                exception);

            _logger.LogError(
                exception,
                "RewindFix captured crash {CrashId} at {Method} {Path}",
                report.CrashId,
                report.Request.Method,
                report.Request.Path);

            if (!_queue.TryEnqueue(report))
            {
                _logger.LogWarning(
                    "RewindFix crash queue is full. Crash {CrashId} was dropped.",
                    report.CrashId);
            }

            throw;
        }
    }

    private static CrashReport CreateCrashReport(
        HttpContext context,
        Exception exception)
    {
        return new CrashReport
        {
            CrashId = Guid.NewGuid(),

            Timestamp = DateTimeOffset.UtcNow,

            Request = new CrashRequest
            {
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                QueryString = context.Request.QueryString.Value
            },

            Exception = new CrashException
            {
                Type = exception.GetType().FullName
                    ?? exception.GetType().Name,

                Message = exception.Message,

                StackTrace = exception.StackTrace
            },

            TraceId = Activity.Current?.Id
                ?? context.TraceIdentifier
        };
    }
}