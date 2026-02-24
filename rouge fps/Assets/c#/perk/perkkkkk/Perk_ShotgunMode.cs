using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 散弹模式 Perk
/// 将所属枪械的射击方式改为散弹（Shotgun）
/// Perk 属于哪把枪，由 PerkManager 的 Perk 列表决定
/// </summary>
public sealed class Perk_ShotgunMode : MonoBehaviour
{
    [Header("散弹参数")]

    [Tooltip("每次射击发射的弹丸数量")]
    [Min(1)]
    public int pelletsPerShot = 6;

    [Tooltip("是否保持单次射击的总伤害不变（会按弹丸数平分伤害）")]
    public bool keepTotalDamageConstant = true;

    [Tooltip("总伤害倍率（在是否平分伤害之后再乘）")]
    [Min(0f)]
    public float totalDamageMultiplier = 1f;

    [Header("前置条件")]

    [Tooltip("如果前置条件不满足，是否自动禁用该 Perk")]
    public bool disableIfPrereqMissing = true;

    private PerkManager _perkManager;

    /// <summary>
    /// 用于保存枪械原始状态，便于 Perk 移除时恢复
    /// </summary>
    private struct GunState
    {
        public CameraGunChannel.ShotType shotType;
        public int pelletsPerShot;
        public float baseDamage;
    }

    private readonly Dictionary<CameraGunChannel, GunState> _savedStates = new();
    private bool _applied;

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null) return;

        // 根据 Perk 所在的列表判断它属于哪把枪
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
    /// 通过 PerkManager 的列表判断该 Perk 属于哪把枪
    /// </summary>
    private int ResolveGunIndexFromManager()
    {
        if (_perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    /// <summary>
    /// 应用散弹效果
    /// </summary>
    private void Apply(int gunIndex)
    {
        if (_applied) return;

        var gunRefs = _perkManager.GetGun(gunIndex);
        var gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (gun == null) return;

        _savedStates.Clear();
        SaveStateIfNeeded(gun);

        int pellets = Mathf.Max(1, pelletsPerShot);

        // 设置射击模式为散弹
        gun.shotType = CameraGunChannel.ShotType.Shotgun;
        gun.pelletsPerShot = pellets;

        // 计算新的基础伤害
        var original = _savedStates[gun];
        float newBaseDamage;

        if (keepTotalDamageConstant)
        {
            // 将总伤害均分到每个弹丸
            newBaseDamage = (original.baseDamage / pellets) * totalDamageMultiplier;
        }
        else
        {
            // 不平分伤害，直接整体乘倍率
            newBaseDamage = original.baseDamage * totalDamageMultiplier;
        }

        gun.baseDamage = Mathf.Max(0f, newBaseDamage);

        _applied = true;
    }

    /// <summary>
    /// 恢复枪械原始状态
    /// </summary>
    private void Revert()
    {
        if (!_applied) return;

        foreach (var kv in _savedStates)
        {
            var gun = kv.Key;
            if (gun == null) continue;

            var state = kv.Value;
            gun.shotType = state.shotType;
            gun.pelletsPerShot = state.pelletsPerShot;
            gun.baseDamage = state.baseDamage;
        }

        _savedStates.Clear();
        _applied = false;
    }

    /// <summary>
    /// 只在第一次应用时保存枪械原始状态
    /// </summary>
    private void SaveStateIfNeeded(CameraGunChannel gun)
    {
        if (gun == null) return;
        if (_savedStates.ContainsKey(gun)) return;

        _savedStates.Add(gun, new GunState
        {
            shotType = gun.shotType,
            pelletsPerShot = gun.pelletsPerShot,
            baseDamage = gun.baseDamage
        });
    }
}
