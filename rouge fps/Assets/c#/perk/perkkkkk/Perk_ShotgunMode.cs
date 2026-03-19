using System.Collections.Generic;
using UnityEngine;

public sealed class Perk_ShotgunMode : GunPerkModifierBase
{
    [Header("Shotgun")]
    [Min(1)] public int pelletsPerShot = 6;

    [Tooltip("If true, keep total damage roughly constant by dividing by pellets.")]
    public bool keepTotalDamageConstant = true;

    [Tooltip("Multiplier applied to total damage (or per-pellet if keepTotalDamageConstant is false).")]
    [Min(0f)] public float totalDamageMultiplier = 1f;

    [Header("Gun Stats Override")]
    [Tooltip("Whether to override the weapon's final damage while this perk is active.")]
    public bool overrideDamage = false;

    [Min(0f)] public float damage = 10f;

    [Tooltip("Whether to override the weapon's final max range while this perk is active.")]
    public bool overrideMaxRange = false;

    [Min(0.01f)] public float maxRange = 40f;

    [Tooltip("Whether to override the weapon's final bullet speed while this perk is active.")]
    public bool overrideBulletSpeed = false;

    [Min(0.01f)] public float bulletSpeed = 80f;

    [Header("Recoil Override")]
    [Tooltip("Whether to override recoil while this perk is active.")]
    public bool overrideRecoil = false;

    [Min(0f)] public float kickPitchPerShot = 1.2f;
    [Min(0f)] public float kickYawRandom = 0.6f;

    [Header("Priority")]
    public int priority = 0;
    public override int Priority => priority;

    private CameraGunChannel.ShotType _prevShotType;
    private int _prevPellets;
    private GunRecoil _recoil;
    private float _prevKickPitchPerShot;
    private float _prevKickYawRandom;
    private bool _applied;

    private void OnEnable()
    {
        base.OnEnable();

        if (SourceGun == null) return;

        if (!_applied)
        {
            _prevShotType = SourceGun.shotType;
            _prevPellets = SourceGun.pelletsPerShot;
            _recoil = SourceGun.recoil;
            if (_recoil == null)
                _recoil = SourceGun.GetComponent<GunRecoil>() ?? SourceGun.GetComponentInParent<GunRecoil>();

            if (_recoil != null)
            {
                _prevKickPitchPerShot = _recoil.kickPitchPerShot;
                _prevKickYawRandom = _recoil.kickYawRandom;
            }

            SourceGun.shotType = CameraGunChannel.ShotType.Shotgun;
            SourceGun.pelletsPerShot = Mathf.Max(1, pelletsPerShot);

            if (overrideRecoil && _recoil != null)
            {
                _recoil.kickPitchPerShot = Mathf.Max(0f, kickPitchPerShot);
                _recoil.kickYawRandom = Mathf.Max(0f, kickYawRandom);
            }

            _applied = true;
        }
    }

    private void OnDisable()
    {
        if (_applied && SourceGun != null)
        {
            SourceGun.shotType = _prevShotType;
            SourceGun.pelletsPerShot = _prevPellets;

            if (_recoil != null)
            {
                _recoil.kickPitchPerShot = _prevKickPitchPerShot;
                _recoil.kickYawRandom = _prevKickYawRandom;
            }

            _recoil = null;
            _applied = false;
        }

        base.OnDisable();
    }

    public override void ApplyModifiers(CameraGunChannel source, Dictionary<GunStat, StatStack> stacks)
    {
        if (source == null || stacks == null) return;
        var ctx = source.GetComponent<GunStatContext>() ?? source.GetComponentInParent<GunStatContext>();

        if (stacks.TryGetValue(GunStat.Damage, out var dmg))
        {
            int pellets = Mathf.Max(1, pelletsPerShot);
            float mult;

            if (keepTotalDamageConstant)
                mult = (pellets > 0) ? (totalDamageMultiplier / pellets) : 0f;
            else
                mult = totalDamageMultiplier;

            dmg.mul *= Mathf.Max(0f, mult);

            if (overrideDamage)
            {
                float baseDamage = ctx != null ? Mathf.Max(0f, ctx.baseDamage) : Mathf.Max(0f, source.baseDamage);
                OverrideStatToValue(ref dmg, baseDamage, damage);
            }

            stacks[GunStat.Damage] = dmg;
        }

        if (overrideMaxRange && stacks.TryGetValue(GunStat.MaxRange, out var rangeStack))
        {
            float baseRange = ctx != null ? Mathf.Max(0.01f, ctx.baseMaxRange) : Mathf.Max(0.01f, source.maxRange);
            OverrideStatToValue(ref rangeStack, baseRange, maxRange);
            stacks[GunStat.MaxRange] = rangeStack;
        }

        if (overrideBulletSpeed && stacks.TryGetValue(GunStat.BulletSpeed, out var speedStack))
        {
            float baseSpeed = ctx != null ? Mathf.Max(0.01f, ctx.baseBulletSpeed) : Mathf.Max(0.01f, source.bulletSpeed);
            OverrideStatToValue(ref speedStack, baseSpeed, bulletSpeed);
            stacks[GunStat.BulletSpeed] = speedStack;
        }
    }

    private static void OverrideStatToValue(ref StatStack stack, float baseValue, float targetValue)
    {
        float safeBase = Mathf.Max(0.0001f, baseValue);
        float safeTarget = Mathf.Max(0.0001f, targetValue);

        stack.flat = safeTarget - safeBase;
        stack.addPct = 0f;
        stack.mul = 1f;
        stack.postMul = 1f;
    }
}
