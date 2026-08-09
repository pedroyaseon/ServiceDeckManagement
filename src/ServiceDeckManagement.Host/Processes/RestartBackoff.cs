using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Host.Processes;

/// <summary>
/// Limita reinícios consecutivos e calcula backoff exponencial saturado.
/// </summary>
public sealed class RestartBackoff(RestartPolicyV1 policy)
{
    private int attempts;

    public int Attempts => attempts;

    public bool TryGetNextDelay(
        TimeSpan previousRuntime,
        out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (!policy.Enabled || policy.MaximumAttempts == 0)
        {
            return false;
        }

        if (previousRuntime >= TimeSpan.FromMinutes(policy.ResetAfterMinutes))
        {
            attempts = 0;
        }

        if (attempts >= policy.MaximumAttempts)
        {
            return false;
        }

        var exponent = Math.Min(attempts, 30);
        var multiplier = 1L << exponent;
        var seconds = Math.Min(
            checked((long)policy.DelaySeconds * multiplier),
            policy.MaximumDelaySeconds);
        attempts++;
        delay = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
