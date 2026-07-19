using ReunionPhotos.Web.Models;

namespace ReunionPhotos.Web.Services;

public class PinAuthService
{
    private readonly AirtableService _airtable;
    private readonly ILogger<PinAuthService> _logger;

    // Simple in-memory lockout tracking: PIN attempts are keyed by the
    // caller's IP address. This resets if the app restarts, which is fine
    // for a reunion-scale app - the goal is to slow down casual guessing,
    // not to defend a high-value target.
    private static readonly Dictionary<string, (int Failures, DateTimeOffset LockedUntil)> _attempts = new();
    private const int MaxFailuresBeforeLockout = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public PinAuthService(AirtableService airtable, ILogger<PinAuthService> logger)
    {
        _airtable = airtable;
        _logger = logger;
    }

    public bool IsLockedOut(string clientKey)
    {
        if (_attempts.TryGetValue(clientKey, out var entry))
        {
            return entry.Failures >= MaxFailuresBeforeLockout && DateTimeOffset.UtcNow < entry.LockedUntil;
        }
        return false;
    }

    public async Task<FamilyMember?> ValidatePinAsync(string pin, string clientKey, CancellationToken ct = default)
    {
        if (IsLockedOut(clientKey))
        {
            _logger.LogWarning("Login blocked for {ClientKey}: too many recent failed attempts", clientKey);
            return null;
        }

        var members = await _airtable.GetActiveFamilyMembersAsync(ct);

        foreach (var member in members)
        {
            if (!string.IsNullOrEmpty(member.PinHash) && BCrypt.Net.BCrypt.Verify(pin, member.PinHash))
            {
                _attempts.Remove(clientKey);
                return member;
            }
        }

        RecordFailure(clientKey);
        return null;
    }

    private void RecordFailure(string clientKey)
    {
        var current = _attempts.TryGetValue(clientKey, out var e) ? e : (Failures: 0, LockedUntil: DateTimeOffset.MinValue);
        var failures = current.Failures + 1;
        var lockedUntil = failures >= MaxFailuresBeforeLockout
            ? DateTimeOffset.UtcNow.Add(LockoutDuration)
            : DateTimeOffset.MinValue;
        _attempts[clientKey] = (failures, lockedUntil);
    }
}
