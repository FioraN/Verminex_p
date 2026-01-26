using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoDualUI : MonoBehaviour
{
    [Header("Refs")]
    public CameraGunDual dual;
    public CameraGunChannel primaryChannel;
    public CameraGunChannel secondaryChannel;

    [Header("Primary UI")]
    public TMP_Text primaryTMP;
    public Text primaryText;

    [Header("Secondary UI")]
    public TMP_Text secondaryTMP;
    public Text secondaryText;

    [Header("Format")]
    [Tooltip("Use {0}=mag, {1}=reserve")]
    public string format = "{0} / {1}";

    [Header("Update")]
    public bool updateEveryFrame = true;

    private GunAmmo _pAmmo;
    private GunAmmo _sAmmo;

    private void Awake()
    {
        TryAutoWire();
        CacheAmmoRefs();
        RefreshAll();
    }

    private void OnEnable()
    {
        TryAutoWire();
        CacheAmmoRefs();
        HookEvents(true);
        RefreshAll();
    }

    private void OnDisable()
    {
        HookEvents(false);
    }

    private void Update()
    {
        if (!updateEveryFrame) return;
        RefreshAll();
    }

    private void TryAutoWire()
    {
        DualGunResolver.TryResolve(ref dual, ref primaryChannel, ref secondaryChannel);
    }

    private void CacheAmmoRefs()
    {
        _pAmmo = (primaryChannel != null) ? primaryChannel.ammo : null;
        _sAmmo = (secondaryChannel != null) ? secondaryChannel.ammo : null;
    }

    private void HookEvents(bool hook)
    {
        if (_pAmmo != null)
        {
            if (hook) _pAmmo.OnAmmoChanged += OnPrimaryAmmoChanged;
            else _pAmmo.OnAmmoChanged -= OnPrimaryAmmoChanged;
        }

        if (_sAmmo != null)
        {
            if (hook) _sAmmo.OnAmmoChanged += OnSecondaryAmmoChanged;
            else _sAmmo.OnAmmoChanged -= OnSecondaryAmmoChanged;
        }
    }

    private void OnPrimaryAmmoChanged(int mag, int reserve) => SetPrimaryText(mag, reserve);
    private void OnSecondaryAmmoChanged(int mag, int reserve) => SetSecondaryText(mag, reserve);

    private void RefreshAll()
    {
        TryAutoWire();

        var oldP = _pAmmo;
        var oldS = _sAmmo;

        CacheAmmoRefs();

        if (oldP != _pAmmo || oldS != _sAmmo)
        {
            if (oldP != null) oldP.OnAmmoChanged -= OnPrimaryAmmoChanged;
            if (oldS != null) oldS.OnAmmoChanged -= OnSecondaryAmmoChanged;

            if (_pAmmo != null) _pAmmo.OnAmmoChanged += OnPrimaryAmmoChanged;
            if (_sAmmo != null) _sAmmo.OnAmmoChanged += OnSecondaryAmmoChanged;
        }

        if (_pAmmo != null) SetPrimaryText(_pAmmo.ammoInMag, _pAmmo.ammoReserve);
        else SetPrimaryText(-1, -1);

        if (_sAmmo != null) SetSecondaryText(_sAmmo.ammoInMag, _sAmmo.ammoReserve);
        else SetSecondaryText(-1, -1);
    }

    private void SetPrimaryText(int mag, int reserve)
    {
        string s = (mag < 0) ? "-- / --" : string.Format(format, mag, reserve);
        if (primaryTMP != null) primaryTMP.text = s;
        if (primaryText != null) primaryText.text = s;
    }

    private void SetSecondaryText(int mag, int reserve)
    {
        string s = (mag < 0) ? "-- / --" : string.Format(format, mag, reserve);
        if (secondaryTMP != null) secondaryTMP.text = s;
        if (secondaryText != null) secondaryText.text = s;
    }
}
