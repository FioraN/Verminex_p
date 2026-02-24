using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for perks that modify gun stats through GunStatContext.
/// Attach the perk under the gun object so it can find CameraGunChannel in parents.
/// </summary>
public abstract class GunPerkModifierBase : MonoBehaviour, IGunStatModifier
{
    public virtual int Priority => 0;

    private GunStatContext _ctx;
    protected CameraGunChannel SourceGun { get; private set; }

    protected virtual void OnEnable()
    {
        SourceGun = GetComponentInParent<CameraGunChannel>();

        _ctx = null;
        if (SourceGun != null)
        {
            _ctx = SourceGun.GetComponent<GunStatContext>();
            if (_ctx == null) _ctx = SourceGun.GetComponentInParent<GunStatContext>();
        }

        if (_ctx != null) _ctx.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (_ctx != null) _ctx.Unregister(this);
        _ctx = null;
        SourceGun = null;
    }

    // NOTE: ref removed to match updated IGunStatModifier signature.
    public abstract void ApplyModifiers(CameraGunChannel source, Dictionary<GunStat, StatStack> stacks);
}