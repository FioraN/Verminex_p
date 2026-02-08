using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 开火模式 + 装填模式切换 Perk
/// 
/// 只做两件事：
/// 1. 修改开火模式（单发 / 连发）
/// 2. 修改装填模式（逐发 / 弹匣）
/// 
/// Perk 属于哪把枪，由 PerkManager 的 Perk 列表决定
/// Perk 等级只存在于 PerkMeta 中，本脚本不使用等级
/// </summary>
public sealed class Perk_FireAndReloadMode : MonoBehaviour
{
    [Header("前置条件")]
    [Tooltip("前置条件不满足时是否自动禁用该 Perk")]
    public bool disableIfPrereqMissing = true;

    [Header("开火模式")]
    [Tooltip("是否覆盖开火模式")]
    public bool overrideFireMode = true;

    [Tooltip("目标开火模式（单发 / 连发）")]
    public CameraGunChannel.FireMode fireMode = CameraGunChannel.FireMode.Semi;

    [Header("装填模式")]
    [Tooltip("是否覆盖装填模式")]
    public bool overrideReloadType = true;

    [Tooltip("目标装填模式（逐发 / 弹匣）")]
    public GunAmmo.ReloadType reloadType = GunAmmo.ReloadType.Magazine;

    private PerkManager _perkManager;

    /// <summary>
    /// 保存枪的原始状态，用于移除 Perk 时恢复
    /// </summary>
    private struct GunState
    {
        public CameraGunChannel.FireMode fireMode;
    }

    /// <summary>
    /// 保存弹药系统原始状态
    /// </summary>
    private struct AmmoState
    {
        public GunAmmo.ReloadType reloadType;
    }

    private readonly Dictionary<CameraGunChannel, GunState> _savedGun = new();
    private readonly Dictionary<GunAmmo, AmmoState> _savedAmmo = new();
    private bool _applied;

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null) return;

        // 判断该 Perk 属于哪把枪
        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0)
        {
            // 尚未被加入任何枪的 Perk 列表
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
    /// 根据 PerkManager 的 Perk 列表判断该 Perk 属于哪把枪
    /// </summary>
    private int ResolveGunIndexFromManager()
    {
        if (_perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    /// <summary>
    /// 应用开火 / 装填模式修改
    /// </summary>
    private void Apply(int gunIndex)
    {
        if (_applied) return;

        var gunRefs = _perkManager.GetGun(gunIndex);
        var gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (gun == null) return;

        var ammo = gunRefs.gunAmmo != null ? gunRefs.gunAmmo : gun.ammo;

        _savedGun.Clear();
        _savedAmmo.Clear();

        SaveGunIfNeeded(gun);
        if (ammo != null) SaveAmmoIfNeeded(ammo);

        if (overrideFireMode)
        {
            gun.fireMode = fireMode;
        }

        if (ammo != null && overrideReloadType)
        {
            ammo.reloadType = reloadType;
        }

        _applied = true;
    }

    /// <summary>
    /// 恢复原始状态
    /// </summary>
    private void Revert()
    {
        if (!_applied) return;

        foreach (var kv in _savedGun)
        {
            var gun = kv.Key;
            if (gun == null) continue;

            gun.fireMode = kv.Value.fireMode;
        }

        foreach (var kv in _savedAmmo)
        {
            var ammo = kv.Key;
            if (ammo == null) continue;

            ammo.reloadType = kv.Value.reloadType;
        }

        _savedGun.Clear();
        _savedAmmo.Clear();
        _applied = false;
    }

    /// <summary>
    /// 保存枪的原始开火模式
    /// </summary>
    private void SaveGunIfNeeded(CameraGunChannel gun)
    {
        if (gun == null) return;
        if (_savedGun.ContainsKey(gun)) return;

        _savedGun.Add(gun, new GunState
        {
            fireMode = gun.fireMode
        });
    }

    /// <summary>
    /// 保存弹药系统原始装填模式
    /// </summary>
    private void SaveAmmoIfNeeded(GunAmmo ammo)
    {
        if (ammo == null) return;
        if (_savedAmmo.ContainsKey(ammo)) return;

        _savedAmmo.Add(ammo, new AmmoState
        {
            reloadType = ammo.reloadType
        });
    }
}
