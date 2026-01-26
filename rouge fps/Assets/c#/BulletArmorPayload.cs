using UnityEngine;

/// <summary>
/// 挂在子弹 prefab 上：配置“对护甲额外效果”
/// - 只有目标处于护甲状态（EnemyArmor.armor > 0）时才会生效
/// </summary>
public class BulletArmorPayload : MonoBehaviour
{
    [Header("Armor Bonus")]
    [Tooltip("对护甲的伤害倍率（只增强打护甲的那部分）。1=不变，2=双倍。")]
    [Min(0f)] public float armorDamageMultiplier = 1f;

    [Header("Pierce")]
    [Tooltip("穿甲比例：最终伤害的这一部分会直接扣血（0~1）。")]
    [Range(0f, 1f)] public float piercePercent = 0f;

    [Tooltip("额外固定穿甲：直接扣血的固定值。")]
    [Min(0f)] public float pierceFlat = 0f;

    [Header("Shatter (Extra Armor Break)")]
    [Tooltip("命中护甲时额外削减护甲（固定值）。")]
    [Min(0f)] public float shatterFlat = 0f;

    [Tooltip("命中护甲时额外削减护甲（按打护甲伤害的比例）。例如 0.2 表示额外削减 20% 的护甲伤害值。")]
    [Min(0f)] public float shatterPercentOfArmorDamage = 0f;

    public ArmorHitInfo ToArmorHitInfo()
    {
        return new ArmorHitInfo
        {
            armorDamageMultiplier = Mathf.Max(0f, armorDamageMultiplier),
            piercePercent = Mathf.Clamp01(piercePercent),
            pierceFlat = Mathf.Max(0f, pierceFlat),
            shatterFlat = Mathf.Max(0f, shatterFlat),
            shatterPercentOfArmorDamage = Mathf.Max(0f, shatterPercentOfArmorDamage)
        };
    }
}
