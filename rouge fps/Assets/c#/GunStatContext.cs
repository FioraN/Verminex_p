using System;
using System.Collections.Generic;
using UnityEngine;

public enum GunStat
{
    Damage,
    FireRate,
    BulletSpeed,
    MaxRange
}

[Serializable]
public struct StatStack
{
    public float flat;
    public float addPct;   // additive percent, e.g. +0.2 = +20%
    public float mul;      // multiplicative, e.g. x1.5

    public void Reset()
    {
        flat = 0f;
        addPct = 0f;
        mul = 1f;
    }

    public float Evaluate(float baseValue)
    {
        return (baseValue + flat) * (1f + addPct) * mul;
    }
}

public interface IGunStatModifier
{
    /// <summary> Lower runs first, higher runs later. </summary>
    int Priority { get; }

    /// <summary> Contribute to stacks. Do not read or write CameraGunChannel fields here. </summary>
    void ApplyModifiers(CameraGunChannel source, Dictionary<GunStat, StatStack> stacks);
}

/// <summary>
/// Per-gun stat aggregator. Attach one to each gun (same GameObject as CameraGunChannel recommended).
/// </summary>
public sealed class GunStatContext : MonoBehaviour
{
    [Header("Auto capture base stats from CameraGunChannel on Awake")]
    public bool captureBaseFromGunOnAwake = true;

    [Header("Base values (captured or manual)")]
    public float baseDamage = 10f;
    public float baseFireRate = 5f;
    public float baseBulletSpeed = 50f;
    public float baseMaxRange = 40f;

    private CameraGunChannel _gun;
    private readonly List<IGunStatModifier> _mods = new();
    private readonly Dictionary<GunStat, StatStack> _stacks = new();
    private bool _dirty = true;

    private void Awake()
    {
        _gun = GetComponent<CameraGunChannel>();
        if (_gun == null) _gun = GetComponentInParent<CameraGunChannel>();

        if (captureBaseFromGunOnAwake && _gun != null)
        {
            baseDamage = _gun.baseDamage;
            baseFireRate = _gun.fireRate;
            baseBulletSpeed = _gun.bulletSpeed;
            baseMaxRange = _gun.maxRange;
        }
    }

    public void Register(IGunStatModifier mod)
    {
        if (mod == null) return;
        if (_mods.Contains(mod)) return;
        _mods.Add(mod);
        _dirty = true;
    }

    public void Unregister(IGunStatModifier mod)
    {
        if (mod == null) return;
        if (_mods.Remove(mod)) _dirty = true;
    }

    private void RebuildIfDirty()
    {
        if (!_dirty) return;

        _stacks.Clear();
        foreach (GunStat s in Enum.GetValues(typeof(GunStat)))
        {
            var st = new StatStack();
            st.Reset();
            _stacks[s] = st;
        }

        _mods.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        for (int i = 0; i < _mods.Count; i++)
        {
            _mods[i].ApplyModifiers(_gun, _stacks);
        }

        _dirty = false;
    }

    public float GetDamage()
    {
        RebuildIfDirty();
        return _stacks[GunStat.Damage].Evaluate(baseDamage);
    }

    public float GetFireRate()
    {
        RebuildIfDirty();
        return _stacks[GunStat.FireRate].Evaluate(baseFireRate);
    }

    public float GetBulletSpeed()
    {
        RebuildIfDirty();
        return _stacks[GunStat.BulletSpeed].Evaluate(baseBulletSpeed);
    }

    public float GetMaxRange()
    {
        RebuildIfDirty();
        return _stacks[GunStat.MaxRange].Evaluate(baseMaxRange);
    }

    public void MarkDirty() => _dirty = true;
}