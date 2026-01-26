using UnityEngine;

public static class DualGunResolver
{
    public static bool TryResolve(
        ref CameraGunDual dual,
        ref CameraGunChannel primary,
        ref CameraGunChannel secondary
    )
    {
        if (dual == null)
            dual = Object.FindFirstObjectByType<CameraGunDual>();

        if (dual != null)
        {
            if (primary == null) primary = dual.primary;
            if (secondary == null) secondary = dual.secondary;
        }

        if (primary != null && secondary != null)
            return true;

        var channels = Object.FindObjectsByType<CameraGunChannel>(FindObjectsSortMode.None);
        for (int i = 0; i < channels.Length; i++)
        {
            var ch = channels[i];
            if (primary == null && ch.role == CameraGunChannel.Role.Primary) primary = ch;
            if (secondary == null && ch.role == CameraGunChannel.Role.Secondary) secondary = ch;
            if (primary != null && secondary != null) return true;
        }

        return primary != null && secondary != null;
    }
}
