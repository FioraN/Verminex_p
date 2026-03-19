using TMPro;
using PrototypeFPC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Perk selection panel controller.
/// UI only: candidate refresh and availability checks come from PerkSelectionRefresher.
/// </summary>
public sealed class PerkSelectionUI : MonoBehaviour
{
    [Header("Core References")]
    public PerkManager perkManager;
    public PlayerExperience playerExperience;
    public Dependencies fpcDependencies;
    public PerkSelectionRefresher selectionRefresher;

    [Header("Card Setup")]
    [Tooltip("Perk card prefab with a PerkCardUI component. Leave empty to build a default card in code.")]
    public PerkCardUI cardPrefab;

    [Tooltip("Background sprite for code-built cards. Leave empty to use a solid color.")]
    public Sprite cardBackgroundSprite;

    [Header("Card Layout")]
    [Tooltip("Center card stays on screen center. Adjust center offset / upper / lower distance here.")]
    public Vector2 centerCardOffset = Vector2.zero;
    public float upperCardDistance = 220f;
    public float lowerCardDistance = 220f;

    [Header("Gun Select Page")]
    [Tooltip("Optional parent prefab for Gun A / Gun B selection. It must have a PerkGunSelectPageUI component.")]
    public PerkGunSelectPageUI gunSelectPagePrefab;

    [Tooltip("Offset of the Gun select page from screen center, using the same anchored-position units as centerCardOffset.")]
    public Vector2 gunSelectPageOffset = Vector2.zero;

    [Header("Gun Select Display")]
    [Tooltip("Optional always-on display copy controller for the Gun select UI.")]
    public PerkGunSelectDisplayUI gunSelectDisplayUI;

    [Header("Upgrade Points Display")]
    [Tooltip("Anchored position of the copied upgrade-points UI from screen center.")]
    public Vector2 upgradePointsDisplayOffset = Vector2.zero;
    public TMP_FontAsset upgradePointsFont;
    public string upgradePointsPrefix = "Upgrade Points Left: ";
    public float upgradePointsFontSize = 28f;
    public FontStyles upgradePointsFontStyle = FontStyles.Bold;
    public Color upgradePointsTextColor = Color.white;

    [Header("Crosshair Highlight")]
    [Range(1f, 1.5f)] public float hoveredButtonBrightness = 1.18f;
    [Range(1f, 1.25f)] public float hoveredButtonScale = 1.04f;
    public Color gunSelectHoveredOutlineColor = Color.black;
    [Min(0f)] public float gunSelectHoveredOutlineSize = 2f;

    private bool _isOpen;
    private GameObject _pendingPrefab;

    private Canvas _hostCanvas;
    private GraphicRaycaster _raycaster;
    private RectTransform _uiRoot;
    private RectTransform _cardListRoot;
    private RectTransform _gunSelectRoot;
    private RectTransform _upgradePointsTextRoot;
    private RectTransform _defaultGunSelectSpawnPoint;
    private PerkGunSelectPageUI _activeGunSelectPage;
    private PerkGunSelectPageUI _instantiatedCustomGunSelectPage;
    private Button _gunAButton;
    private Button _gunBButton;
    private Button _boundGunAButton;
    private Button _boundGunBButton;
    private Button _boundBackButton;
    private Button _hoveredButton;
    private Button _highlightedButton;
    private Graphic[] _highlightedGraphics;
    private Color[] _highlightedOriginalColors;
    private Vector3 _highlightedOriginalScale = Vector3.one;
    private readonly System.Collections.Generic.List<Outline> _highlightedOutlines = new();
    private EventSystem _eventSystem;
    private TextMeshProUGUI _upgradePointsText;

    private static readonly System.Collections.Generic.List<RaycastResult> RaycastResults = new();

    private readonly System.Collections.Generic.List<PerkCardUI> _spawnedCards = new();

    private void Awake()
    {
        if (playerExperience == null)
            playerExperience = FindFirstObjectByType<PlayerExperience>();

        if (gunSelectDisplayUI == null)
            gunSelectDisplayUI = FindFirstObjectByType<PerkGunSelectDisplayUI>();

        BuildPanel();
        SetOpen(false);
    }

