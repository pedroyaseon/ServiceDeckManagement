using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.HostTests;

public sealed class RestartBackoffTests
{
    [Fact]
    public void TryGetNextDelay_AppliesSaturatedExponentialBackoff()
    {
        var backoff = new RestartBackoff(Policy(maximumAttempts: 4));

        Assert.True(backoff.TryGetNextDelay(TimeSpan.FromSeconds(1), out var first));
        Assert.True(backoff.TryGetNextDelay(TimeSpan.FromSeconds(1), out var second));
        Assert.True(backoff.TryGetNextDelay(TimeSpan.FromSeconds(1), out var third));
        Assert.True(backoff.TryGetNextDelay(TimeSpan.FromSeconds(1), out var fourth));
        Assert.False(backoff.TryGetNextDelay(TimeSpan.FromSeconds(1), out _));

        Assert.Equal(TimeSpan.FromSeconds(2), first);
        Assert.Equal(TimeSpan.FromSeconds(4), second);
        Assert.Equal(TimeSpan.FromSeconds(5), third);
        Assert.Equal(TimeSpan.FromSeconds(5), fourth);
    }

    [Fact]
    public void TryGetNextDelay_ResetsAttemptsAfterStableRuntime()
    {
        var backoff = new RestartBackoff(Policy(maximumAttempts: 1));

        Assert.True(backoff.TryGetNextDelay(TimeSpan.Zero, out _));
        Assert.False(backoff.TryGetNextDelay(TimeSpan.Zero, out _));
        Assert.True(backoff.TryGetNextDelay(TimeSpan.FromMinutes(1), out var delay));
        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void TryGetNextDelay_RejectsDisabledPolicy()
    {
        var policy = Policy(maximumAttempts: 5) with { Enabled = false };
        var backoff = new RestartBackoff(policy);

        Assert.False(backoff.TryGetNextDelay(TimeSpan.Zero, out _));
    }

    private static RestartPolicyV1 Policy(int maximumAttempts) => new()
    {
        Enabled = true,
        MaximumAttempts = maximumAttempts,
        DelaySeconds = 2,
        MaximumDelaySeconds = 5,
        ResetAfterMinutes = 1
    };
}
