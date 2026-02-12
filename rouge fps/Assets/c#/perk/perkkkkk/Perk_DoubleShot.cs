using System.Collections;
using UnityEngine;

/// <summary>
/// Each time this gun fires, schedule one extra round after a configurable delay.
/// - Works for hitscan and projectile.
/// - Does NOT re-raise OnFire (because it calls CameraGunChannel.FireBonusPellets).
/// - Optional: consume one extra ammo for the delayed shot.
/// </summary>
public sealed class Perk_DoubleShot : MonoBehaviour
{
    [Header("Prerequisites")]
    public bool disableIfPrereqMissing = true;

    [Header("Delay")]
    [Tooltip("Delay (seconds) before firing the extra shot.")]
    [Min(0f)] public float extraShotDelaySeconds = 0.08f;

    [Header("Ammo")]
    [Tooltip("If true, the delayed bonus shot consumes 1 extra ammo from magazine.")]
    public bool consumeExtraAmmo = false;

    private PerkManager _pm;
    private CameraGunChannel _gun;
    private bool _isActive;

    private void OnEnable()
    {
        _pm ??= FindFirstObjectByType<PerkManager>();
        if (_pm == null) { enabled = false; return; }

        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0) { enabled = false; return; }

        if (disableIfPrereqMissing && !_pm.PrerequisitesMet(gameObject, gunIndex))
        {
            enabled = false;
            return;
        }

        var gunRefs = _pm.GetGun(gunIndex);
        _gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (_gun == null) { enabled = false; return; }

        _isActive = true;
        CombatEventHub.OnFire += HandleFire;
    }

    private void OnDisable()
    {
        _isActive = false;
        CombatEventHub.OnFire -= HandleFire;
        StopAllCoroutines();
        _gun = null;
    }

    private int ResolveGunIndexFromManager()
    {
        // IMPORTANT: requires the perk INSTANCE to be in the list
        // (use PerkManager.InstantiatePerkToGun, do not drag prefab asset into the list).
        if (_pm.selectedPerksGunA.Contains(this)) return 0;
        if (_pm.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    private void HandleFire(CombatEventHub.FireEvent e)
    {
        if (_gun == null) return;
        if (e.source != _gun) return;

        int pellets = Mathf.Max(1, e.pellets);

        // Schedule the extra shot after delay
        StartCoroutine(DelayedBonusShot(pellets));
    }

    private IEnumerator DelayedBonusShot(int pellets)
    {
        float t = Mathf.Max(0f, extraShotDelaySeconds);
        if (t > 0f) yield return new WaitForSeconds(t);

        if (!_isActive) yield break;
        if (_gun == null) yield break;

        if (consumeExtraAmmo)
        {
            if (_gun.ammo == null) yield break;
            if (!_gun.ammo.HasAmmoInMag()) yield break;
            if (!_gun.ammo.TryConsumeOne()) yield break;
        }

        _gun.FireBonusPellets(pellets);
    }
}
