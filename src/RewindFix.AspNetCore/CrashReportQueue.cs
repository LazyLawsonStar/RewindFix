using System.Threading.Channels;
using RewindFix.AspNetCore.Models;

namespace RewindFix.AspNetCore;

public sealed class CrashReportQueue
{
    private readonly Channel<CrashReport> _channel;

    public CrashReportQueue(RewindFixOptions options)
    {
        _channel = Channel.CreateBounded<CrashReport>(
            new BoundedChannelOptions(options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public bool TryEnqueue(CrashReport report)
    {
        return _channel.Writer.TryWrite(report);
    }

    public IAsyncEnumerable<CrashReport> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(
            cancellationToken);
    }
}