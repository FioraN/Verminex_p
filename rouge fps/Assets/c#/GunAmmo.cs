using System.Collections;
using UnityEngine;

public class GunAmmo : MonoBehaviour
{
    public enum ReloadType { Magazine, PerBullet }

    [Header("Ammo")]
    [Min(1)] public int magazineSize = 30;
    [Min(0)] public int ammoInMag = 30;
    [Min(0)] public int ammoReserve = 120;

    [Header("Reload Type")]
    public ReloadType reloadType = ReloadType.Magazine;

    [Header("Reload Times (Magazine)")]
    [Min(0f)] public float reloadTimeMagazine = 1.8f;

    [Header("Reload Times (PerBullet)")]
    [Min(0f)] public float reloadStartTime = 0.35f;
    [Min(0f)] public float insertOneTime = 0.45f;
    [Min(0f)] public float reloadEndTime = 0.25f;

    public bool IsReloading => _isReloading;
    public bool IsUninterruptible => _isUninterruptible; // magazine phase

    private bool _isReloading;
    private bool _isUninterruptible;

    public System.Action<int, int> OnAmmoChanged;
    public System.Action OnReloadStart;
    public System.Action OnReloadEnd;

    private void Awake()
    {
        NotifyAmmo();
    }

    public bool HasAmmoInMag() => ammoInMag > 0;

    public bool TryConsumeOne()
    {
        if (ammoInMag <= 0) return false;
        ammoInMag--;
        NotifyAmmo();
        return true;
    }

    public bool NeedsReload()
    {
        // 有备弹 + 弹匣没满 => 需要换弹（战术换弹/补弹）
        if (ammoReserve <= 0) return false;
        return ammoInMag < magazineSize;
    }


    // --- External reload control (used by CameraGunDual) ---

    public void BeginExternalReload(bool uninterruptible)
    {
        _isReloading = true;
        _isUninterruptible = uninterruptible;
        OnReloadStart?.Invoke();
    }

    public void EndExternalReload()
    {
        _isUninterruptible = false;
        _isReloading = false;
        OnReloadEnd?.Invoke();
    }

    public void CancelReloadIfPerBullet()
    {
        // Only meaningful for external per-bullet flow; dual coroutine will stop itself.
        // Keep as a semantic API for channel.
        // No-op here; the dual checks input and stops the coroutine.
    }

    public void ApplyMagazineReloadNow()
    {
        int needed = magazineSize - ammoInMag;
        if (needed <= 0) return;
        int take = Mathf.Min(needed, ammoReserve);
        ammoInMag += take;
        ammoReserve -= take;
        NotifyAmmo();
    }

    public bool CanInsertOne()
    {
        return ammoReserve > 0 && ammoInMag < magazineSize;
    }

    public void InsertOneNow()
    {
        if (!CanInsertOne()) return;
        ammoInMag += 1;
        ammoReserve -= 1;
        NotifyAmmo();
    }

    private void NotifyAmmo()
    {
        OnAmmoChanged?.Invoke(ammoInMag, ammoReserve);
    }
}
