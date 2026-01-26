using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shock-A（株连/伤害连锁）
/// 被命中的目标若有 Shock：对附近最多 N 个敌人造成额外电伤害。
/// 连锁伤害默认不触发 OnHit（SkipHitEvent），避免无限连锁/触发命中类perk。
/// </summary>
public class ShockChainProc : MonoBehaviour
{
    [Header("Target Filter")]
    [Tooltip("用于筛选“敌人”Collider 的层级（建议只勾 Enemy 层）。")]
    public LayerMask enemyMask = ~0;

    private void OnEnable()
    {
        CombatEventHub.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= HandleHit;
    }

    private void HandleHit(CombatEventHub.HitEvent e)
    {
        if (e.target == null) return;

        // 命中的目标必须有 StatusContainer 且有 Shock 参数
        var sc = e.target.GetComponent<StatusContainer>();
        if (sc == null) sc = e.target.GetComponentInParent<StatusContainer>();
        if (sc == null) return;

        if (!sc.TryGetShockChainParams(out float chainDamage, out float radius, out int maxChains, out CameraGunChannel source))
            return;

        if (chainDamage <= 0f || radius <= 0.01f || maxChains <= 0) return;

        // 搜索半径内的敌人
        Collider[] cols = Physics.OverlapSphere(e.hitPoint == Vector3.zero ? e.target.transform.position : e.hitPoint, radius, enemyMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return;

        // 去重：同一个敌人可能有多个 collider
        var uniqueTargets = new HashSet<GameObject>();

        // 选择最多 maxChains 个（简单实现：按遍历顺序；你后面可改成“最近优先”）
        int applied = 0;
        for (int i = 0; i < cols.Length && applied < maxChains; i++)
        {
            var root = cols[i].GetComponentInParent<MonsterHealth>();
            if (root == null) continue;

            GameObject t = root.gameObject;
            if (t == e.target) continue; // 不打自己
            if (!uniqueTargets.Add(t)) continue;

            // 造成电伤害：不触发 OnHit，避免再次触发 Shock
            root.TakeDamage(new DamageInfo
            {
                source = source != null ? source : e.source,
                damage = chainDamage,
                isHeadshot = false,
                hitPoint = root.transform.position,
                hitCollider = null,
                flags = DamageFlags.SkipHitEvent
            });

            applied++;
        }
    }
}
