using System.Collections.Generic;
using UnityEngine;

public sealed class PerkMeta : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id used for prerequisite checks. If empty, will default to GameObject name.")]
    public string perkId = "";

    [Header("Tier")]
    [Range(1, 2)]
    public int perkTier = 1;

    [Header("Prerequisites")]
    [Tooltip("If empty, no prerequisites.")]
    public List<string> requiredPerkIds = new();

    public string EffectiveId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(perkId)) return perkId;
            return gameObject.name;
        }
    }
}
