using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewindFix.AspNetCore.Models;

namespace RewindFix.AspNetCore;

internal sealed class CrashReportSender
    : BackgroundService
{
    private readonly CrashReportQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RewindFixOptions _options;
    private readonly ILogger<CrashReportSender> _logger;

    public CrashReportSender(
        CrashReportQueue queue,
        IHttpClientFactory httpClientFactory,
        RewindFixOptions options,
        ILogger<CrashReportSender> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (
            var report in _queue.ReadAllAsync(stoppingToken))
        {
            await SendAsync(
                report,
                stoppingToken);
        }
    }

    private async Task SendAsync(
        CrashReport report,
        CancellationToken stoppingToken)
    {
        try
        {
            var client =
                _httpClientFactory.CreateClient("RewindFix");

            using var timeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken);

            timeout.CancelAfter(_options.SendTimeout);

            var endpoint = new Uri(
                new Uri(
                    _options.ServerUrl.TrimEnd('/') + "/"),
                "api/crashes");

            var response =
                await client.PostAsJsonAsync(
                    endpoint,
                    report,
                    timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "RewindFix sent crash {CrashId}",
                    report.CrashId);

                return;
            }

            _logger.LogWarning(
                "RewindFix server returned HTTP {StatusCode} for crash {CrashId}",
                (int)response.StatusCode,
                report.CrashId);
        }
        catch (OperationCanceledException)
            when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RewindFix timed out while sending crash {CrashId}",
                report.CrashId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "RewindFix could not send crash {CrashId}",
                report.CrashId);
        }
    }
}