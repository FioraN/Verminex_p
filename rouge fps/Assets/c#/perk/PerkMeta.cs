using System.Collections.Generic;
using UnityEngine;

public sealed class PerkMeta : MonoBehaviour
{
    [Header("身份信息")]

    [Tooltip("唯一ID，用于前置与互斥检测。如果为空，默认使用GameObject名称。")]
    public string perkId = "";

    [Header("阶级")]
    [Range(1, 2)]
    public int perkTier = 1;

    [Header("前置条件")]
    [Tooltip("需要已拥有的Perk ID列表。如果为空，则无前置条件。")]
    public List<string> requiredPerkIds = new();

    [Header("互斥条件")]
    [Tooltip("与这些Perk ID互斥，若玩家已拥有其中任意一个，则无法获取本Perk。")]
    public List<string> mutuallyExclusivePerkIds = new();

    [Header("文本信息")]
    [Tooltip("风味文字（用于展示氛围或世界观描述）。")]
    [TextArea(2, 4)]
    public string flavorText = "";

    [Tooltip("功能描述文字（用于UI展示效果说明）。")]
    [TextArea(2, 6)]
    public string description = "";

    /// <summary>
    /// 获取最终生效ID。
    /// 若perkId为空，则默认使用GameObject名称。
    /// </summary>
    public string EffectiveId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(perkId))
                return perkId;

            return gameObject.name;
        }
    }
}