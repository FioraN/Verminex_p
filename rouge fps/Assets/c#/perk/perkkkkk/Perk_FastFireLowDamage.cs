using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fast fire rate + lower damage (multiplicative).
/// - fireRate *= fireRateMultiplier ( > 1 => faster )
/// - baseDamage *= damageMultiplier ( < 1 => lower )
///
/// Designed to stack safely:
/// multiple perks on the same gun will combine by multiplying their multipliers,
/// and restore correctly when any perk is disabled/destroyed.
/// </summary>
public sealed class Perk_FastFireLowDamage : MonoBehaviour
{
    [Header("Prerequisites")]
    [Tooltip("If true, disable this perk when prerequisites are not met.")]
    public bool disableIfPrereqMissing = true;

    [Header("Multipliers")]
    [Tooltip("Multiply CameraGunChannel.fireRate. > 1 means faster.")]
    [Min(0.01f)] public float fireRateMultiplier = 2.5f;

    [Tooltip("Multiply CameraGunChannel.baseDamage. < 1 means lower damage.")]
    [Min(0.0f)] public float damageMultiplier = 0.6f;

    private PerkManager _pm;
    private CameraGunChannel _gun;

    // Per-gun stacking state
    private sealed class GunState
    {
        public float originalFireRate;
        public float originalBaseDamage;
        public readonly Dictionary<int, Entry> entries = new(); // key = perk instance id
    }

    private struct Entry
    {
        public float fr;
        public float dmg;
    }

    private static readonly Dictionary<CameraGunChannel, GunState> s_states = new();

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

        RegisterAndApply(_gun);
    }

    private void OnDisable()
    {
        UnregisterAndApply(_gun);
        _gun = null;
    }

    private void OnDestroy()
    {
        // Ensure cleanup even if destroyed while enabled
        UnregisterAndApply(_gun);
        _gun = null;
    }

    private int ResolveGunIndexFromManager()
    {
        // IMPORTANT: this works only when the perk instance is actually in the list
        // (use PerkManager.InstantiatePerkToGun, not dragging prefab into list).
        if (_pm.selectedPerksGunA.Contains(this)) return 0;
        if (_pm.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    private void RegisterAndApply(CameraGunChannel gun)
    {
        if (!s_states.TryGetValue(gun, out var state))
        {
            state = new GunState
            {
                originalFireRate = gun.fireRate,
                originalBaseDamage = gun.baseDamage
            };
            s_states.Add(gun, state);
        }

        int id = GetInstanceID();
        state.entries[id] = new Entry
        {
            fr = Mathf.Max(0.01f, fireRateMultiplier),
            dmg = Mathf.Max(0.0f, damageMultiplier)
        };

        RecomputeAndApply(gun, state);
    }

    private void UnregisterAndApply(CameraGunChannel gun)
    {
        if (gun == null) return;
        if (!s_states.TryGetValue(gun, out var state)) return;

        int id = GetInstanceID();
        state.entries.Remove(id);

        if (state.entries.Count == 0)
        {
            // Restore originals and remove state
            gun.fireRate = Mathf.Max(0.01f, state.originalFireRate);
            gun.baseDamage = Mathf.Max(0.0f, state.originalBaseDamage);
            s_states.Remove(gun);
            return;
        }

        RecomputeAndApply(gun, state);
    }

    private static void RecomputeAndApply(CameraGunChannel gun, GunState state)
    {
        float frMul = 1f;
        float dmgMul = 1f;

        foreach (var kv in state.entries)
        {
            frMul *= kv.Value.fr;
            dmgMul *= kv.Value.dmg;
        }

        gun.fireRate = Mathf.Max(0.01f, state.originalFireRate * frMul);
        gun.baseDamage = Mathf.Max(0.0f, state.originalBaseDamage * dmgMul);
    }
}
