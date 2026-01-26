// MarkStatus.cs
using UnityEngine;

/// <summary>
/// 挂在敌人身上：印记的运行时数据
/// 规则：
/// - 施加枪命中：ApplyOrRefresh（刷新持续时间 + 可选累计计数）
/// - 引爆枪命中：Detonate（结算一次伤害）
/// - 超时未引爆：自动消失（不结算）
/// </summary>
public class MarkStatus : MonoBehaviour
{
    [Header("Runtime Debug")]
    public bool active;
    public int applyHitCount;      // 施加枪累计命中次数（用于放大引爆伤害）
    public float remainingTime;    // 剩余时间（仅用于 Inspector 显示）

    private float _expireTime;
    private MarkConfig _cfg;

    // 最近一次施加印记的枪（可用于归因/调试）
    private CameraGunChannel _lastApplierGun;

    public bool IsActive
    {
        get
        {
            if (!active) return false;
            if (_cfg == null) return false;
            return Time.time < _expireTime;
        }
    }

    public void ApplyOrRefresh(MarkConfig cfg, CameraGunChannel applierGun)
    {
        _cfg = cfg;
        _lastApplierGun = applierGun;

        if (!active)
        {
            active = true;
            applyHitCount = 0;
        }

        // 规则：施加枪命中时可累计计数（用于引爆放大）
        if (cfg != null && cfg.addCountOnApplyHit)
            applyHitCount += 1;

        // 刷新持续时间
        float dur = (cfg != null) ? Mathf.Max(0.01f, cfg.duration) : 0.01f;
        _expireTime = Time.time + dur;
        remainingTime = dur;
    }

    /// <summary>
    /// 引爆：返回本次应造成的引爆伤害（由 Manager 去对目标扣血）
    /// </summary>
    public float ComputeDetonateDamage()
    {
        if (!IsActive) return 0f;
        if (_cfg == null) return 0f;

        float baseDmg = Mathf.Max(0f, _cfg.detonateBaseDamage);
        float bonus = Mathf.Max(0f, _cfg.detonateDamagePerApplyHit) * Mathf.Max(0, applyHitCount);
        return baseDmg + bonus;
    }

    public CameraGunChannel GetLastApplierGun() => _lastApplierGun;

    public void Consume()
    {
        active = false;
        Destroy(this);
    }

    private void Update()
    {
        if (!active) return;

        remainingTime = Mathf.Max(0f, _expireTime - Time.time);

        // 超时未引爆：直接消失（不结算）
        if (Time.time >= _expireTime)
        {
            active = false;
            Destroy(this);
        }
    }
}
