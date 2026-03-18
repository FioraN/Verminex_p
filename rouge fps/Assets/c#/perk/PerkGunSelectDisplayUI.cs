using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates an always-visible, non-interactive copy of the Gun select UI on a target screen canvas.
/// </summary>
public sealed class PerkGunSelectDisplayUI : MonoBehaviour
{
    private sealed class OverlayInstanceState
    {
        public RectTransform anchorRoot;
        public RectTransform instanceRoot;
        public Vector2 baseAnchoredPosition;
        public Vector3 baseLocalScale;
        public Quaternion baseLocalRotation;
        public bool anchorWasActive;
        public readonly System.Collections.Generic.List<Graphic> hiddenGraphics = new();
        public readonly System.Collections.Generic.List<bool> hiddenGraphicStates = new();
    }

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

    [Header("Perk Widget Display")]
    [Tooltip("Optional anchor used to show extra perk UI prefabs for Gun A.")]
    public RectTransform gunAPerkWidgetAnchor;

    [Tooltip("Optional anchor used to show extra perk UI prefabs for Gun B.")]
    public RectTransform gunBPerkWidgetAnchor;

    [Tooltip("Extra anchored-position offset applied per spawned widget index. Leave zero if you only show one widget.")]
    public Vector2 perkWidgetSpacing = Vector2.zero;

    private GameObject _pendingPrefab;
    private PerkGunSelectPageUI _displayPageInstance;
    private PerkGunSelectPageUI _currentSourcePrefab;
    private Vector3 _baseLocalScale = Vector3.one;
    private Quaternion _baseLocalRotation = Quaternion.identity;
    private readonly System.Collections.Generic.List<OverlayInstanceState> _gunAWidgetInstances = new();
    private readonly System.Collections.Generic.List<OverlayInstanceState> _gunBWidgetInstances = new();

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

        ApplyOverlayTransforms(_gunAWidgetInstances);
        ApplyOverlayTransforms(_gunBWidgetInstances);
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
        RefreshPerkWidgets();

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

    private void RefreshPerkWidgets()
    {
        ClearPerkWidgets(_gunAWidgetInstances);
        ClearPerkWidgets(_gunBWidgetInstances);

        SpawnPerkWidgetsForGun(gunAPerkWidgetAnchor, _gunAWidgetInstances, 0);
        SpawnPerkWidgetsForGun(gunBPerkWidgetAnchor, _gunBWidgetInstances, 1);
    }

    private void SpawnPerkWidgetsForGun(RectTransform anchor, System.Collections.Generic.List<OverlayInstanceState> targetStates, int gunIndex)
    {
        if (anchor == null || perkManager == null)
            return;

        var perkList = perkManager.GetPerkList(gunIndex);
        if (perkList == null)
            return;

        int widgetIndex = 0;
        for (int i = 0; i < perkList.Count; i++)
        {
            MonoBehaviour perk = perkList[i];
            if (perk == null)
                continue;

            var widget = perk.GetComponent<PerkGunDisplayWidget>();
            if (widget == null || widget.uiPrefab == null)
                continue;

            OverlayInstanceState state = new OverlayInstanceState
            {
                anchorRoot = anchor,
                anchorWasActive = anchor.gameObject.activeSelf
            };

            if (targetStates.Count == 0)
                HideAnchorGraphics(anchor, state);

            GameObject instance = Instantiate(widget.uiPrefab, anchor);
            instance.name = widget.uiPrefab.name;

            RectTransform instanceRoot = instance.transform as RectTransform;
            if (instanceRoot != null)
                instanceRoot.anchoredPosition += perkWidgetSpacing * widgetIndex;

            MakeWidgetNonInteractive(instance);

            state.instanceRoot = instanceRoot;
            state.baseAnchoredPosition = instanceRoot != null ? instanceRoot.anchoredPosition : Vector2.zero;
            state.baseLocalScale = instanceRoot != null ? instanceRoot.localScale : Vector3.one;
            state.baseLocalRotation = instanceRoot != null ? instanceRoot.localRotation : Quaternion.identity;
            targetStates.Add(state);

            widgetIndex++;
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

    private static void ApplyOverlayTransforms(System.Collections.Generic.List<OverlayInstanceState> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            OverlayInstanceState state = states[i];
            if (state == null || state.instanceRoot == null)
                continue;

            state.instanceRoot.localScale = state.baseLocalScale;
            state.instanceRoot.localRotation = state.baseLocalRotation;
            state.instanceRoot.anchoredPosition = state.baseAnchoredPosition;
        }
    }

    private static void ClearPerkWidgets(System.Collections.Generic.List<OverlayInstanceState> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            OverlayInstanceState state = states[i];
            if (state == null)
                continue;

            if (state.instanceRoot != null)
                Destroy(state.instanceRoot.gameObject);

            RestoreAnchorGraphics(state);
        }

        states.Clear();
    }

    private static void HideAnchorGraphics(RectTransform anchor, OverlayInstanceState state)
    {
        if (anchor == null || state == null)
            return;

        var graphics = anchor.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            state.hiddenGraphics.Add(graphic);
            state.hiddenGraphicStates.Add(graphic.enabled);
            graphic.enabled = false;
        }
    }

    private static void RestoreAnchorGraphics(OverlayInstanceState state)
    {
        if (state == null)
            return;

        for (int i = 0; i < state.hiddenGraphics.Count; i++)
        {
            Graphic graphic = state.hiddenGraphics[i];
            if (graphic == null)
                continue;

            bool wasEnabled = i < state.hiddenGraphicStates.Count && state.hiddenGraphicStates[i];
            graphic.enabled = wasEnabled;
        }

        if (state.anchorRoot != null)
            state.anchorRoot.gameObject.SetActive(state.anchorWasActive);
    }

    private static void MakeWidgetNonInteractive(GameObject widgetRoot)
    {
        if (widgetRoot == null)
            return;

        var buttons = widgetRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            buttons[i].transition = Selectable.Transition.None;
            buttons[i].enabled = false;
        }

        var selectables = widgetRoot.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            if (selectables[i] != null)
                selectables[i].interactable = false;
        }

        var graphics = widgetRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
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
