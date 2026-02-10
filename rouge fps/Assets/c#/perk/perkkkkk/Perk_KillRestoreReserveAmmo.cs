using System;
using System.Reflection;
using UnityEngine;

public sealed class Perk_KillRestoreReserveAmmo : MonoBehaviour
{
    public enum RewardTarget
    {
        Reserve,
        Magazine
    }

    [Header("Reward")]
    [Min(1)]
    public int ammoToRestore = 5;

    public RewardTarget rewardTarget = RewardTarget.Reserve;

    [Tooltip("If > 0, DOT kills only count if last hit was within this many seconds.")]
    [Min(0f)]
    public float rewardWindowSeconds = 0f;

    [Header("Safety")]
    public bool disableIfNotAllowed = true;
    public bool requirePrerequisites = true;

    private PerkManager _perkManager;
    private CameraGunChannel _boundChannel;
    private GunAmmo _gunAmmo;

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
        _gunAmmo = gun != null ? gun.gunAmmo : null;

        if (_boundChannel == null || _gunAmmo == null)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        CombatEventHub.OnKill += HandleKill;
    }

    private void OnDisable()
    {
        CombatEventHub.OnKill -= HandleKill;
    }

    private int ResolveGunIndexFromManager()
    {
        if (_perkManager == null) return -1;

        if (_perkManager.selectedPerksGunA != null && _perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB != null && _perkManager.selectedPerksGunB.Contains(this)) return 1;

        return -1;
    }

    private void HandleKill(CombatEventHub.KillEvent e)
    {
        if (!isActiveAndEnabled) return;
        if (ammoToRestore <= 0) return;
        if (e.target == null) return;

        CameraGunChannel rewardSource = e.source;

        MonsterHealth mh = e.target.GetComponent<MonsterHealth>();
        if (mh == null) mh = e.target.GetComponentInParent<MonsterHealth>();

        if (rewardSource == null && mh != null)
        {
            if (rewardWindowSeconds > 0f)
            {
                if (Time.time - mh.LastHitTime > rewardWindowSeconds)
                    return;
            }

            rewardSource = mh.LastHitSource;
        }

        if (rewardSource == null) return;

        // This perk instance only rewards its bound gun
        if (rewardSource != _boundChannel) return;

        if (rewardTarget == RewardTarget.Reserve)
        {
            TryAddReserveAmmo(_gunAmmo, ammoToRestore);
        }
        else
        {
            TryAddMagazineAmmo(_gunAmmo, ammoToRestore);
        }
    }

    // -----------------------------
    // Reserve
    // -----------------------------
    private static void TryAddReserveAmmo(GunAmmo ammo, int amount)
    {
        if (ammo == null || amount <= 0) return;

        // Prefer methods
        if (TryInvokeIntMethod(ammo, "AddReserveAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "AddReserve", amount)) return;
        if (TryInvokeIntMethod(ammo, "GiveReserveAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "GiveAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "AddAmmo", amount)) return;

        // Fallback fields
        if (TryAddToIntFieldWithMax(ammo, "reserveAmmo", "maxReserveAmmo", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "ammoReserve", "maxAmmoReserve", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "reserve", "maxReserve", amount)) return;
    }

    // -----------------------------
    // Magazine
    // -----------------------------
    private static void TryAddMagazineAmmo(GunAmmo ammo, int amount)
    {
        if (ammo == null || amount <= 0) return;

        // Prefer methods
        if (TryInvokeIntMethod(ammo, "AddMagazineAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "AddMagAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "AddClipAmmo", amount)) return;
        if (TryInvokeIntMethod(ammo, "AddToMag", amount)) return;

        // Some projects use "Refill" style functions
        if (TryInvokeIntMethod(ammo, "RefillMagazine", amount)) return;
        if (TryInvokeIntMethod(ammo, "RefillMag", amount)) return;

        // Fallback fields (current-in-mag)
        // Common pairs:
        // - ammoInMag / magSize
        // - bulletsInMag / maxBulletsInMag
        // - currentAmmo / maxAmmo
        // - clip / clipSize
        if (TryAddToIntFieldWithMax(ammo, "ammoInMag", "magSize", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "bulletsInMag", "maxBulletsInMag", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "currentAmmo", "maxAmmo", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "clip", "clipSize", amount)) return;
        if (TryAddToIntFieldWithMax(ammo, "magAmmo", "maxMagAmmo", amount)) return;
    }

    // -----------------------------
    // Reflection helpers
    // -----------------------------
    private static bool TryInvokeIntMethod(object obj, string methodName, int value)
    {
        MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi == null) return false;

        ParameterInfo[] ps = mi.GetParameters();
        if (ps.Length != 1) return false;
        if (ps[0].ParameterType != typeof(int)) return false;

        mi.Invoke(obj, new object[] { value });
        return true;
    }

    private static bool TryAddToIntFieldWithMax(object obj, string fieldName, string maxFieldName, int add)
    {
        Type t = obj.GetType();

        FieldInfo fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi == null || fi.FieldType != typeof(int)) return false;

        int cur = (int)fi.GetValue(obj);
        int next = cur + add;

        FieldInfo maxFi = t.GetField(maxFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (maxFi != null && maxFi.FieldType == typeof(int))
        {
            int max = (int)maxFi.GetValue(obj);
            next = Mathf.Min(next, max);
        }

        fi.SetValue(obj, next);
        return true;
    }
}
