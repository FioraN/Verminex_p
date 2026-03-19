using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrototypeFPC;
using TMPro;

public sealed class GameStartPauseIntroUI : MonoBehaviour
{
    [SerializeField] private GameObject introUiRoot;
    [SerializeField] private PlayerVitals playerVitals;
    [SerializeField] private Movement playerMovement;
    [SerializeField] private Perspective playerPerspective;
    [SerializeField] private CameraGunDual playerGunDual;
    [SerializeField] private CameraGunChannel[] playerGunChannels;
    [SerializeField] [Min(0.01f)] private float fadeOutDuration = 1f;
    [SerializeField] [Min(0f)] private float destroyDelayAfterFade = 0f;
    [SerializeField] private bool pauseGameAtStart = true;
    [SerializeField] private float pausedTimeScale = 0f;

    private readonly List<ImageState> _imageStates = new List<ImageState>();
    private readonly List<TextState> _textStates = new List<TextState>();
    private readonly Dictionary<Behaviour, bool> _behaviourEnabledStates = new Dictionary<Behaviour, bool>();
    private Coroutine _fadeRoutine;
    private bool _waitingForAnyKey = true;
    private bool _introDismissed;

    private struct ImageState
    {
        public Image image;
        public Color originalColor;
    }

    private struct TextState
    {
        public TMP_Text text;
        public Color originalColor;
    }

    private void Awake()
    {
        ResolvePlayerControlReferences();
        CacheUiImages();
        ApplyStartPause();
    }

    private void Update()
    {
        if (!_waitingForAnyKey || _introDismissed)
            return;

        if (!Input.anyKeyDown)
            return;

        StartGame();
    }

    private void ResolvePlayerControlReferences()
    {
        Transform root = null;

        if (playerVitals == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerVitals = player.GetComponentInParent<PlayerVitals>();
        }

        if (playerVitals != null)
            root = playerVitals.transform.root;
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                root = player.transform.root;
        }

        if (root == null)
            return;

        if (playerMovement == null)
            playerMovement = root.GetComponentInChildren<Movement>(true);

        if (playerPerspective == null)
            playerPerspective = root.GetComponentInChildren<Perspective>(true);

        if (playerGunDual == null)
            playerGunDual = root.GetComponentInChildren<CameraGunDual>(true);

        if (playerGunChannels == null || playerGunChannels.Length == 0)
            playerGunChannels = root.GetComponentsInChildren<CameraGunChannel>(true);
    }

    private void CacheUiImages()
    {
        _imageStates.Clear();
        _textStates.Clear();

        if (introUiRoot == null)
            return;

        Image[] images = introUiRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            _imageStates.Add(new ImageState
            {
                image = image,
                originalColor = image.color
            });
        }

        TMP_Text[] texts = introUiRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            _textStates.Add(new TextState
            {
                text = text,
                originalColor = text.color
            });
        }
    }

    private void ApplyStartPause()
    {
        DisablePlayerControls();

        if (pauseGameAtStart)
            Time.timeScale = pausedTimeScale;
    }

    private void StartGame()
    {
        if (_introDismissed)
            return;

        _introDismissed = true;
        _waitingForAnyKey = false;

        RestorePlayerControls();
        if (pauseGameAtStart)
            Time.timeScale = 1f;

        if (introUiRoot == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeOutAndDestroyUi());
    }

    private void DisablePlayerControls()
    {
        _behaviourEnabledStates.Clear();

        CacheAndDisable(playerMovement);
        CacheAndDisable(playerPerspective);
        CacheAndDisable(playerGunDual);

        if (playerGunChannels == null)
            return;

        for (int i = 0; i < playerGunChannels.Length; i++)
            CacheAndDisable(playerGunChannels[i]);
    }

    private void RestorePlayerControls()
    {
        foreach (var pair in _behaviourEnabledStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        _behaviourEnabledStates.Clear();
    }

    private void CacheAndDisable(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        if (!_behaviourEnabledStates.ContainsKey(behaviour))
            _behaviourEnabledStates.Add(behaviour, behaviour.enabled);

        behaviour.enabled = false;
    }

    private IEnumerator FadeOutAndDestroyUi()
    {
        float duration = Mathf.Max(0.0001f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < _imageStates.Count; i++)
            {
                ImageState state = _imageStates[i];
                if (state.image == null)
                    continue;

                Color color = state.originalColor;
                color.a = Mathf.Lerp(state.originalColor.a, 0f, t);
                state.image.color = color;
            }

            for (int i = 0; i < _textStates.Count; i++)
            {
                TextState state = _textStates[i];
                if (state.text == null)
                    continue;

                Color color = state.originalColor;
                color.a = Mathf.Lerp(state.originalColor.a, 0f, t);
                state.text.color = color;
            }

            yield return null;
        }

        if (destroyDelayAfterFade > 0f)
            yield return new WaitForSecondsRealtime(destroyDelayAfterFade);

        if (introUiRoot != null)
            Destroy(introUiRoot);

        Destroy(gameObject);
    }
}
