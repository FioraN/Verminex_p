using UnityEngine;

/// <summary>
/// 子弹对护甲的额外效果（由 BulletArmorPayload 提供）
/// </summary>
public struct ArmorHitInfo
{
    // 对护甲伤害倍率（只影响“打护甲”的那部分伤害）
    public float armorDamageMultiplier;

    // 穿甲：直接打到血的比例（0~1）
    public float piercePercent;

    // 穿甲：额外固定直接扣血
    public float pierceFlat;

    // 破甲：命中护甲时额外减少护甲（固定值）
    public float shatterFlat;

    // 破甲：命中护甲时额外减少护甲（按“打护甲伤害”比例）
    public float shatterPercentOfArmorDamage;

    public static ArmorHitInfo Default => new ArmorHitInfo
    {
        armorDamageMultiplier = 1f,
        piercePercent = 0f,
        pierceFlat = 0f,
        shatterFlat = 0f,
        shatterPercentOfArmorDamage = 0f
    };
}