    public void SetHostCanvas(Canvas hostCanvas)
    {
        _hostCanvas = hostCanvas;

        if (_uiRoot == null || _hostCanvas == null)
            return;

        if (_hostCanvas.renderMode == RenderMode.WorldSpace && _hostCanvas.worldCamera == null)
            _hostCanvas.worldCamera = Camera.main;

        _raycaster = _hostCanvas.GetComponent<GraphicRaycaster>();
        if (_raycaster == null)
            _raycaster = _hostCanvas.gameObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        _uiRoot.SetParent(_hostCanvas.transform, false);
        _uiRoot.SetAsLastSibling();
    }

    public void Open()
    {
        if (_isOpen) return;
        if (_hostCanvas == null)
        {
            Debug.LogWarning("[PerkSelectionUI] Host canvas is not assigned.");
            return;
        }

        if (_uiRoot.parent != _hostCanvas.transform)
            SetHostCanvas(_hostCanvas);

        SetOpen(true);
    }

    public void Close()
    {
        if (!_isOpen) return;
        SetOpen(false);
    }

    private void Update()
    {
        if (!_isOpen || _hostCanvas == null || _raycaster == null)
            return;

        EnsureEventSystem();
        RefreshUpgradePointsCopy();
        UpdateCenterScreenInteraction();
    }

    private void SetOpen(bool open)
    {
        _isOpen = open;
        if (_uiRoot != null)
            _uiRoot.gameObject.SetActive(open);

        PerkSceneCanvasUI.IsFireBlocked = open;

        if (open)
        {
            RefreshUpgradePointsCopy();
            ShowCardList(forceRefresh: false);
        }
        else
        {
            if (gunSelectDisplayUI != null)
                gunSelectDisplayUI.ClearPendingPreview();
            SetHoveredButton(null);
        }
    }

    private void ShowCardList()
    {
        ShowCardList(forceRefresh: false);
    }

    private void ShowCardList(bool forceRefresh)
    {
        SetHoveredButton(null);
        if (gunSelectDisplayUI != null)
            gunSelectDisplayUI.ClearPendingPreview();
        _pendingPrefab = null;
        _cardListRoot.gameObject.SetActive(true);
        _gunSelectRoot.gameObject.SetActive(false);
        SpawnCandidateCards(forceRefresh);
    }

    private void ShowGunSelect(GameObject perkPrefab)
    {
        SetHoveredButton(null);
        _pendingPrefab = perkPrefab;
        _cardListRoot.gameObject.SetActive(false);
        _gunSelectRoot.gameObject.SetActive(true);

        RefreshActiveGunSelectPagePreview();
        if (gunSelectDisplayUI != null)
            gunSelectDisplayUI.SetPendingPreview(perkPrefab);
        RefreshGunSelectButtons();
    }

    private void OnGunSelected(int gunIndex)
    {
        if (_pendingPrefab == null || perkManager == null)
        {
            ShowCardList(forceRefresh: false);
            return;
        }

        if (!HasAvailableUpgradePoint())
        {
            Debug.LogWarning("[PerkSelectionUI] Not enough upgrade points to select a perk.");
            RefreshGunSelectButtons();
            return;
        }

        perkManager.RefreshAll(force: true);

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.root == null)
        {
            Debug.LogError($"[PerkSelectionUI] GunRefs.root is null (gunIndex={gunIndex}). Check PerkManager setup.");
            ShowCardList(forceRefresh: false);
            return;
        }

        var inst = perkManager.InstantiatePerkToGun(_pendingPrefab, gunIndex, gunRefs.root.transform);
        if (inst == null)
        {
            Debug.LogWarning($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} failed (prerequisite/conflict/already owned).");
        }
        else
        {
            if (!playerExperience.TrySpendUpgradePoint())
            {
                Debug.LogError("[PerkSelectionUI] Perk granted but failed to spend an upgrade point.");
                return;
            }

            Debug.Log($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} success.");
            ShowCardList(forceRefresh: true);
            return;
        }

