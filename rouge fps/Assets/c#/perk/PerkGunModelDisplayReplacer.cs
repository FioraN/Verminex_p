using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns perk-provided 3D model prefabs on dedicated Gun A / Gun B anchors.
/// This is separate from PerkGunSelectDisplayUI, which only handles the persistent UI copy.
/// </summary>
public sealed class PerkGunModelDisplayReplacer : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Optional source PerkSelectionUI. Used to auto-fill PerkManager.")]
    public PerkSelectionUI perkSelectionUI;

    [Tooltip("Optional explicit PerkManager override.")]
    public PerkManager perkManager;

    [Header("Model Anchors")]
    [Tooltip("World/model-space anchor used to spawn perk model prefabs for Gun A.")]
    public Transform gunAModelAnchor;

    [Tooltip("World/model-space anchor used to spawn perk model prefabs for Gun B.")]
    public Transform gunBModelAnchor;

    [Header("Original Models")]
    [Tooltip("Original Gun A model root to hide while replacement models are active.")]
    public GameObject gunAOriginalModelRoot;

    [Tooltip("Original Gun B model root to hide while replacement models are active.")]
    public GameObject gunBOriginalModelRoot;

    private readonly List<GameObject> _gunAModelInstances = new();
    private readonly List<GameObject> _gunBModelInstances = new();
    private PerkManager _subscribedPerkManager;
    private bool _gunAOriginalWasActive = true;
    private bool _gunBOriginalWasActive = true;

    private void Awake()
    {
        ResolveReferences();
        RebindPerkManagerEvents();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebindPerkManagerEvents();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnbindPerkManagerEvents();
        ClearInstances(_gunAModelInstances);
        ClearInstances(_gunBModelInstances);
        RestoreOriginalModel(gunAOriginalModelRoot, _gunAOriginalWasActive);
        RestoreOriginalModel(gunBOriginalModelRoot, _gunBOriginalWasActive);
    }

    public void RefreshDisplay()
    {
        ResolveReferences();
        RebindPerkManagerEvents();

        ClearInstances(_gunAModelInstances);
        ClearInstances(_gunBModelInstances);

        SpawnModelsForGun(gunAModelAnchor, _gunAModelInstances, 0);
        SpawnModelsForGun(gunBModelAnchor, _gunBModelInstances, 1);

        UpdateOriginalModelVisibility(gunAOriginalModelRoot, _gunAModelInstances, ref _gunAOriginalWasActive);
        UpdateOriginalModelVisibility(gunBOriginalModelRoot, _gunBModelInstances, ref _gunBOriginalWasActive);
    }

    private void ResolveReferences()
    {
        if (perkSelectionUI == null)
            perkSelectionUI = FindFirstObjectByType<PerkSelectionUI>();

        if (perkManager == null && perkSelectionUI != null)
            perkManager = perkSelectionUI.perkManager;

        if (perkManager == null)
            perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void RebindPerkManagerEvents()
    {
        if (_subscribedPerkManager == perkManager)
            return;

        UnbindPerkManagerEvents();
        _subscribedPerkManager = perkManager;

        if (_subscribedPerkManager == null)
            return;

        _subscribedPerkManager.PerksChangedAny += RefreshDisplay;
        _subscribedPerkManager.RefsRefreshed += RefreshDisplay;
    }

    private void UnbindPerkManagerEvents()
    {
        if (_subscribedPerkManager == null)
            return;

        _subscribedPerkManager.PerksChangedAny -= RefreshDisplay;
        _subscribedPerkManager.RefsRefreshed -= RefreshDisplay;
        _subscribedPerkManager = null;
    }

    private void SpawnModelsForGun(Transform anchor, List<GameObject> targetInstances, int gunIndex)
    {
        if (anchor == null || perkManager == null)
            return;

        var perkList = perkManager.GetPerkList(gunIndex);
        if (perkList == null)
            return;

        for (int i = 0; i < perkList.Count; i++)
        {
            MonoBehaviour perk = perkList[i];
            if (perk == null)
                continue;

            var modelDisplay = perk.GetComponent<PerkGunModelDisplay>();
            if (modelDisplay == null)
                continue;

            GameObject prefabToSpawn = gunIndex == 0
                ? modelDisplay.gunAModelPrefab
                : modelDisplay.gunBModelPrefab;

            if (prefabToSpawn == null)
                continue;

            GameObject instance = Instantiate(prefabToSpawn, anchor);
            instance.name = prefabToSpawn.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            BindFirePointOverride(instance, gunIndex);
            targetInstances.Add(instance);
        }
    }

    private void BindFirePointOverride(GameObject instance, int gunIndex)
    {
        if (instance == null || perkManager == null)
            return;

        var firePointOverride = instance.GetComponentInChildren<GunModelFirePointOverride>(true);
        if (firePointOverride == null)
            return;

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.cameraGunChannel == null)
            return;

        firePointOverride.Bind(gunRefs.cameraGunChannel, gunRefs.autoAimLock);
    }

    private static void ClearInstances(List<GameObject> instances)
    {
        if (instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
                Destroy(instances[i]);
        }

        instances.Clear();
    }

    private static void UpdateOriginalModelVisibility(GameObject originalRoot, List<GameObject> spawnedInstances, ref bool originalWasActive)
    {
        if (originalRoot == null)
            return;

        bool hasReplacement = spawnedInstances != null && spawnedInstances.Count > 0;

        if (hasReplacement)
        {
            if (originalRoot.activeSelf)
                originalWasActive = true;
            originalRoot.SetActive(false);
        }
        else
        {
            originalRoot.SetActive(originalWasActive);
        }
    }

    private static void RestoreOriginalModel(GameObject originalRoot, bool originalWasActive)
    {
        if (originalRoot == null)
            return;

        originalRoot.SetActive(originalWasActive);
    }
}
