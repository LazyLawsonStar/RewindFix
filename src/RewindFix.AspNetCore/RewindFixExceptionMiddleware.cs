using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RewindFix.AspNetCore.Models;

namespace RewindFix.AspNetCore;

public sealed class RewindFixExceptionMiddleware
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<RewindFixExceptionMiddleware> _logger;

    public RewindFixExceptionMiddleware(
        RequestDelegate next,
        ILogger<RewindFixExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var report = CreateCrashReport(context, exception);

            LogCrash(report, exception);

            await TrySendCrashAsync(report);

            throw;
        }
    }

    private void LogCrash(
        CrashReport report,
        Exception exception)
    {
        var json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        _logger.LogError(
            exception,
            "RewindFix captured crash {CrashId}\n{CrashReport}",
            report.CrashId,
            json);
    }

    private async Task TrySendCrashAsync(CrashReport report)
    {
        try
        {
            var response = await HttpClient.PostAsJsonAsync(
                "http://localhost:5000/api/crashes",
                report);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "RewindFix successfully sent crash {CrashId}",
                    report.CrashId);
            }
            else
            {
                _logger.LogWarning(
                    "RewindFix server returned HTTP {StatusCode} for crash {CrashId}",
                    (int)response.StatusCode,
                    report.CrashId);
            }
        }
        catch (Exception exception)
        {
            // RewindFix НИКОГДА не должен ломать приложение клиента,
            // если наш сервер недоступен.
            _logger.LogWarning(
                exception,
                "RewindFix could not send crash {CrashId}",
                report.CrashId);
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