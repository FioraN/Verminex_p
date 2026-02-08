using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 枪械数值修改 Perk（极简版）
/// 只修改：
/// - 射程（maxRange）
/// - 伤害（baseDamage）
/// - 开火间隙：
///   - 基础射速（fireRate，间隙=1/fireRate）
///   - 单发额外冷却（semiFireCooldown，可选）
/// - 子弹下坠（bulletGravity）
/// 
/// 归属哪把枪：由 PerkManager 的 Perk 列表决定（GunA / GunB）
/// Perk 等级只存在于 PerkMeta 中，本脚本不使用等级参与逻辑
/// </summary>
public sealed class Perk_GunStatsOverride : MonoBehaviour
{
    [Header("前置条件")]
    [Tooltip("前置条件不满足时是否自动禁用该 Perk")]
    public bool disableIfPrereqMissing = true;

    [Header("射程")]
    [Tooltip("是否修改射程（CameraGunChannel.maxRange）")]
    public bool overrideRange = true;

    [Tooltip("射程倍率（例如 1.2 = +20%）")]
    [Min(0f)]
    public float rangeMultiplier = 1f;

    [Header("伤害")]
    [Tooltip("是否修改伤害（CameraGunChannel.baseDamage）")]
    public bool overrideDamage = true;

    [Tooltip("伤害倍率（例如 0.8 = -20%）")]
    [Min(0f)]
    public float damageMultiplier = 1f;

    [Header("开火间隙（基础射速）")]
    [Tooltip("是否修改基础射速（CameraGunChannel.fireRate）")]
    public bool overrideFireRate = false;

    [Tooltip("射速倍率（>1 更快；<1 更慢）。间隙=1/fireRate")]
    [Min(0f)]
    public float fireRateMultiplier = 1f;

    [Header("开火间隙（单发额外冷却）")]
    [Tooltip("是否修改单发额外冷却（CameraGunChannel.semiFireCooldown）")]
    public bool overrideSemiExtraCooldown = false;

    [Tooltip("单发额外冷却倍率（>1 更慢；<1 更快）")]
    [Min(0f)]
    public float semiExtraCooldownMultiplier = 1f;

    [Header("子弹下坠")]
    [Tooltip("是否修改子弹下坠（CameraGunChannel.bulletGravity）")]
    public bool overrideBulletGravity = true;

    [Tooltip("下坠倍率（>1 下坠更强；<1 下坠更弱）")]
    [Min(0f)]
    public float bulletGravityMultiplier = 1f;

    private PerkManager _perkManager;

    /// <summary>
    /// 保存枪械原始状态，用于移除 Perk 时恢复
    /// </summary>
    private struct GunState
    {
        public float maxRange;
        public float baseDamage;
        public float fireRate;
        public float semiFireCooldown;
        public float bulletGravity;
    }

    private readonly Dictionary<CameraGunChannel, GunState> _saved = new();
    private bool _applied;

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null) return;

        // 根据 Perk 所在列表判断属于哪把枪
        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0)
        {
            // 说明该 Perk 尚未被注册进任何枪的 Perk 列表
            enabled = false;
            return;
        }

        // 前置条件检查（如果有 PerkMeta）
        if (disableIfPrereqMissing && !_perkManager.PrerequisitesMet(gameObject, gunIndex))
        {
            enabled = false;
            return;
        }

        Apply(gunIndex);
    }

    private void OnDisable()
    {
        Revert();
    }

    private void OnDestroy()
    {
        Revert();
    }

    /// <summary>
    /// 通过 PerkManager 的 Perk 列表判断该 Perk 属于哪把枪
    /// </summary>
    private int ResolveGunIndexFromManager()
    {
        if (_perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    /// <summary>
    /// 应用数值修改
    /// </summary>
    private void Apply(int gunIndex)
    {
        if (_applied) return;

        var gunRefs = _perkManager.GetGun(gunIndex);
        var gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (gun == null) return;

        _saved.Clear();
        SaveIfNeeded(gun);

        var original = _saved[gun];

        if (overrideRange)
            gun.maxRange = Mathf.Max(0f, original.maxRange * rangeMultiplier);

        if (overrideDamage)
            gun.baseDamage = Mathf.Max(0f, original.baseDamage * damageMultiplier);

        if (overrideFireRate)
            gun.fireRate = Mathf.Max(0.01f, original.fireRate * fireRateMultiplier);

        if (overrideSemiExtraCooldown)
            gun.semiFireCooldown = Mathf.Max(0f, original.semiFireCooldown * semiExtraCooldownMultiplier);

        if (overrideBulletGravity)
            gun.bulletGravity = Mathf.Max(0f, original.bulletGravity * bulletGravityMultiplier);

        _applied = true;
    }

    /// <summary>
    /// 恢复原始数值
    /// </summary>
    private void Revert()
    {
        if (!_applied) return;

        foreach (var kv in _saved)
        {
            var gun = kv.Key;
            if (gun == null) continue;

            var s = kv.Value;
            gun.maxRange = s.maxRange;
            gun.baseDamage = s.baseDamage;
            gun.fireRate = s.fireRate;
            gun.semiFireCooldown = s.semiFireCooldown;
            gun.bulletGravity = s.bulletGravity;
        }

        _saved.Clear();
        _applied = false;
    }

    /// <summary>
    /// 保存枪械原始数值（只保存一次）
    /// </summary>
    private void SaveIfNeeded(CameraGunChannel gun)
    {
        if (gun == null) return;
        if (_saved.ContainsKey(gun)) return;

        _saved.Add(gun, new GunState
        {
            maxRange = gun.maxRange,
            baseDamage = gun.baseDamage,
            fireRate = gun.fireRate,
            semiFireCooldown = gun.semiFireCooldown,
            bulletGravity = gun.bulletGravity
        });
    }
}
