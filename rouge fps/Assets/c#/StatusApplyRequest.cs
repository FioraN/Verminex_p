using UnityEngine;

public struct StatusApplyRequest
{
    public StatusType type;

    /// <summary>本次增加层数（无上限）。</summary>
    public int stacksToAdd;

    /// <summary>持续时间（秒）。再次施加会重新计时，并叠层。</summary>
    public float duration;

    /// <summary>来源枪（主/副枪），用于后续按枪区分的 perk。</summary>
    public CameraGunChannel source;

    // ===== Burn（燃烧：DoT） =====
    /// <summary>DoT tick 间隔（秒）。<=0 则使用容器默认值。</summary>
    public float tickInterval;

    /// <summary>每次 tick 每层造成的伤害（只对 Burn 生效）。</summary>
    public float burnDamagePerTickPerStack;

    // ===== Slow（减速：移速倍率） =====
    /// <summary>每层减少的移速比例（0.05 = 每层-5%）。只对 Slow 生效。</summary>
    public float slowPerStack;

    // ===== Poison（虚弱：降低敌人输出） =====
    /// <summary>每层降低的“敌人造成伤害”比例（0.06 = 每层-6%）。只对 Poison 生效。</summary>
    public float weakenPerStack;

    // ===== Shock（株连：伤害连锁） =====
    /// <summary>Shock-A：连锁时对每个被连锁敌人造成的额外伤害（每层）。</summary>
    public float shockChainDamagePerStack;

    /// <summary>Shock-A：连锁半径（米）。</summary>
    public float shockChainRadius;

    /// <summary>Shock-A：每次触发最多连锁到多少个敌人。</summary>
    public int shockMaxChains;
}
