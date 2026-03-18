using UnityEngine;

/// <summary>
/// Optional perk component used by PerkGunSelectDisplayUI to show an extra UI widget
/// on the corresponding gun's persistent display area.
/// </summary>
public sealed class PerkGunDisplayWidget : MonoBehaviour
{
    [Tooltip("UI prefab to spawn on the corresponding gun display slot.")]
    public GameObject uiPrefab;
}
