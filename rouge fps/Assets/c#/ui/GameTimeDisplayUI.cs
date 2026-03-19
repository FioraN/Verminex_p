using TMPro;
using UnityEngine;

public sealed class GameTimeDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private float startOffsetSeconds = 0f;

    private float _startedAt;

    private void Awake()
    {
        _startedAt = GetCurrentTime();
    }

    private void Update()
    {
        if (timeText == null)
            return;

        float elapsed = Mathf.Max(0f, GetCurrentTime() - _startedAt + startOffsetSeconds);
        int totalSeconds = Mathf.FloorToInt(elapsed);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeText.text = $"{minutes:00}:{seconds:00}";
    }

    private float GetCurrentTime()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }
}
