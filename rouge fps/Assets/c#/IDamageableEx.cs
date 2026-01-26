using UnityEngine;

public interface IDamageableEx : IDamageable
{
    void TakeDamage(DamageInfo info);
}

[System.Flags]
public enum DamageFlags
{
    None = 0,

    /// <summary>
    /// 不触发 OnHit（用于 DoT 等持续伤害），但死亡仍可触发 OnKill
    /// </summary>
    SkipHitEvent = 1 << 0
}

public struct DamageInfo
{
    public CameraGunChannel source;
    public float damage;
    public bool isHeadshot;
    public Vector3 hitPoint;
    public Collider hitCollider;

    /// <summary>
    /// 伤害标记。默认 None：会触发 OnHit/OnKill（你现有子弹命中就是这种）。
    /// DoT 等持续伤害可设置 SkipHitEvent。
    /// </summary>
    public DamageFlags flags;
}
