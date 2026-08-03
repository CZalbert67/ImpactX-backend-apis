using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Core.Sync;

public static class MobileSyncCursor
{
    public static string Compute(MobileSyncSnapshotDto snapshot)
    {
        var originalSnapshotId = snapshot.SnapshotId;
        var originalGeneratedAt = snapshot.GeneratedAtUtc;
        var originalCursor = snapshot.SyncCursor;
        var originalEmergencySyncAt = snapshot.EmergencyContacts.SynchronizedAtUtc;

        try
        {
            snapshot.SnapshotId = Guid.Empty;
            snapshot.GeneratedAtUtc = default;
            snapshot.SyncCursor = string.Empty;
            snapshot.EmergencyContacts.SynchronizedAtUtc = default;
            var json = JsonSerializer.Serialize(snapshot);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        }
        finally
        {
            snapshot.SnapshotId = originalSnapshotId;
            snapshot.GeneratedAtUtc = originalGeneratedAt;
            snapshot.SyncCursor = originalCursor;
            snapshot.EmergencyContacts.SynchronizedAtUtc = originalEmergencySyncAt;
        }
    }
}
