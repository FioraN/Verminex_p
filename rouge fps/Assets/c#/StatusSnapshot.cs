using UnityEngine;

/// <summary>
/// 用于“共享异常状态”的快照：只保存类型、层数、剩余时间、以及必要参数。
/// 共享策略：应用到目标时，等价于“重新施加同类型异常（叠层+刷新计时）”。
/// </summary>
public struct StatusSnapshot
{
    public StatusType type;
    public int stacks;
    public float remaining;

    // Burn
    public float tickInterval;
    public float burnDamagePerTickPerStack;

    // Slow
    public float slowPerStack;

    // Poison(Weaken)
    public float weakenPerStack;

    // Shock-A
    public float shockChainDamagePerStack;
    public float shockChainRadius;
    public int shockMaxChains;

    public CameraGunChannel source;
}
