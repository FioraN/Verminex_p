using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Gun select page parent prefab and wire its buttons in the Inspector.
/// </summary>
public sealed class PerkGunSelectPageUI : MonoBehaviour
{
    private sealed class PreviewSlotState
    {
        public RectTransform root;
        public bool rootWasActive;
        public GameObject activeInstance;
    }

    [Header("Root")]
    [Tooltip("Optional explicit root. If empty, this component's RectTransform will be used.")]
    public RectTransform root;

    [Header("Buttons")]
    public Button gunAButton;
    public Button gunBButton;
    public Button backButton;

    [Header("Preview Roots (Optional)")]
    [Tooltip("If empty, Gun A preview falls back to gunAButton's RectTransform.")]
    public RectTransform gunAVisualRoot;

    [Tooltip("If empty, Gun B preview falls back to gunBButton's RectTransform.")]
    public RectTransform gunBVisualRoot;

    public RectTransform gripVisualRoot;
    public RectTransform stockVisualRoot;
    public RectTransform scopeVisualRoot;

    private readonly System.Collections.Generic.Dictionary<RectTransform, PreviewSlotState> _slotStates = new();
    private Button _currentGunAButton;
    private Button _currentGunBButton;

    public RectTransform Root
    {
        get
        {
            if (root != null)
                return root;

            return transform as RectTransform;
        }
    }

    public bool IsComplete()
    {
        return Root != null && gunAButton != null && gunBButton != null && backButton != null;
    }

    public Button CurrentGunAButton => _currentGunAButton != null ? _currentGunAButton : gunAButton;
    public Button CurrentGunBButton => _currentGunBButton != null ? _currentGunBButton : gunBButton;

    private void Awake()
    {
        _currentGunAButton = gunAButton;
        _currentGunBButton = gunBButton;
    }

    public void ResetPreviewImages()
    {
        _currentGunAButton = gunAButton;
        _currentGunBButton = gunBButton;

        foreach (var pair in _slotStates)
        {
            var state = pair.Value;
            if (state == null)
                continue;

            if (state.activeInstance != null)
                Destroy(state.activeInstance);

            if (state.root != null)
                state.root.gameObject.SetActive(state.rootWasActive);
        }
    }

    public void ApplyEquippedPerkPreview(PerkMeta meta, int gunIndex)
    {
        if (meta == null || meta.changeGunUiPrefab == null)
            return;

        if (meta.changeGunImageTarget == PerkGunImageChangeTarget.None)
            return;

        RectTransform targetRoot = GetTargetRoot(meta.changeGunImageTarget, gunIndex);
        if (targetRoot == null)
            return;

        ApplyPrefabOverride(targetRoot, meta.changeGunUiPrefab);
    }

    public void ApplyPendingPerkPreview(PerkMeta meta)
    {
        if (meta == null || meta.changeGunUiPrefab == null)
            return;

        if (meta.changeGunImageTarget == PerkGunImageChangeTarget.None)
            return;

        if (meta.changeGunImageTarget == PerkGunImageChangeTarget.Gun)
            return;

        RectTransform targetRoot = GetTargetRoot(meta.changeGunImageTarget, gunIndex: -1);
        if (targetRoot == null)
            return;

        ApplyPrefabOverride(targetRoot, meta.changeGunUiPrefab);
    }

    private void ApplyPrefabOverride(RectTransform targetRoot, GameObject prefab)
    {
        if (targetRoot == null || prefab == null)
            return;

        if (!_slotStates.TryGetValue(targetRoot, out PreviewSlotState state) || state == null)
        {
            state = new PreviewSlotState
            {
                root = targetRoot,
                rootWasActive = targetRoot.gameObject.activeSelf
            };

            _slotStates[targetRoot] = state;
        }

        if (state.activeInstance != null)
            Destroy(state.activeInstance);

        state.rootWasActive = targetRoot.gameObject.activeSelf;
        targetRoot.gameObject.SetActive(false);

        Transform parent = targetRoot.parent != null ? targetRoot.parent : targetRoot;
        state.activeInstance = Instantiate(prefab, parent);
        state.activeInstance.name = prefab.name;

        var rt = state.activeInstance.transform as RectTransform;
        if (rt != null)
        {
            CopyRectTransform(targetRoot, rt);
            rt.SetSiblingIndex(targetRoot.GetSiblingIndex() + 1);
        }

        Button replacementButton = state.activeInstance.GetComponentInChildren<Button>(true);
        if (replacementButton != null)
        {
            if (targetRoot == GetTargetRoot(PerkGunImageChangeTarget.Gun, 0))
                _currentGunAButton = replacementButton;
            else if (targetRoot == GetTargetRoot(PerkGunImageChangeTarget.Gun, 1))
                _currentGunBButton = replacementButton;
        }
    }

    private RectTransform GetTargetRoot(PerkGunImageChangeTarget target, int gunIndex)
    {
        switch (target)
        {
            case PerkGunImageChangeTarget.Gun:
                if (gunIndex == 0)
                    return gunAVisualRoot != null ? gunAVisualRoot : (gunAButton != null ? gunAButton.transform as RectTransform : null);

                if (gunIndex == 1)
                    return gunBVisualRoot != null ? gunBVisualRoot : (gunBButton != null ? gunBButton.transform as RectTransform : null);

                return null;
            case PerkGunImageChangeTarget.Grip:
                return gripVisualRoot;
            case PerkGunImageChangeTarget.Stock:
                return stockVisualRoot;
            case PerkGunImageChangeTarget.Scope:
                return scopeVisualRoot;
            default:
                return null;
        }
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
    }
}
