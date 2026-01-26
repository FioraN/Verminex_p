// MarkConfig.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Mark Config", fileName = "MarkConfig")]
public class MarkConfig : ScriptableObject
{
    [Header("Which gun applies / detonates")]
    [Tooltip("哪把枪的命中会施加/刷新印记（Role）。")]
    public CameraGunChannel.Role applierRole = CameraGunChannel.Role.Secondary;

    [Tooltip("哪把枪的命中会引爆印记（Role）。")]
    public CameraGunChannel.Role detonatorRole = CameraGunChannel.Role.Primary;

    [Header("Mark Duration")]
    [Tooltip("印记持续时间（秒）。超过时间未引爆则消失。")]
    [Min(0.01f)] public float duration = 6f;

    [Header("Apply Behavior")]
    [Tooltip("施加枪每次命中时是否增加印记计数（用于放大引爆伤害）。")]
    public bool addCountOnApplyHit = true;

    [Header("Detonate Damage")]
    [Tooltip("引爆时的基础伤害。")]
    [Min(0f)] public float detonateBaseDamage = 10f;

    [Tooltip("施加枪累计的命中计数，每 1 次会让引爆伤害额外增加多少。")]
    [Min(0f)] public float detonateDamagePerApplyHit = 5f;

    [Tooltip("引爆伤害是否不触发 OnHit（避免无限连锁/触发命中类perk）。建议保持 true。")]
    public bool detonateSkipHitEvent = true;

    [Tooltip("引爆后是否消耗（移除）印记。一般为 true。")]
    public bool consumeOnDetonate = true;

    [Header("Optional Extensions (later perks)")]
    [Tooltip("当引爆枪命中一个带印记的目标时：把该目标身上的异常状态共享给其他带印记单位。")]
    public bool shareStatusesOnDetonateHit = false;

    [Tooltip("击杀带印记目标时：让印记跳到另一个随机敌人（留作后续perk用）。")]
    public bool jumpMarkOnKill = false;

    [Tooltip("跳印记时，最多扫描多少个敌人（防止场景敌人太多卡顿）。")]
    [Min(1)] public int jumpSearchLimit = 64;

    [Header("Debug")]
    [Tooltip("是否在 Inspector 里显示更详细的调试信息。")]
    public bool debug = false;
}
