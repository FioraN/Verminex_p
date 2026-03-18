using UnityEngine;

/// <summary>
/// Optional perk component used by PerkGunModelDisplayReplacer to spawn a different
/// 3D model prefab depending on whether the perk is equipped on Gun A or Gun B.
/// </summary>
public sealed class PerkGunModelDisplay : MonoBehaviour
{
    [Tooltip("3D prefab to spawn when this perk is equipped on Gun A.")]
    public GameObject gunAModelPrefab;

    [Tooltip("3D prefab to spawn when this perk is equipped on Gun B.")]
    public GameObject gunBModelPrefab;
}
