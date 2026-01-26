using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HitFeedbackUI : MonoBehaviour
{
    public static HitFeedbackUI Instance { get; private set; }

    [Header("UI Groups (alpha)")]
    [Tooltip("把那几个受击UI方块的 CanvasGroup 拖进来")]
    public CanvasGroup[] groups;

    [Header("UI Images (color)")]
    [Tooltip("需要变色就把 Image 拖进来（可为空）")]
    public Image[] images;

    [Header("Colors")]
    public Color bodyColor = Color.white;
    public Color headColor = Color.red;

    [Header("Fade")]
    [Min(0.01f)] public float fadeDuration = 0.35f;

    private Coroutine _fadeCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetAlphaInstant(0f);
    }

    public void ShowHit(bool isHeadshot)
    {
        Color c = isHeadshot ? headColor : bodyColor;
        SetColor(c);

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        SetAlphaInstant(1f);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeDuration);
            SetAlphaInstant(a);
            yield return null;
        }

        SetAlphaInstant(0f);
        _fadeCo = null;
    }

    private void SetAlphaInstant(float a)
    {
        if (groups == null) return;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            groups[i].alpha = a;
        }
    }

    private void SetColor(Color c)
    {
        if (images == null) return;
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            images[i].color = c;
        }
    }
}
