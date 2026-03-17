using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates an always-visible, non-interactive copy of the Gun select UI on a target screen canvas.
/// </summary>
public sealed class PerkGunSelectDisplayUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Optional source PerkSelectionUI. Used to auto-fill PerkManager and Gun select page prefab.")]
    public PerkSelectionUI perkSelectionUI;

    [Tooltip("Optional explicit PerkManager override.")]
    public PerkManager perkManager;

    [Tooltip("Optional explicit Gun select page prefab override. If empty, uses PerkSelectionUI.gunSelectPagePrefab.")]
    public PerkGunSelectPageUI gunSelectPagePrefab;

    [Header("Display Target")]
    [Tooltip("Screen canvas where the non-interactive display copy will be spawned.")]
    public Canvas targetCanvas;

    [Tooltip("Anchored position offset from screen center.")]
    public Vector2 displayOffset = Vector2.zero;

    [Min(0.01f)]
    [Tooltip("Multiplier applied on top of the prefab root scale.")]
    public float displayScaleMultiplier = 1f;

    [Tooltip("Z rotation in degrees applied to the display copy root.")]
    public float displayRotationZ = 0f;

    private GameObject _pendingPrefab;
    private PerkGunSelectPageUI _displayPageInstance;
    private PerkGunSelectPageUI _currentSourcePrefab;
    private Vector3 _baseLocalScale = Vector3.one;
    private Quaternion _baseLocalRotation = Quaternion.identity;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private void LateUpdate()
    {
        if (_displayPageInstance != null)
            ApplyDisplayTransform(_displayPageInstance.Root);
    }

    public void SetPendingPreview(GameObject perkPrefab)
    {
        _pendingPrefab = perkPrefab;
        RefreshDisplay();
    }

    public void ClearPendingPreview()
    {
        _pendingPrefab = null;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        ResolveReferences();

        if (!EnsureDisplayCopy())
            return;

        _displayPageInstance.ResetPreviewImages();
        ApplyEquippedPreviews(_displayPageInstance);

        var meta = _pendingPrefab != null ? _pendingPrefab.GetComponent<PerkMeta>() : null;
        _displayPageInstance.ApplyPendingPerkPreview(meta);

        _displayPageInstance.gameObject.SetActive(true);
        ApplyDisplayTransform(_displayPageInstance.Root);
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

    private bool EnsureDisplayCopy()
    {
        var sourcePrefab = GetSourcePrefab();
        if (sourcePrefab == null || targetCanvas == null)
            return false;

        bool parentChanged = _displayPageInstance != null && _displayPageInstance.transform.parent != targetCanvas.transform;
        bool sourceChanged = _currentSourcePrefab != sourcePrefab;

        if (_displayPageInstance == null || parentChanged || sourceChanged)
        {
            if (_displayPageInstance != null)
                Destroy(_displayPageInstance.gameObject);

            _displayPageInstance = Instantiate(sourcePrefab, targetCanvas.transform);
            _displayPageInstance.name = sourcePrefab.name + "_PersistentDisplayCopy";
            _currentSourcePrefab = sourcePrefab;

            if (_displayPageInstance.Root != null)
            {
                _baseLocalScale = _displayPageInstance.Root.localScale;
                _baseLocalRotation = _displayPageInstance.Root.localRotation;
            }

            MakeDisplayCopyNonInteractive(_displayPageInstance);
        }

        return _displayPageInstance != null;
    }

    private PerkGunSelectPageUI GetSourcePrefab()
    {
        if (gunSelectPagePrefab != null)
            return gunSelectPagePrefab;

        return perkSelectionUI != null ? perkSelectionUI.gunSelectPagePrefab : null;
    }

    private void ApplyEquippedPreviews(PerkGunSelectPageUI targetPage)
    {
        if (targetPage == null || perkManager == null)
            return;

        ApplyEquippedPreviewsForGun(targetPage, 0);
        ApplyEquippedPreviewsForGun(targetPage, 1);
    }

    private void ApplyEquippedPreviewsForGun(PerkGunSelectPageUI targetPage, int gunIndex)
    {
        var list = perkManager.GetPerkList(gunIndex);
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            var perk = list[i];
            if (perk == null)
                continue;

            var meta = perk.GetComponent<PerkMeta>();
            targetPage.ApplyEquippedPerkPreview(meta, gunIndex);
        }
    }

    private void ApplyDisplayTransform(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = displayOffset;
        rt.localScale = _baseLocalScale * displayScaleMultiplier;
        rt.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, displayRotationZ);
    }

    private static void MakeDisplayCopyNonInteractive(PerkGunSelectPageUI page)
    {
        if (page == null)
            return;

        if (page.backButton != null)
            page.backButton.gameObject.SetActive(false);

        var buttons = page.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            buttons[i].transition = Selectable.Transition.None;
            buttons[i].enabled = false;
        }

        var selectables = page.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            if (selectables[i] != null)
                selectables[i].interactable = false;
        }

        var graphics = page.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }
}
