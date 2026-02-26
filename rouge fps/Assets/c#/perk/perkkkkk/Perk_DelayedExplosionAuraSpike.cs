using System.Collections.Generic;
using UnityEngine;

public sealed class Perk_DelayedExplosionAuraSpike : GunPerkModifierBase
{
    [Header("Gun Damage (GunStatContext)")]
    [Tooltip("Multiply bullet base damage (direct hit damage).")]
    [Min(0f)] public float damageMultiplier = 1.35f;

    [Header("Delayed Explosion")]
    [Min(0.01f)] public float delaySeconds = 0.45f;
    [Min(0.01f)] public float radius = 3.5f;

    [Tooltip("If true, explosion damage is based on the direct hit damage dealt (post-calculation).")]
    public bool explosionDamageFromHit = true;

    [Tooltip("Explosion damage = (hitDealtDamage * explosionDamageMultiplier) when explosionDamageFromHit is true.")]
    [Min(0f)] public float explosionDamageMultiplier = 1.0f;

    [Tooltip("Explosion damage (flat) when explosionDamageFromHit is false.")]
    [Min(0f)] public float explosionDamageFlat = 25f;

    [Tooltip("Layers used to find enemies for AoE damage.")]
    public LayerMask enemyMask = ~0;

    [Tooltip("Explosion damage uses SkipHitEvent to avoid recursive procs.")]
    public bool explosionSkipHitEvent = true;

    [Header("Aura Spike VFX (optional)")]
    [Tooltip("Spawned immediately on hit and destroyed on explosion.")]
    public GameObject auraSpikeVfxPrefab;

    [Tooltip("Spawned at explosion time.")]
    public GameObject explosionVfxPrefab;

    public Transform vfxParent;
    [Min(0f)] public float vfxAutoDestroySeconds = 6f;

    public bool scaleVfxByRadius = true;
    public bool vfxUseDiameter = true;
    [Min(0f)] public float vfxScalePerUnit = 0.5f;

    [Header("Priority")]
    public int priority = 0;
    public override int Priority => priority;

    // Global (per-process) suspend: ensures NO recursion even if flags are dropped by DamageResolver.
    private static int s_explosionDepth = 0;

    // Same-frame suppression: if HitEvent is raised after ApplyHit (still same frame), this blocks it.
    private int _lastExplosionFrame = -999999;

    public override void ApplyModifiers(CameraGunChannel source, Dictionary<GunStat, StatStack> stacks)
    {
        if (source == null || stacks == null) return;

        if (stacks.TryGetValue(GunStat.Damage, out var dmg))
        {
            dmg.mul *= Mathf.Max(0f, damageMultiplier);
            stacks[GunStat.Damage] = dmg;
        }
    }

    private void OnEnable()
    {
        base.OnEnable();
        CombatEventHub.OnHit += OnHit;
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

        // Only this gun.
        if (e.source != SourceGun) return;

        // Hard recursion guard (does NOT rely on HitEvent.flags).
        if (s_explosionDepth > 0) return;

        // Same-frame guard: if our explosion caused hits in the same frame, ignore.
        if (Time.frameCount == _lastExplosionFrame) return;

        // Soft guard (if your pipeline propagates flags correctly, this also works).
        if (explosionSkipHitEvent && (e.flags & DamageFlags.SkipHitEvent) != 0) return;

        Collider hitCol = e.hitCollider;
        if (hitCol == null) return;

        Vector3 hitPoint = e.hitPoint;
        if (hitPoint == default)
            hitPoint = hitCol.ClosestPoint(SourceGun.transform.position);

        float dealtDamage = 0f;
        if (explosionDamageFromHit)
        {
            // Prefer strong-typed e.damage if present in your HitEvent.
            dealtDamage = Mathf.Max(0f, e.damage);
        }

        // Create anchor
        var anchorGO = new GameObject("DelayedExplosion_AuraSpike");
        var anchor = anchorGO.AddComponent<DelayedExplosionAnchor>();
        anchor.Init(
            owner: this,
            source: SourceGun,
            attachTo: hitCol.transform,
            localPos: hitCol.transform.InverseTransformPoint(hitPoint),
            worldPos: hitPoint,
            delay: Mathf.Max(0.01f, delaySeconds),
            radius: Mathf.Max(0.01f, radius),
            enemyMask: enemyMask,
            skipHitEvent: explosionSkipHitEvent,
            explosionFromHit: explosionDamageFromHit,
            hitDealtDamage: dealtDamage,
            explosionMul: Mathf.Max(0f, explosionDamageMultiplier),
            explosionFlat: Mathf.Max(0f, explosionDamageFlat),
            auraVfxPrefab: auraSpikeVfxPrefab,
            explosionVfxPrefab: explosionVfxPrefab,
            vfxParent: vfxParent,
            vfxAutoDestroySeconds: vfxAutoDestroySeconds,
            scaleVfxByRadius: scaleVfxByRadius,
            vfxUseDiameter: vfxUseDiameter,
            vfxScalePerUnit: vfxScalePerUnit
        );
    }

    // -------------------------
    // Helper component
    // -------------------------
    private sealed class DelayedExplosionAnchor : MonoBehaviour
    {
        private Perk_DelayedExplosionAuraSpike _owner;
        private CameraGunChannel _source;
        private Transform _attachTo;
        private Vector3 _localPos;
        private Vector3 _worldPos;

        private float _delay;
        private float _radius;
        private LayerMask _enemyMask;
        private bool _skipHitEvent;

        private bool _explosionFromHit;
        private float _hitDealtDamage;
        private float _explosionMul;
        private float _explosionFlat;

        private GameObject _auraVfx;
        private GameObject _explosionVfxPrefab;
        private Transform _vfxParent;
        private float _vfxAutoDestroySeconds;

