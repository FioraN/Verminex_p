using MoreMountains.Feedbacks;
using UnityEngine;

public class FireCameraShake : MonoBehaviour
{
    [Header("Feel")]
    public MMF_Player fireShakeFeedback;

    [Header("Filter")]
    [Tooltip("If empty, all CameraGunChannel fire events will trigger this shake.")]
    public CameraGunChannel[] sourceFilter;

    [Header("Intensity")]
    [Min(0f)] public float baseIntensity = 1f;
    public bool scaleFromGunRecoil = true;
    [Min(0.0001f)] public float recoilToIntensityDivisor = 1.5f;
    [Min(0f)] public float maxIntensity = 2f;

    private void OnEnable()
    {
        CombatEventHub.OnFire += HandleFire;
    }

    private void OnDisable()
    {
        CombatEventHub.OnFire -= HandleFire;
    }

    private void HandleFire(CombatEventHub.FireEvent e)
    {
        if (fireShakeFeedback == null || e.source == null)
            return;

        if (!MatchesFilter(e.source))
            return;

        float intensity = GetIntensityMultiplier(e.source);
        fireShakeFeedback.PlayFeedbacks(transform.position, intensity);
    }

    private bool MatchesFilter(CameraGunChannel source)
    {
        if (sourceFilter == null || sourceFilter.Length == 0)
            return true;

        for (int i = 0; i < sourceFilter.Length; i++)
        {
            if (sourceFilter[i] == source)
                return true;
        }

        return false;
    }

    private float GetIntensityMultiplier(CameraGunChannel source)
    {
        float intensity = Mathf.Max(0f, baseIntensity);

        if (scaleFromGunRecoil && source != null)
        {
            GunRecoil recoil = source.recoil;
            if (recoil == null)
                recoil = source.GetComponent<GunRecoil>() ?? source.GetComponentInParent<GunRecoil>();

            if (recoil != null)
            {
                float recoilAmount = Mathf.Max(0f, recoil.kickPitchPerShot) + Mathf.Max(0f, recoil.kickYawRandom);
                intensity *= recoilAmount / Mathf.Max(0.0001f, recoilToIntensityDivisor);
            }
        }

        if (maxIntensity > 0f)
            intensity = Mathf.Min(intensity, maxIntensity);

        return intensity;
    }
}
