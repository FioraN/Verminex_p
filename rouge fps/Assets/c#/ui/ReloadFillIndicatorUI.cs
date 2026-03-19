using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReloadFillIndicatorUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Image indicatorImage;

    [Header("Timing")]
    [Range(0f, 0.45f)] [SerializeField] private float fadeInFraction = 0.12f;
    [Range(0f, 0.45f)] [SerializeField] private float fadeOutFraction = 0.12f;

    private Coroutine _animCo;
    private CameraGunChannel _activeSource;

    private void Awake()
    {
        if (playerRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerRoot = player.transform.root;
        }

        if (indicatorImage == null)
            indicatorImage = GetComponent<Image>();

        ResetVisualImmediate();
    }

    private void OnEnable()
    {
        ResetVisualImmediate();
        CombatEventHub.OnReload += HandleReload;
    }

    private void OnDisable()
    {
        CombatEventHub.OnReload -= HandleReload;
        StopCurrentAnimation();
        ResetVisualImmediate();
    }

    private void HandleReload(CombatEventHub.ReloadEvent e)
    {
        if (e.source == null || e.source.ammo == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        if (e.source.ammo.reloadType != GunAmmo.ReloadType.Magazine)
            return;

        if (e.isStart)
        {
            StartReloadAnimation(e.source, Mathf.Max(0.01f, e.source.ammo.reloadTimeMagazine));
            return;
        }

        if (e.source == _activeSource)
            _activeSource = null;
    }

    private void StartReloadAnimation(CameraGunChannel source, float duration)
    {
        StopCurrentAnimation();
        _activeSource = source;
        _animCo = StartCoroutine(ReloadAnimationRoutine(duration));
    }

    private IEnumerator ReloadAnimationRoutine(float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeFadeIn = Mathf.Clamp01(fadeInFraction) * safeDuration;
        float safeFadeOut = Mathf.Clamp01(fadeOutFraction) * safeDuration;
        float fillDuration = Mathf.Max(0.0001f, safeDuration - safeFadeIn - safeFadeOut);

        SetAlpha(0f);
        SetFill(1f);

        if (safeFadeIn > 0f)
        {
            float t = 0f;
            while (t < safeFadeIn)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(t / safeFadeIn));
                yield return null;
            }
        }

        SetAlpha(1f);
        SetFill(1f);

        float fillElapsed = 0f;
        while (fillElapsed < fillDuration)
        {
            fillElapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(fillElapsed / fillDuration);
            SetFill(1f - normalized);
            yield return null;
        }

        SetFill(0f);

        if (safeFadeOut > 0f)
        {
            float t = 0f;
            while (t < safeFadeOut)
            {
                t += Time.deltaTime;
                SetAlpha(1f - Mathf.Clamp01(t / safeFadeOut));
                yield return null;
            }
        }

        ResetVisualImmediate();
        _animCo = null;
        _activeSource = null;
    }

    private bool IsOwnedByCurrentPlayer(CameraGunChannel source)
    {
        if (source == null)
            return false;

        if (playerRoot != null)
            return source.transform.root == playerRoot;

        return true;
    }

    private void StopCurrentAnimation()
    {
        if (_animCo == null)
            return;

        StopCoroutine(_animCo);
        _animCo = null;
        _activeSource = null;
    }

    private void ResetVisualImmediate()
    {
        SetAlpha(0f);
        SetFill(1f);
    }

    private void SetAlpha(float alpha)
    {
        if (indicatorImage == null)
            return;

        Color color = indicatorImage.color;
        color.a = Mathf.Clamp01(alpha);
        indicatorImage.color = color;
    }

    private void SetFill(float value)
    {
        if (indicatorImage != null)
            indicatorImage.fillAmount = Mathf.Clamp01(value);
    }
}