        RefreshGunSelectButtons();
    }

    private void RefreshGunSelectButtons()
    {
        bool hasPoint = HasAvailableUpgradePoint();
        bool canEquipGunA = hasPoint && _pendingPrefab != null && perkManager != null && perkManager.CanEquipPerkToGun(_pendingPrefab, 0);
        bool canEquipGunB = hasPoint && _pendingPrefab != null && perkManager != null && perkManager.CanEquipPerkToGun(_pendingPrefab, 1);

        SetButtonSelectableVisual(_gunAButton, canEquipGunA);
        SetButtonSelectableVisual(_gunBButton, canEquipGunB);
    }

    private bool HasAvailableUpgradePoint()
    {
        return playerExperience != null && playerExperience.AvailableUpgradePoints > 0;
    }

    private void ResolveExperienceUI()
    { }

    private void RefreshUpgradePointsCopy()
    {
        if (_upgradePointsTextRoot == null || _upgradePointsText == null)
            return;

        _upgradePointsTextRoot.anchorMin = _upgradePointsTextRoot.anchorMax = _upgradePointsTextRoot.pivot = new Vector2(0.5f, 0.5f);
        _upgradePointsTextRoot.anchoredPosition = upgradePointsDisplayOffset;
        if (upgradePointsFont != null)
            _upgradePointsText.font = upgradePointsFont;
        _upgradePointsText.fontSize = upgradePointsFontSize;
        _upgradePointsText.fontStyle = upgradePointsFontStyle;
        _upgradePointsText.color = upgradePointsTextColor;
        _upgradePointsText.text = $"{upgradePointsPrefix}{Mathf.Max(0, playerExperience != null ? playerExperience.AvailableUpgradePoints : 0)}";
    }

    private void SpawnCandidateCards(bool forceRefresh)
    {
        ClearCards();

        System.Collections.Generic.IReadOnlyList<GameObject> candidates = selectionRefresher != null
            ? selectionRefresher.RefreshCandidates(forceRefresh)
            : System.Array.Empty<GameObject>();

        Vector2 nativeSize = Vector2.zero;
        if (cardPrefab != null)
        {
            var prefabRT = cardPrefab.GetComponent<RectTransform>();
            if (prefabRT != null && prefabRT.sizeDelta.x > 0f && prefabRT.sizeDelta.y > 0f)
                nativeSize = prefabRT.sizeDelta;
        }

        foreach (var perkPrefab in candidates)
        {
            if (perkPrefab == null) continue;

            var capturedPrefab = perkPrefab;
            PerkCardUI card;

            if (cardPrefab != null)
            {
                card = Instantiate(cardPrefab, _cardListRoot);
                if (nativeSize != Vector2.zero)
                {
                    var le = card.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = nativeSize.x;
                    le.preferredHeight = nativeSize.y;
                }
            }
            else
            {
                card = BuildDefaultCard(_cardListRoot, cardBackgroundSprite);
            }

            EnsureCardBackgroundVisible(card);
            card.Populate(capturedPrefab);

            bool selectable = selectionRefresher == null || selectionRefresher.IsPerkSelectableForAnyGun(capturedPrefab);
            card.SetSelectableVisual(selectable);

            if (selectable && card.selectButton != null)
                card.selectButton.onClick.AddListener(() => HandlePerkCardSelected(capturedPrefab));

            _spawnedCards.Add(card);
        }

        PositionSpawnedCards();
    }

    private void PositionSpawnedCards()
    {
        int count = _spawnedCards.Count;
        if (count == 0) return;

        int centerIndex = count / 2;

        for (int i = 0; i < count; i++)
        {
            var card = _spawnedCards[i];
            if (card == null) continue;

            var rt = card.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            float y = centerCardOffset.y;
            if (i < centerIndex)
                y += upperCardDistance * (centerIndex - i);
            else if (i > centerIndex)
                y -= lowerCardDistance * (i - centerIndex);

            rt.anchoredPosition = new Vector2(centerCardOffset.x, y);
        }
    }

    private static void EnsureCardBackgroundVisible(PerkCardUI card)
    {
        if (card == null) return;

        var images = card.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image != null)
                image.enabled = true;
        }
    }

    private static void SetButtonSelectableVisual(Button button, bool selectable)
    {
        if (button == null) return;

        button.interactable = selectable;

        var image = button.GetComponent<Image>();
        if (image == null) return;

        Color baseColor = button.colors.normalColor;
        image.color = selectable ? baseColor : ToGray(baseColor);
    }

    private static Color ToGray(Color color)
    {
        float gray = color.grayscale;
        return new Color(gray, gray, gray, color.a * 0.75f);
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        _spawnedCards.Clear();
    }

    private static PerkCardUI BuildDefaultCard(Transform parent, Sprite bgSprite)
    {
        var root = new GameObject("PerkCard");
        root.transform.SetParent(parent, false);
        root.AddComponent<LayoutElement>().preferredHeight = 180f;

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.35f, 0.40f, 0.48f, 1f);
        bg.sprite = bgSprite;
        if (bgSprite != null) bg.type = Image.Type.Sliced;

        var btn = root.AddComponent<Button>();
        var bc = btn.colors;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        bc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        bc.selectedColor = Color.white;
        btn.colors = bc;

        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.42f, 0.72f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(10f, 2f);
        nameRT.offsetMax = new Vector2(-10f, -2f);

        var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.fontSize = 20;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAlignmentOptions.MidlineRight;

        var descGO = new GameObject("DescText");
        descGO.transform.SetParent(root.transform, false);
        var descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.44f);
        descRT.offsetMin = new Vector2(12f, 6f);
        descRT.offsetMax = new Vector2(-8f, -6f);

        var descTxt = descGO.AddComponent<TextMeshProUGUI>();
        descTxt.fontSize = 13;
        descTxt.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        descTxt.alignment = TextAlignmentOptions.TopLeft;

        var card = root.AddComponent<PerkCardUI>();
        card.perkNameTMP = nameTxt;
        card.descriptionTMP = descTxt;
        card.selectButton = btn;

        return card;
    }

    private void BuildPanel()
    {
        _uiRoot = NewRT("PerkSelectionRoot", transform);
        Stretch(_uiRoot);

        _upgradePointsTextRoot = NewRT("UpgradePointsTextRoot", _uiRoot);
        _upgradePointsTextRoot.anchorMin = _upgradePointsTextRoot.anchorMax = _upgradePointsTextRoot.pivot = new Vector2(0.5f, 0.5f);
        _upgradePointsTextRoot.sizeDelta = new Vector2(460f, 48f);
        _upgradePointsTextRoot.anchoredPosition = upgradePointsDisplayOffset;

        _upgradePointsText = _upgradePointsTextRoot.gameObject.AddComponent<TextMeshProUGUI>();
        _upgradePointsText.raycastTarget = false;
        if (upgradePointsFont != null)
            _upgradePointsText.font = upgradePointsFont;
        _upgradePointsText.fontSize = upgradePointsFontSize;
        _upgradePointsText.fontStyle = upgradePointsFontStyle;
        _upgradePointsText.alignment = TextAlignmentOptions.Center;
        _upgradePointsText.color = upgradePointsTextColor;
        _upgradePointsText.text = $"{upgradePointsPrefix}0";

        _cardListRoot = NewRT("CardListRoot", _uiRoot);
        Stretch(_cardListRoot);

        _gunSelectRoot = NewRT("GunSelectPanel", _uiRoot);
        Stretch(_gunSelectRoot);

        _defaultGunSelectSpawnPoint = NewRT("GunSelectSpawnPoint", _gunSelectRoot);
        Stretch(_defaultGunSelectSpawnPoint);

        BuildConfiguredGunSelectPanel();
    }

    private void BuildConfiguredGunSelectPanel()
    {
        _activeGunSelectPage = null;

        if (TryBuildCustomGunSelectPanel())
            return;

        BuildFallbackGunSelectPanel(_defaultGunSelectSpawnPoint);
    }

    private bool TryBuildCustomGunSelectPanel()
    {
        if (gunSelectPagePrefab == null)
            return false;

        if (!gunSelectPagePrefab.IsComplete())
        {
            Debug.LogWarning("[PerkSelectionUI] Gun select page prefab is missing required button references. Falling back to code-built page.");
            return false;
        }

        _instantiatedCustomGunSelectPage = Instantiate(gunSelectPagePrefab, _defaultGunSelectSpawnPoint);
        _instantiatedCustomGunSelectPage.name = gunSelectPagePrefab.name;

        RectTransform root = _instantiatedCustomGunSelectPage.Root;
        if (root == null)
        {
            Debug.LogWarning("[PerkSelectionUI] Gun select page prefab has no RectTransform root. Falling back to code-built page.");
            Destroy(_instantiatedCustomGunSelectPage.gameObject);
            _instantiatedCustomGunSelectPage = null;
            return false;
        }

        ApplyGunSelectPagePosition(root);

        _activeGunSelectPage = _instantiatedCustomGunSelectPage;
        BindGunSelectButtons(_activeGunSelectPage);
        return true;
    }

    private void BuildFallbackGunSelectPanel(RectTransform parent)
    {
        var col = NewRT("Column", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
        col.anchoredPosition = Vector2.zero;
        col.sizeDelta = new Vector2(520f, 0f);

        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        col.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _gunAButton = SpawnButton(col, "Gun  A", new Color(0.08f, 0.36f, 0.12f, 1f), 82f, 24);
        _gunBButton = SpawnButton(col, "Gun  B", new Color(0.08f, 0.12f, 0.38f, 1f), 82f, 24);
        var btnBack = SpawnButton(col, "Back", new Color(0.22f, 0.08f, 0.08f, 1f), 54f, 16);

        var page = col.gameObject.AddComponent<PerkGunSelectPageUI>();
        page.root = col;
        page.gunAButton = _gunAButton;
        page.gunBButton = _gunBButton;
        page.backButton = btnBack;

        ApplyGunSelectPagePosition(col);

        _activeGunSelectPage = page;
        BindGunSelectButtons(page);
    }

    private void ApplyGunSelectPagePosition(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = gunSelectPageOffset;
    }

    private void BindGunSelectButtons(PerkGunSelectPageUI page)
    {
        if (page == null || !page.IsComplete())
            return;

        RebindGunSelectButtons(page);
    }

    private void RefreshActiveGunSelectPagePreview()
    {
        if (_activeGunSelectPage == null)
            return;

        _activeGunSelectPage.ResetPreviewImages();
        ApplyEquippedGunSelectPagePreviews(_activeGunSelectPage);

        var meta = _pendingPrefab != null ? _pendingPrefab.GetComponent<PerkMeta>() : null;
        _activeGunSelectPage.ApplyPendingPerkPreview(meta);
        RebindGunSelectButtons(_activeGunSelectPage);
    }

    private void ApplyEquippedGunSelectPagePreviews(PerkGunSelectPageUI targetPage)
    {
        if (targetPage == null || perkManager == null)
            return;

        ApplyEquippedGunSelectPagePreviewsForGun(targetPage, 0);
        ApplyEquippedGunSelectPagePreviewsForGun(targetPage, 1);
    }

    private void ApplyEquippedGunSelectPagePreviewsForGun(PerkGunSelectPageUI targetPage, int gunIndex)
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

    private void RebindGunSelectButtons(PerkGunSelectPageUI page)
    {
        if (page == null)
            return;

        UnityAction gunAAction = HandleGunASelected;
        UnityAction gunBAction = HandleGunBSelected;
        UnityAction backAction = HandleBackSelected;

        if (_boundGunAButton != null)
            _boundGunAButton.onClick.RemoveListener(gunAAction);
        if (_boundGunBButton != null)
            _boundGunBButton.onClick.RemoveListener(gunBAction);
        if (_boundBackButton != null)
            _boundBackButton.onClick.RemoveListener(backAction);

        _boundGunAButton = page.CurrentGunAButton;
        _boundGunBButton = page.CurrentGunBButton;
        _boundBackButton = page.backButton;

        if (_boundGunAButton != null)
            _boundGunAButton.onClick.AddListener(gunAAction);
        if (_boundGunBButton != null)
            _boundGunBButton.onClick.AddListener(gunBAction);
        if (_boundBackButton != null)
            _boundBackButton.onClick.AddListener(backAction);

        _gunAButton = _boundGunAButton;
        _gunBButton = _boundGunBButton;
    }

    private void HandleGunASelected()
    {
        PlayButtonClickSound();
        OnGunSelected(0);
    }

    private void HandleGunBSelected()
    {
        PlayButtonClickSound();
        OnGunSelected(1);
    }

    private void HandleBackSelected()
    {
        PlayButtonClickSound();
        ShowCardList(forceRefresh: false);
    }

    private void HandlePerkCardSelected(GameObject perkPrefab)
    {
        PlayButtonClickSound();
        ShowGunSelect(perkPrefab);
    }

    private void PlayButtonClickSound()
    {
        if (PlayerEventAudioPlayer.Instance != null)
            PlayerEventAudioPlayer.Instance.PlayPerkUiClick();
    }

    private static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Button SpawnButton(RectTransform parent, string label, Color bg, float height, int fontSize)
    {
        var rt = NewRT(label, parent);
        rt.gameObject.AddComponent<Image>().color = bg;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        var btn = rt.gameObject.AddComponent<Button>();
        var c = btn.colors;
        c.normalColor = bg;
        c.highlightedColor = Color.Lerp(bg, Color.white, 0.22f);
        c.pressedColor = Color.Lerp(bg, Color.black, 0.28f);
        c.selectedColor = bg;
        btn.colors = c;

        var lblRT = NewRT("Label", rt);
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        var txt = lblRT.gameObject.AddComponent<Text>();
        txt.text = label;
        txt.font = GetFont();
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private static Font _font;

    private static Font GetFont()
    {
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

    private void EnsureEventSystem()
    {
        if (_eventSystem != null)
            return;

        _eventSystem = EventSystem.current;
        if (_eventSystem != null)
            return;

        var go = new GameObject("EventSystem");
        _eventSystem = go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void UpdateCenterScreenInteraction()
    {
        if (_eventSystem == null)
            return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var pointerData = new PointerEventData(_eventSystem)
        {
            position = screenCenter
        };

        RaycastResults.Clear();
        _raycaster.Raycast(pointerData, RaycastResults);

        Button targetButton = null;
        for (int i = 0; i < RaycastResults.Count; i++)
        {
            var go = RaycastResults[i].gameObject;
            if (go == null) continue;

            targetButton = go.GetComponentInParent<Button>();
            if (targetButton != null && targetButton.interactable && targetButton.gameObject.activeInHierarchy)
                break;

            targetButton = null;
        }

        SetHoveredButton(targetButton);

        if (_hoveredButton != null && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            _hoveredButton.onClick.Invoke();
        }
    }

    private void SetHoveredButton(Button button)
    {
        if (_hoveredButton == button)
            return;

        ClearHoveredButtonVisual();
        _hoveredButton = button;

        if (_eventSystem != null)
        {
            _eventSystem.SetSelectedGameObject(_hoveredButton != null ? _hoveredButton.gameObject : null);
        }

        ApplyHoveredButtonVisual(_hoveredButton);
    }

    private void ApplyHoveredButtonVisual(Button button)
    {
        if (button == null)
            return;

        _highlightedButton = button;
        _highlightedGraphics = button.GetComponentsInChildren<Graphic>(true);
        _highlightedOriginalColors = new Color[_highlightedGraphics.Length];

        for (int i = 0; i < _highlightedGraphics.Length; i++)
        {
            var graphic = _highlightedGraphics[i];
            if (graphic == null)
                continue;

            _highlightedOriginalColors[i] = graphic.color;
            graphic.color = Brighten(_highlightedOriginalColors[i], hoveredButtonBrightness);
        }

        _highlightedOriginalScale = button.transform.localScale;
        button.transform.localScale = _highlightedOriginalScale * hoveredButtonScale;

        if (button.transform.IsChildOf(_gunSelectRoot))
        {
            for (int i = 0; i < _highlightedGraphics.Length; i++)
            {
                var graphic = _highlightedGraphics[i];
                if (graphic == null)
                    continue;

                var outline = graphic.gameObject.AddComponent<Outline>();
                outline.effectColor = gunSelectHoveredOutlineColor;
                outline.effectDistance = Vector2.one * gunSelectHoveredOutlineSize;
                outline.useGraphicAlpha = true;
                _highlightedOutlines.Add(outline);
            }
        }
    }

    private void ClearHoveredButtonVisual()
    {
        if (_highlightedButton == null)
            return;

        if (_highlightedGraphics != null && _highlightedOriginalColors != null)
        {
            int count = Mathf.Min(_highlightedGraphics.Length, _highlightedOriginalColors.Length);
            for (int i = 0; i < count; i++)
            {
                var graphic = _highlightedGraphics[i];
                if (graphic != null)
                    graphic.color = _highlightedOriginalColors[i];
            }
        }

        for (int i = 0; i < _highlightedOutlines.Count; i++)
        {
            if (_highlightedOutlines[i] != null)
                Destroy(_highlightedOutlines[i]);
        }

        _highlightedButton.transform.localScale = _highlightedOriginalScale;
        _highlightedButton = null;
        _highlightedGraphics = null;
        _highlightedOriginalColors = null;
        _highlightedOriginalScale = Vector3.one;
        _highlightedOutlines.Clear();
    }

    private static Color Brighten(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }
}
