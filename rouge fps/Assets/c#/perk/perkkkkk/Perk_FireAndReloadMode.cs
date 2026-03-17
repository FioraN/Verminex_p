using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Switches fire mode / reload mode and can optionally override the gun's base magazine size.
/// The owning gun is resolved from PerkManager's selected perk lists.
/// </summary>
public sealed class Perk_FireAndReloadMode : MonoBehaviour
{
    [Header("Prerequisite")]
    [Tooltip("Disable this perk automatically when prerequisites are not met.")]
    public bool disableIfPrereqMissing = true;

    [Header("Fire Mode")]
    [Tooltip("Whether to override the gun fire mode.")]
    public bool overrideFireMode = true;

    [Tooltip("Target fire mode.")]
    public CameraGunChannel.FireMode fireMode = CameraGunChannel.FireMode.Semi;

    [Header("Reload Type")]
    [Tooltip("Whether to override the reload type.")]
    public bool overrideReloadType = true;

    [Tooltip("Target reload type.")]
    public GunAmmo.ReloadType reloadType = GunAmmo.ReloadType.Magazine;

    [Header("Base Magazine")]
    [Tooltip("Whether to override GunStatContext.baseMagazineSize. Falls back to GunAmmo.magazineSize when no GunStatContext exists.")]
    public bool overrideBaseMagazineSize = false;

    [Tooltip("Target base magazine size.")]
    [Min(1)] public int baseMagazineSize = 12;

    private PerkManager _perkManager;

    private struct GunState
    {
        public CameraGunChannel.FireMode fireMode;
    }

    private struct AmmoState
    {
        public GunAmmo.ReloadType reloadType;
        public int magazineSize;
    }

    private struct StatContextState
    {
        public int baseMagazineSize;
    }

    private readonly Dictionary<CameraGunChannel, GunState> _savedGun = new();
    private readonly Dictionary<GunAmmo, AmmoState> _savedAmmo = new();
    private readonly Dictionary<GunStatContext, StatContextState> _savedCtx = new();
    private bool _applied;

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null)
            return;

        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0)
        {
            enabled = false;
            return;
        }

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

    private int ResolveGunIndexFromManager()
    {
        if (_perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB.Contains(this)) return 1;
        return -1;
    }

    private void Apply(int gunIndex)
    {
        if (_applied)
            return;

        var gunRefs = _perkManager.GetGun(gunIndex);
        var gun = gunRefs != null ? gunRefs.cameraGunChannel : null;
        if (gun == null)
            return;

        var ammo = gunRefs.gunAmmo != null ? gunRefs.gunAmmo : gun.ammo;
        var ctx = gun.GetComponent<GunStatContext>() ?? gun.GetComponentInParent<GunStatContext>();

        _savedGun.Clear();
        _savedAmmo.Clear();
        _savedCtx.Clear();

        SaveGunIfNeeded(gun);
        if (ammo != null)
            SaveAmmoIfNeeded(ammo);
        if (ctx != null)
            SaveCtxIfNeeded(ctx);

        if (overrideFireMode)
            gun.fireMode = fireMode;

        if (ammo != null && overrideReloadType)
            ammo.reloadType = reloadType;

        if (overrideBaseMagazineSize)
        {
            int targetMagazineSize = Mathf.Max(1, baseMagazineSize);

            if (ctx != null)
            {
                ctx.baseMagazineSize = targetMagazineSize;
                ctx.ForceRebuildNow();
            }
            else if (ammo != null)
            {
                ammo.magazineSize = targetMagazineSize;
                ammo.ammoInMag = Mathf.Min(ammo.ammoInMag, ammo.magazineSize);
                ammo.OnAmmoChanged?.Invoke(ammo.ammoInMag, ammo.ammoReserve);
            }
        }

        _applied = true;
    }

    private void Revert()
    {
        if (!_applied)
            return;

        foreach (var kv in _savedGun)
        {
            var gun = kv.Key;
            if (gun == null)
                continue;

            gun.fireMode = kv.Value.fireMode;
        }

        foreach (var kv in _savedAmmo)
        {
            var ammo = kv.Key;
            if (ammo == null)
                continue;

            ammo.reloadType = kv.Value.reloadType;
            ammo.magazineSize = Mathf.Max(1, kv.Value.magazineSize);
            ammo.ammoInMag = Mathf.Min(ammo.ammoInMag, ammo.magazineSize);
            ammo.OnAmmoChanged?.Invoke(ammo.ammoInMag, ammo.ammoReserve);
        }

        foreach (var kv in _savedCtx)
        {
            var ctx = kv.Key;
            if (ctx == null)
                continue;

            ctx.baseMagazineSize = Mathf.Max(1, kv.Value.baseMagazineSize);
            ctx.ForceRebuildNow();
        }

        _savedGun.Clear();
        _savedAmmo.Clear();
        _savedCtx.Clear();
        _applied = false;
    }

    private void SaveGunIfNeeded(CameraGunChannel gun)
    {
        if (gun == null || _savedGun.ContainsKey(gun))
            return;

        _savedGun.Add(gun, new GunState
        {
            fireMode = gun.fireMode
        });
    }

    private void SaveAmmoIfNeeded(GunAmmo ammo)
    {
        if (ammo == null || _savedAmmo.ContainsKey(ammo))
            return;

        _savedAmmo.Add(ammo, new AmmoState
        {
            reloadType = ammo.reloadType,
            magazineSize = Mathf.Max(1, ammo.magazineSize)
        });
    }

    private void SaveCtxIfNeeded(GunStatContext ctx)
    {
        if (ctx == null || _savedCtx.ContainsKey(ctx))
            return;

        _savedCtx.Add(ctx, new StatContextState
        {
            baseMagazineSize = Mathf.Max(1, ctx.baseMagazineSize)
        });
    }
}
