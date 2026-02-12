using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Perk：自动瞄准（开启枪上的 AutoAimLockOn）
/// - 启用 Perk：把 AutoAimLockOn.active 设为 true
/// - 关闭/移除 Perk：恢复原始 active 值
/// </summary>
public sealed class Perk_EnableAutoAim : MonoBehaviour
{
    [Header("前置条件")]
    [Tooltip("如果前置不满足则自动禁用 Perk")]
    public bool disableIfPrereqMissing = true;

    [Tooltip("是否在枪的子物体中查找 AutoAimLockOn")]
    public bool searchInChildren = true;

    private PerkManager _pm;
    private CameraGunChannel _gun;

    // 可能有多个 AutoAimLockOn（保险起见全部打开，关闭时逐个恢复）
    private readonly List<AutoAimLockOn> _targets = new();
    private readonly List<bool> _originalActives = new();

    private void OnEnable()
    {
        _pm ??= FindFirstObjectByType<PerkManager>();
        if (_pm == null) { enabled = false; return; }

        // 判断这个 Perk 实例属于哪把枪（必须是实例在 selectedPerks 列表里）
        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0) { enabled = false; return; }

        // 前置检查
        if (disableIfPrereqMissing && !_pm.PrerequisitesMet(gameObject, gunIndex))
        {
            enabled = false;
            return;
        }

        // 获取这把枪的 CameraGunChannel
        var gunRefs = _pm.GetGun(gunIndex);
        _gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (_gun == null) { enabled = false; return; }

        // 找到并开启 AutoAimLockOn
        CacheAndEnableAutoAim();
    }

    private void OnDisable()
    {
        // 关闭/移除 Perk 时恢复原始状态
        RestoreAutoAim();

        _gun = null;
    }

    /// <summary>
    /// 判断当前 Perk 属于 GunA 还是 GunB
    /// 0 = GunA, 1 = GunB
    /// </summary>
    private int ResolveGunIndexFromManager()
    {
        // 注意：必须通过 PerkManager.InstantiatePerkToGun() 实例化到枪上
        // 直接把 prefab 拖进 selectedPerks 列表不会按预期工作
        if (_pm.selectedPerksGunA.Contains(this)) return 0;
        if (_pm.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    /// <summary>
    /// 缓存原始 active，并把所有 AutoAimLockOn.active 打开
    /// </summary>
    private void CacheAndEnableAutoAim()
    {
        _targets.Clear();
        _originalActives.Clear();

        if (_gun == null) return;

        AutoAimLockOn[] aims = searchInChildren
            ? _gun.GetComponentsInChildren<AutoAimLockOn>(true)
            : _gun.GetComponents<AutoAimLockOn>();

        if (aims == null || aims.Length == 0) return;

        for (int i = 0; i < aims.Length; i++)
        {
            var a = aims[i];
            if (a == null) continue;

            _targets.Add(a);
            _originalActives.Add(a.active);

            // 开启自瞄
            a.active = true;
        }
    }

    /// <summary>
    /// 恢复所有 AutoAimLockOn 的原始 active 值
    /// </summary>
    private void RestoreAutoAim()
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            var a = _targets[i];
            if (a == null) continue;

            bool original = (i < _originalActives.Count) ? _originalActives[i] : false;
            a.active = original;
        }

        _targets.Clear();
        _originalActives.Clear();
    }
}
