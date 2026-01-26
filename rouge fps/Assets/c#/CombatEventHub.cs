using System;
using UnityEngine;

/// <summary>
/// 战斗事件总线：把“开火/命中/击杀/换弹/技能键”等事件统一抛出来，Perk 系统只订阅事件即可。
/// </summary>
public static class CombatEventHub
{
    // ====== 数据结构 ======

    public struct FireEvent
    {
        public CameraGunChannel source;   // 哪把枪发射
        public int pellets;               // 弹丸数量（霰弹/双发）
        public bool isProjectile;         // 是否为抛射物模式
        public float time;                // 时间戳
    }

    public struct HitEvent
    {
        public CameraGunChannel source;   // 哪把枪造成
        public GameObject target;         // 受击目标（通常是 MonsterHealth 所在的根）
        public Collider hitCollider;      // 命中碰撞体
        public Vector3 hitPoint;          // 命中点
        public float damage;              // 实际扣血伤害
        public bool isHeadshot;           // 是否爆头（如果有 Hitbox.Head）
        public float time;
    }

    public struct KillEvent
    {
        public CameraGunChannel source;
        public GameObject target;
        public float time;
    }

    public struct ReloadEvent
    {
        public CameraGunChannel source;
        public bool isStart;              // true=start, false=end
        public float time;
    }

    public struct AbilityEvent
    {
        public KeyCode key;               // 目前先用 F
        public float time;
    }

    // ====== 事件 ======
    public static event Action<FireEvent> OnFire;
    public static event Action<HitEvent> OnHit;
    public static event Action<KillEvent> OnKill;
    public static event Action<ReloadEvent> OnReload;
    public static event Action<AbilityEvent> OnAbility;

    // ====== Raise 方法（由武器/子弹/生命系统调用） ======
    public static void RaiseFire(in FireEvent e) => OnFire?.Invoke(e);
    public static void RaiseHit(in HitEvent e) => OnHit?.Invoke(e);
    public static void RaiseKill(in KillEvent e) => OnKill?.Invoke(e);
    public static void RaiseReload(in ReloadEvent e) => OnReload?.Invoke(e);
    public static void RaiseAbility(in AbilityEvent e) => OnAbility?.Invoke(e);
}
