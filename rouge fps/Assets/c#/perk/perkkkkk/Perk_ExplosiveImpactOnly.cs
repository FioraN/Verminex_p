using System.Collections.Generic;
using UnityEngine;

public sealed class Perk_ExplosiveImpactOnly : MonoBehaviour
{
    [Header("Explosion")]
    [Min(0.01f)]
    public float radius = 3.5f;

    [Min(0f)]
    public float explosionDamage = 25f;

    [Tooltip("Layers used to find enemies for AoE damage.")]
    public LayerMask enemyMask = ~0;

    [Tooltip("If true, explosion damage will use DamageFlags.SkipHitEvent to avoid recursive perk procs.")]
    public bool explosionSkipHitEvent = true;

    [Header("VFX")]
    public GameObject explosionVfxPrefab;
    public Transform vfxParent;

    [Min(0f)]
    public float vfxAutoDestroySeconds = 6f;

    public bool scaleVfxByRadius = true;

    [Tooltip("If true, scale by diameter (2r). If false, scale by radius (r).")]
    public bool vfxUseDiameter = true;

    [Min(0f)]
    public float vfxScalePerUnit = 0.5f;

    [Header("Gun Penalties")]
    [Range(0.05f, 1f)]
    public float fireRateMultiplier = 0.6f;

    [Range(0.05f, 1f)]
    public float bulletSpeedMultiplier = 0.7f;

    [Header("Safety")]
    [Tooltip("If true, disable this component when it is not properly registered in PerkManager.")]
    public bool disableIfNotAllowed = true;

    [Tooltip("If true, requires PerkManager.PrerequisitesMet(gameObject, gunIndex) to be true.")]
    public bool requirePrerequisites = true;

    private PerkManager _perkManager;
    private CameraGunChannel _boundChannel;

    private float _originalBaseDamage;
    private float _originalFireRate;
    private float _originalBulletSpeed;

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        if (requirePrerequisites && !_perkManager.PrerequisitesMet(gameObject, gunIndex))
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        var gun = _perkManager.GetGun(gunIndex);
        _boundChannel = gun != null ? gun.cameraGunChannel : null;

        if (_boundChannel == null)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        // ImpactOnly：直击伤害为 0（只靠爆炸伤害）
        _originalBaseDamage = _boundChannel.baseDamage;
        _boundChannel.baseDamage = 0f;

        // 惩罚
        _originalFireRate = _boundChannel.fireRate;
        _originalBulletSpeed = _boundChannel.bulletSpeed;

        _boundChannel.fireRate = Mathf.Max(0.01f, _originalFireRate * Mathf.Clamp(fireRateMultiplier, 0.05f, 1f));
        _boundChannel.bulletSpeed = Mathf.Max(0.01f, _originalBulletSpeed * Mathf.Clamp(bulletSpeedMultiplier, 0.05f, 1f));

        CombatEventHub.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= HandleHit;

        if (_boundChannel != null)
        {
            _boundChannel.baseDamage = _originalBaseDamage;
            _boundChannel.fireRate = _originalFireRate;
            _boundChannel.bulletSpeed = _originalBulletSpeed;
        }

        _boundChannel = null;
    }

    private int ResolveGunIndexFromManager()
    {
        if (_perkManager == null) return -1;

        if (_perkManager.selectedPerksGunA != null && _perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB != null && _perkManager.selectedPerksGunB.Contains(this)) return 1;

        return -1;
    }

    private void HandleHit(CombatEventHub.HitEvent e)
    {
        if (!isActiveAndEnabled) return;
        if (e.target == null) return;

        // 只响应这把枪造成的命中
        if (_boundChannel != null && e.source != _boundChannel) return;

        // 关键：过滤二次伤害（比如爆炸/连锁）引起的 OnHit，避免递归卡死
        if ((e.flags & DamageFlags.SkipHitEvent) != 0) return;

        if (radius <= 0.01f) return;
        if (explosionDamage <= 0f)
        {
            // 仍然可以播特效（命中就爆），这里按需
            SpawnVfx(GetCenter(e));
            return;
        }

        Vector3 center = GetCenter(e);

        // 命中即爆：先播特效（不管炸到没炸到）
        SpawnVfx(center);

        Collider[] cols = Physics.OverlapSphere(center, radius, enemyMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return;

        // 去重：同一个敌人多个 collider 只吃一次爆炸伤害
        var uniqueTargets = new HashSet<MonsterHealth>();

        for (int i = 0; i < cols.Length; i++)
        {
            var hitCol = cols[i];
            if (hitCol == null) continue;

            var mh = hitCol.GetComponentInParent<MonsterHealth>();
            if (mh == null) continue;
            if (!uniqueTargets.Add(mh)) continue;

            var aoeInfo = new DamageInfo
            {
                source = e.source,
                damage = explosionDamage,
                isHeadshot = false,
                hitPoint = center,
                hitCollider = hitCol,

                // 爆炸伤害必须 SkipHitEvent：防递归
                flags = explosionSkipHitEvent ? DamageFlags.SkipHitEvent : DamageFlags.None
            };

            // 关键：把原命中事件带来的 armorPayload 传进去，才能走“先扣护甲再扣血”
            // 如果你项目里 HitEvent 字段名不是 armorPayload，把 HitEvent 定义贴出来我帮你改名
            DamageResolver.ApplyHit(
                baseInfo: aoeInfo,
                hitCol: hitCol,
                hitPoint: center,
                source: aoeInfo.source,
                armorPayload: e.armorPayload,
                statusPayload: null,
                showHitUI: false
            );
        }
    }

    private Vector3 GetCenter(CombatEventHub.HitEvent e)
    {
        Vector3 center = e.hitPoint;
        if (center == Vector3.zero && e.target != null)
            center = e.target.transform.position;
        return center;
    }

    private void SpawnVfx(Vector3 position)
    {
        if (explosionVfxPrefab == null) return;

        var go = Instantiate(explosionVfxPrefab, position, Quaternion.identity, vfxParent);

        if (scaleVfxByRadius)
        {
            float baseValue = vfxUseDiameter ? (radius * 2f) : radius;
            float s = Mathf.Max(0.0001f, baseValue * vfxScalePerUnit);
            go.transform.localScale = Vector3.one * s;
        }

        if (vfxAutoDestroySeconds > 0f)
        {
            Destroy(go, vfxAutoDestroySeconds);
        }
    }
}