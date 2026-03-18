using UnityEngine;

/// <summary>
/// Put this on a spawned gun model prefab. When bound to a gun channel, enabling this model
/// overrides that gun's firePoint with the local override point; disabling restores the previous one.
/// </summary>
public sealed class GunModelFirePointOverride : MonoBehaviour
{
    [Tooltip("Override fire point. If empty, this object's transform is used.")]
    public Transform overrideFirePoint;

    [Tooltip("Also redirect AutoAimLockOn.firePoint when available.")]
    public bool applyToAutoAimLock = true;

    private CameraGunChannel _targetGun;
    private AutoAimLockOn _targetAutoAimLock;
    private Transform _previousFirePoint;
    private Transform _previousAutoAimFirePoint;
    private bool _isApplied;

    public void Bind(CameraGunChannel targetGun, AutoAimLockOn targetAutoAimLock)
    {
        if (_isApplied)
            Restore();

        _targetGun = targetGun;
        _targetAutoAimLock = targetAutoAimLock;

        if (isActiveAndEnabled)
            Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Apply()
    {
        if (_isApplied || _targetGun == null)
            return;

        Transform newFirePoint = overrideFirePoint != null ? overrideFirePoint : transform;
        _previousFirePoint = _targetGun.firePoint;
        _targetGun.firePoint = newFirePoint;

        if (applyToAutoAimLock && _targetAutoAimLock != null)
        {
            _previousAutoAimFirePoint = _targetAutoAimLock.firePoint;
            _targetAutoAimLock.firePoint = newFirePoint;
        }

        _isApplied = true;
    }

    private void Restore()
    {
        if (!_isApplied)
            return;

        if (_targetGun != null)
            _targetGun.firePoint = _previousFirePoint;

        if (applyToAutoAimLock && _targetAutoAimLock != null)
            _targetAutoAimLock.firePoint = _previousAutoAimFirePoint;

        _isApplied = false;
    }
}
