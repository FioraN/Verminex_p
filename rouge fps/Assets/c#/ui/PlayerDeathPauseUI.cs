using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrototypeFPC;
using TMPro;

public sealed class PlayerDeathPauseUI : MonoBehaviour
{
    [SerializeField] private PlayerVitals playerVitals;
    [SerializeField] private GameObject deathUiRoot;
    [SerializeField] [Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField] private Movement playerMovement;
    [SerializeField] private Perspective playerPerspective;
    [SerializeField] private CameraGunDual playerGunDual;
    [SerializeField] private CameraGunChannel[] playerGunChannels;
    [SerializeField] private PlayerEventAudioPlayer playerEventAudioPlayer;

    private readonly List<ImageFadeState> _imageStates = new List<ImageFadeState>();
    private readonly List<TextFadeState> _textStates = new List<TextFadeState>();
    private Coroutine _fadeRoutine;
    private bool _deathHandled;

    private struct ImageFadeState
    {
        public Image image;
        public Color targetColor;
        public float targetAlpha;
    }

    private struct TextFadeState
    {
        public TMP_Text text;
        public Color targetColor;
        public float targetAlpha;
    }

    private void Awake()
    {
        if (playerVitals == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerVitals = player.GetComponentInParent<PlayerVitals>();
        }

        ResolvePlayerControlReferences();
    }

    private void OnEnable()
    {
        if (playerVitals != null)
            playerVitals.OnDied += HandlePlayerDied;

        if (playerVitals != null && playerVitals.IsDead)
            HandlePlayerDied();
    }

    private void OnDisable()
    {
        if (playerVitals != null)
            playerVitals.OnDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;

        if (deathUiRoot == null)
            return;

        deathUiRoot.SetActive(true);
        PrepareImagesForFade();
        DisablePlayerControls();

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeImagesIn());
    }

    private void PrepareImagesForFade()
    {
        _imageStates.Clear();
        _textStates.Clear();

        Image[] images = deathUiRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            Color targetColor = image.color;
            _imageStates.Add(new ImageFadeState
            {
                image = image,
                targetColor = targetColor,
                targetAlpha = 1f
            });

            targetColor.a = 0f;
            image.color = targetColor;
        }

        TMP_Text[] texts = deathUiRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            Color targetColor = text.color;
            _textStates.Add(new TextFadeState
            {
                text = text,
                targetColor = targetColor,
                targetAlpha = 1f
            });

            targetColor.a = 0f;
            text.color = targetColor;
        }
    }

    private IEnumerator FadeImagesIn()
    {
        if (_imageStates.Count == 0)
            yield break;

        float duration = Mathf.Max(0.0001f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < _imageStates.Count; i++)
            {
                ImageFadeState state = _imageStates[i];
                if (state.image == null)
                    continue;

                Color color = state.targetColor;
                color.a = Mathf.Lerp(0f, state.targetAlpha, t);
                state.image.color = color;
            }

            for (int i = 0; i < _textStates.Count; i++)
            {
                TextFadeState state = _textStates[i];
                if (state.text == null)
                    continue;

                Color color = state.targetColor;
                color.a = Mathf.Lerp(0f, state.targetAlpha, t);
                state.text.color = color;
            }

            yield return null;
        }

        for (int i = 0; i < _imageStates.Count; i++)
        {
            ImageFadeState state = _imageStates[i];
            if (state.image == null)
                continue;

            Color color = state.targetColor;
            color.a = state.targetAlpha;
            state.image.color = color;
        }

        for (int i = 0; i < _textStates.Count; i++)
        {
            TextFadeState state = _textStates[i];
            if (state.text == null)
                continue;

            Color color = state.targetColor;
            color.a = state.targetAlpha;
            state.text.color = color;
        }

        _fadeRoutine = null;
    }

    private void ResolvePlayerControlReferences()
    {
        Transform root = null;
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

        if (playerEventAudioPlayer == null)
            playerEventAudioPlayer = root.GetComponentInChildren<PlayerEventAudioPlayer>(true);
    }

    private void DisablePlayerControls()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerPerspective != null)
            playerPerspective.enabled = false;

        if (playerGunDual != null)
            playerGunDual.enabled = false;

        if (playerGunChannels == null)
            return;

        for (int i = 0; i < playerGunChannels.Length; i++)
        {
            if (playerGunChannels[i] != null)
                playerGunChannels[i].enabled = false;
        }

        if (playerEventAudioPlayer != null)
            playerEventAudioPlayer.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
