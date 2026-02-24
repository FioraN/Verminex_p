using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Perk: 子弹命中敌人后，随机“弹射”命中另一个敌人
/// - 默认使用“安全模式”：直接对 MonsterHealth 造成一次额外伤害（SkipHitEvent），避免无限递归
/// - 可选“触发 OnHit 模式”：用 DamageResolver.ApplyHit 走完整命中流程（会触发其他 OnHit perk）
/// </summary>
public sealed class Perk_RandomRicochetOnHit : MonoBehaviour
{
    [Header("Ricochet Settings")]
    [Min(0.01f)]
    [Tooltip("在命中点附近搜索可弹射目标的半径")]
    public float searchRadius = 6f;

    [Range(0f, 1f)]
    [Tooltip("每次命中触发弹射的概率")]
    public float procChance = 0.35f;

    [Min(0f)]
    [Tooltip("弹射伤害 = 原命中伤害 * multiplier + flatBonus")]
    public float damageMultiplier = 0.6f;

    [Min(0f)]
    public float flatBonusDamage = 0f;

    [Tooltip("用于筛选敌人的层级（建议只勾 Enemy 层）")]
    public LayerMask enemyMask = ~0;

    [Header("Mode")]
    [Tooltip("为 true：弹射命中会走 DamageResolver.ApplyHit 并触发 OnHit；为 false：直接 MonsterHealth.TakeDamage（更安全）")]
    public bool ricochetTriggersHitEvent = false;

    [Header("Safety")]
    [Tooltip("为 true：若未正确注册到 PerkManager（不在 selectedPerksGunA/B）则自动禁用")]
    public bool disableIfNotAllowed = true;

    [Tooltip("为 true：要求满足 PerkMeta 前置（PerkManager.PrerequisitesMet）")]
    public bool requirePrerequisites = true;

    private PerkManager _perkManager;
    private CameraGunChannel _boundChannel;

    // 递归防护（只在 ricochetTriggersHitEvent=true 时真正需要）
    private int _reentryDepth = 0;

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

        CombatEventHub.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= HandleHit;
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

        // 只响应绑定那把枪
        if (_boundChannel != null && e.source != _boundChannel) return;

        // 防递归：如果我们自己触发了一个“二次命中事件”，这里直接跳过
        if (_reentryDepth > 0) return;

        if (procChance < 1f && Random.value > procChance) return;

        Vector3 center = e.hitPoint;
        if (center == Vector3.zero) center = e.target.transform.position;

        // 找范围内敌人
        Collider[] cols = Physics.OverlapSphere(center, searchRadius, enemyMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return;

        // 去重 + 排除直击目标
        var candidates = new List<MonsterHealth>(16);
        var seen = new HashSet<MonsterHealth>();

        for (int i = 0; i < cols.Length; i++)
        {
            var mh = cols[i].GetComponentInParent<MonsterHealth>();
            if (mh == null) continue;
            if (mh.gameObject == e.target) continue;
            if (!seen.Add(mh)) continue;

            candidates.Add(mh);
        }

        if (candidates.Count == 0) return;

        // 随机选一个弹射目标
        MonsterHealth target = candidates[Random.Range(0, candidates.Count)];
        if (target == null) return;

        float ricochetDamage = (e.damage * damageMultiplier) + flatBonusDamage;
        Debug.Log($"[Ricochet] inst={GetInstanceID()} obj={name} src={(e.source ? e.source.name : "null")} base={e.damage} mult={damageMultiplier} flat={flatBonusDamage} => ricochet={ricochetDamage}");
        if (ricochetDamage <= 0f) return;

        // 计算一个“看起来像命中”的点（用于 UI/VFX/命中事件）
        Vector3 targetPoint = target.transform.position;

        if (!ricochetTriggersHitEvent)
        {
            // 安全模式：不走 OnHit，避免无限链
            target.TakeDamage(new DamageInfo
            {
                source = e.source,
                damage = ricochetDamage,
                isHeadshot = false,
                hitPoint = targetPoint,
                hitCollider = null,
                flags = DamageFlags.SkipHitEvent
            });

            return;
        }

        // 触发 OnHit 模式：走 DamageResolver.ApplyHit
        // 需要一个 collider；尽量取目标身上的任意 Collider（非 trigger 优先）
        Collider targetCol = FindBestCollider(target, targetPoint);
        if (targetCol == null)
        {
            // 没 collider 就退回安全模式
            target.TakeDamage(new DamageInfo
            {
                source = e.source,
                damage = ricochetDamage,
                isHeadshot = false,
                hitPoint = targetPoint,
                hitCollider = null,
                flags = DamageFlags.SkipHitEvent
            });
            return;
        }

        try
        {
            _reentryDepth++;

            DamageResolver.ApplyHit(
                baseInfo: new DamageInfo
                {
                    source = e.source,
                    damage = ricochetDamage,
                    isHeadshot = false,
                    hitPoint = targetPoint,
                    hitCollider = targetCol,
                    flags = DamageFlags.None
                },
                hitCol: targetCol,
                hitPoint: targetCol.ClosestPoint(targetPoint),
                source: e.source,
                armorPayload: e.armorPayload,
                statusPayload: e.statusPayload,
                showHitUI: false
            );
        }
        finally
        {
            _reentryDepth--;
        }
    }

    private static Collider FindBestCollider(MonsterHealth mh, Vector3 preferPoint)
    {
        Collider[] cols = mh.GetComponentsInChildren<Collider>(true);
        if (cols == null || cols.Length == 0) return null;

        Collider best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null) continue;

            // Prefer non-trigger
            float score = c.isTrigger ? -1000f : 0f;

            // Prefer body hitbox, avoid head hitbox
            Hitbox hb = c.GetComponent<Hitbox>();
            if (hb != null)
            {
                if (hb.part == Hitbox.Part.Body) score += 10000f;
                else score -= 5000f; // head
            }
            else
            {
                // Colliders without Hitbox are still acceptable but less ideal than Body
                score += 1000f;
            }

            // Prefer closer collider to the intended hit point
            Vector3 cp = c.ClosestPoint(preferPoint);
            float dist = Vector3.Distance(cp, preferPoint);
            score -= dist * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

}
