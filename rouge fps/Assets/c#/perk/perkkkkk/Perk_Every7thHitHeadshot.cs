using System.Collections.Generic;
using UnityEngine;

public sealed class Perk_Every7thHitHeadshot : GunPerkModifierBase
{
    [Header("Rule")]
    [Min(1)] public int everyNthHit = 7;

    [Header("Extra Damage Proc")]
    public bool extraDamageSkipHitEvent = true;
    public bool showExtraHitUI = false;
    [Min(0f)] public float maxExtraDamagePerHit = 99999f;

    [Header("Hitbox Multiplier Auto-Read (Strong Typed)")]
    [Tooltip("If true, head multiplier will be detected as the maximum HEAD hitbox multiplier found on the target root.")]
    public bool headIsMaxMultiplierOnTarget = true;

    [Tooltip("If true, the current hit's multiplier will be read from the Hitbox on the hit collider.")]
    public bool readBodyMultiplierFromHitCollider = true;

    [Tooltip("Fallback multiplier when the hit collider has no Hitbox.")]
    [Min(0.01f)] public float fallbackBodyMultiplier = 1f;

    [Tooltip("Fallback head multiplier when target scan fails.")]
    [Min(1f)] public float fallbackHeadMultiplier = 2f;

    private int _hitCount;

    // Cache head multipliers per target root instance id.
    private readonly Dictionary<int, float> _cachedHeadMulByRoot = new Dictionary<int, float>(64);

    public override void ApplyModifiers(CameraGunChannel source, Dictionary<GunStat, StatStack> stacks)
    {
        // No stat changes. This perk only adds extra damage on every Nth hit.
    }

    private void OnEnable()
    {
        base.OnEnable();
        CombatEventHub.OnHit += OnHit;
        _hitCount = 0;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= OnHit;
        base.OnDisable();
    }

    private void OnDestroy()
    {
        CombatEventHub.OnHit -= OnHit;
    }

    private void OnHit(CombatEventHub.HitEvent e)
    {
        if (!isActiveAndEnabled) return;
        if (SourceGun == null) return;

        // Only react to hits from this gun.
        if (e.source != SourceGun) return;

        // Ignore our own extra damage hit.
        if (extraDamageSkipHitEvent && (e.flags & DamageFlags.SkipHitEvent) != 0) return;

        Collider col = e.hitCollider;
        if (col == null) return;

        _hitCount++;
        if (everyNthHit <= 0) return;
        if ((_hitCount % everyNthHit) != 0) return;

        float dealtDamage = e.damage;
        if (dealtDamage <= 0f) return;

        // ----- Read multipliers (no guessing) -----
        float bodyMul = readBodyMultiplierFromHitCollider ? ReadFinalMultiplierFromHitCollider(e.source, col) : fallbackBodyMultiplier;
        if (bodyMul <= 0f) bodyMul = fallbackBodyMultiplier;

        float headMul = ResolveHeadMultiplierStrongTyped(e.source, e.target, col);
        if (headMul < 1f) headMul = fallbackHeadMultiplier;

        // If head multiplier is not larger than current, no need to add damage.
        float ratio = headMul / Mathf.Max(0.0001f, bodyMul);
        if (ratio <= 1f) return;

        float extraDamage = dealtDamage * (ratio - 1f);
        if (maxExtraDamagePerHit > 0f) extraDamage = Mathf.Min(extraDamage, maxExtraDamagePerHit);
        if (extraDamage <= 0f) return;

        Vector3 hitPoint = e.hitPoint != default
            ? e.hitPoint
            : col.ClosestPoint(SourceGun.transform.position);

        DamageInfo info = default;
        info.damage = extraDamage;
        info.flags = extraDamageSkipHitEvent ? DamageFlags.SkipHitEvent : DamageFlags.None;
        info.source = e.source;
        info.hitPoint = hitPoint;
        info.hitCollider = col;

        DamageResolver.ApplyHit(info, col, hitPoint, e.source, null, null, showExtraHitUI);
    }

    private float ResolveHeadMultiplierStrongTyped(CameraGunChannel gun, GameObject target, Collider hitCol)
    {
        if (!headIsMaxMultiplierOnTarget) return fallbackHeadMultiplier;

        Transform root = FindTargetRootStrongTyped(target, hitCol);
        if (root == null) return fallbackHeadMultiplier;

        int key = root.gameObject.GetInstanceID();
        if (_cachedHeadMulByRoot.TryGetValue(key, out float cached))
            return cached;

        float maxHeadMul = 0f;

        // Scan Hitbox components only (no guessing fields on random components).
        Hitbox[] hitboxes = root.GetComponentsInChildren<Hitbox>(true);
        if (hitboxes != null)
        {
            var mgr = HitboxMultiplierManager.Instance;

            for (int i = 0; i < hitboxes.Length; i++)
            {
                Hitbox hb = hitboxes[i];
                if (hb == null) continue;
                if (hb.part != Hitbox.Part.Head) continue;

                float m = mgr != null ? mgr.ResolveMultiplier(gun, hb) : Mathf.Max(0f, hb.damageMultiplier);
                if (m > maxHeadMul) maxHeadMul = m;
            }
        }

        if (maxHeadMul < 1f) maxHeadMul = fallbackHeadMultiplier;

        _cachedHeadMulByRoot[key] = maxHeadMul;
        return maxHeadMul;
    }

    private static Transform FindTargetRootStrongTyped(GameObject target, Collider hitCol)
    {
        if (target != null) return target.transform;
        if (hitCol != null) return hitCol.transform.root;
        return null;
    }

    private static float ReadFinalMultiplierFromHitCollider(CameraGunChannel gun, Collider col)
    {
        if (col == null) return 0f;

        // Must be a Hitbox on the collider or in its parents.
        Hitbox hb = col.GetComponentInParent<Hitbox>();
        if (hb == null) return 0f;

        var mgr = HitboxMultiplierManager.Instance;
        if (mgr != null) return mgr.ResolveMultiplier(gun, hb);

        return Mathf.Max(0f, hb.damageMultiplier);
    }
}