using UnityEngine;

/// <summary>
/// 敌人护甲：类似哈迪斯护甲条
/// 规则（与你确认一致）：
/// 1) 护甲存在时：伤害先打护甲，溢出可扣血（由 MonsterHealth 处理）
/// 2) 爆头仍然有效（伤害先算完再拆分）
/// 3) 敌人护甲模式可选：永久 / 再生 / 破甲窗口
/// </summary>
public class EnemyArmor : MonoBehaviour
{
    public enum ArmorMode
    {
        Permanent,      // 打光后永久无护甲
        Regen,          // 打光后延迟再生
        BreakWindow     // 打光后进入易伤窗口，窗口结束后恢复护甲
    }

    [Header("Armor")]
    [Min(0f)] public float maxArmor = 50f;
    [Min(0f)] public float armor = 50f;

    [Header("Mode")]
    public ArmorMode mode = ArmorMode.Permanent;

    [Header("Regen")]
    [Tooltip("护甲为0后，延迟多少秒开始再生。")]
    [Min(0f)] public float regenDelay = 2f;

    [Tooltip("再生速度：每秒恢复多少护甲。")]
    [Min(0f)] public float regenRate = 10f;

    [Header("Break Window")]
    [Tooltip("护甲被打光后，易伤窗口持续时间。")]
    [Min(0f)] public float vulnerableDuration = 3f;

    [Tooltip("易伤窗口结束后恢复护甲量（可设置为 maxArmor 表示回满）。")]
    [Min(0f)] public float restoreArmorAmount = 50f;

    [Header("Debug")]
    public bool isArmored;
    public float remainingVulnerableTime;

    private float _nextRegenStartTime;
    private float _vulnerableEndTime;
    private bool _inVulnerable;

    public bool HasArmor => armor > 0.0001f;

    public bool InVulnerableWindow => _inVulnerable && Time.time < _vulnerableEndTime;

    private void Awake()
    {
        armor = Mathf.Clamp(armor, 0f, Mathf.Max(0f, maxArmor));
        UpdateDebug();
    }

    private void Update()
    {
        if (mode == ArmorMode.Regen)
        {
            if (!HasArmor)
            {
                if (Time.time >= _nextRegenStartTime)
                {
                    armor = Mathf.Min(maxArmor, armor + regenRate * Time.deltaTime);
                }
            }
        }
        else if (mode == ArmorMode.BreakWindow)
        {
            if (_inVulnerable)
            {
                remainingVulnerableTime = Mathf.Max(0f, _vulnerableEndTime - Time.time);
                if (Time.time >= _vulnerableEndTime)
                {
                    _inVulnerable = false;
                    remainingVulnerableTime = 0f;

                    float restore = Mathf.Clamp(restoreArmorAmount, 0f, maxArmor);
                    armor = Mathf.Max(armor, restore);
                }
            }
        }

        UpdateDebug();
    }

    /// <summary>
    /// 扣护甲：返回实际扣掉的护甲值
    /// </summary>
    public float DamageArmor(float amount)
    {
        if (amount <= 0f) return 0f;
        if (!HasArmor) return 0f;

        float before = armor;
        armor = Mathf.Max(0f, armor - amount);
        float taken = before - armor;

        if (before > 0f && armor <= 0f)
        {
            OnArmorBroken();
        }

        UpdateDebug();
        return taken;
    }

    private void OnArmorBroken()
    {
        if (mode == ArmorMode.Regen)
        {
            _nextRegenStartTime = Time.time + regenDelay;
        }
        else if (mode == ArmorMode.BreakWindow)
        {
            _inVulnerable = true;
            _vulnerableEndTime = Time.time + vulnerableDuration;
            remainingVulnerableTime = Mathf.Max(0f, _vulnerableEndTime - Time.time);
        }
    }

    private void UpdateDebug()
    {
        isArmored = HasArmor;
        if (!_inVulnerable) remainingVulnerableTime = 0f;
    }
}