        private bool _scaleVfxByRadius;
        private bool _vfxUseDiameter;
        private float _vfxScalePerUnit;

        // NonAlloc + dedupe
        private readonly Collider[] _overlaps = new Collider[128];
        private readonly HashSet<int> _dedupe = new HashSet<int>(128);

        public void Init(
            Perk_DelayedExplosionAuraSpike owner,
            CameraGunChannel source,
            Transform attachTo,
            Vector3 localPos,
            Vector3 worldPos,
            float delay,
            float radius,
            LayerMask enemyMask,
            bool skipHitEvent,
            bool explosionFromHit,
            float hitDealtDamage,
            float explosionMul,
            float explosionFlat,
            GameObject auraVfxPrefab,
            GameObject explosionVfxPrefab,
            Transform vfxParent,
            float vfxAutoDestroySeconds,
            bool scaleVfxByRadius,
            bool vfxUseDiameter,
            float vfxScalePerUnit
        )
        {
            _owner = owner;
            _source = source;

            _attachTo = attachTo;
            _localPos = localPos;
            _worldPos = worldPos;

            _delay = delay;
            _radius = radius;
            _enemyMask = enemyMask;
            _skipHitEvent = skipHitEvent;

            _explosionFromHit = explosionFromHit;
            _hitDealtDamage = hitDealtDamage;
            _explosionMul = explosionMul;
            _explosionFlat = explosionFlat;

            _explosionVfxPrefab = explosionVfxPrefab;
            _vfxParent = vfxParent;
            _vfxAutoDestroySeconds = vfxAutoDestroySeconds;

            _scaleVfxByRadius = scaleVfxByRadius;
            _vfxUseDiameter = vfxUseDiameter;
            _vfxScalePerUnit = vfxScalePerUnit;

            // Place anchor.
            if (_attachTo != null)
            {
                transform.SetParent(_attachTo, worldPositionStays: false);
                transform.localPosition = _localPos;
            }
            else
            {
                transform.position = _worldPos;
            }

            // Spawn aura vfx now.
            if (auraVfxPrefab != null)
            {
                Transform parent = _vfxParent != null ? _vfxParent : transform;
                _auraVfx = Instantiate(auraVfxPrefab, transform.position, Quaternion.identity, parent);

                if (_scaleVfxByRadius)
                {
                    float units = _vfxUseDiameter ? (_radius * 2f) : _radius;
                    float s = Mathf.Max(0f, units * _vfxScalePerUnit);
                    _auraVfx.transform.localScale = _auraVfx.transform.localScale * s;
                }
            }

            Invoke(nameof(Explode), _delay);
        }

        private void Explode()
        {
            if (_auraVfx != null) Destroy(_auraVfx);

            Vector3 center = (_attachTo != null) ? transform.position : _worldPos;

            // Explosion vfx
            if (_explosionVfxPrefab != null)
            {
                Transform parent = _vfxParent != null ? _vfxParent : null;
                GameObject vfx = parent != null
                    ? Instantiate(_explosionVfxPrefab, center, Quaternion.identity, parent)
                    : Instantiate(_explosionVfxPrefab, center, Quaternion.identity);

                if (_scaleVfxByRadius)
                {
                    float units = _vfxUseDiameter ? (_radius * 2f) : _radius;
                    float s = Mathf.Max(0f, units * _vfxScalePerUnit);
                    vfx.transform.localScale = vfx.transform.localScale * s;
                }

                if (_vfxAutoDestroySeconds > 0f)
                    Destroy(vfx, _vfxAutoDestroySeconds);
            }

            float explosionDamage = _explosionFromHit
                ? Mathf.Max(0f, _hitDealtDamage * _explosionMul)
                : Mathf.Max(0f, _explosionFlat);

            if (explosionDamage > 0f)
            {
                // Mark same-frame suppression on owner BEFORE we apply hits.
                if (_owner != null) _owner._lastExplosionFrame = Time.frameCount;

                // Hard recursion suspend: blocks OnHit from spawning new anchors even if flags are dropped.
                s_explosionDepth++;
                try
                {
                    _dedupe.Clear();

                    int count = Physics.OverlapSphereNonAlloc(
                        center,
                        Mathf.Max(0.01f, _radius),
                        _overlaps,
                        _enemyMask,
                        QueryTriggerInteraction.Ignore
                    );

                    DamageFlags flags = _skipHitEvent ? DamageFlags.SkipHitEvent : DamageFlags.None;

                    for (int i = 0; i < count; i++)
                    {
                        Collider col = _overlaps[i];
                        if (col == null) continue;

                        // Dedupe per-enemy (multi collider)
                        int id;
                        var mh = col.GetComponentInParent<MonsterHealth>();
                        if (mh != null)
                        {
                            id = mh.gameObject.GetInstanceID();
                        }
                        else
                        {
                            var dmg = col.GetComponentInParent<IDamageableEx>();
                            if (dmg == null) continue;
                            var comp = dmg as Component;
                            id = comp != null ? comp.gameObject.GetInstanceID() : col.transform.root.gameObject.GetInstanceID();
                        }

                        if (!_dedupe.Add(id)) continue;

                        // Fill DamageInfo fully (improves propagation in your pipeline)
                        DamageInfo info = default;
                        info.source = _source;
                        info.damage = explosionDamage;
                        info.flags = flags;
                        info.hitPoint = col.ClosestPoint(center);
                        info.hitCollider = col;

                        DamageResolver.ApplyHit(info, col, info.hitPoint, _source, null, null, true);
                    }
                }
                finally
                {
                    s_explosionDepth--;
                }
            }

            Destroy(gameObject);
        }
    }
}