using System.Collections.Generic;
using UnityEngine;

public sealed class Perk_PenetrateOneTarget : MonoBehaviour
{
    [Header("Penetration")]
    [Range(0f, 1f)]
    public float secondHitDamageMultiplier = 0.6f;

    [Header("Safety")]
    public bool disableIfNotAllowed = true;
    public bool requirePrerequisites = true;

    private PerkManager _perkManager;
    private CameraGunChannel _boundChannel;

    private static readonly Dictionary<CameraGunChannel, Config> _configs = new();

    public struct Config
    {
        public float secondHitDamageMultiplier;
    }

    public static bool TryGetConfig(CameraGunChannel src, out Config cfg)
    {
        if (src != null && _configs.TryGetValue(src, out cfg))
            return true;

        cfg = default;
        return false;
    }

    private void Awake()
    {
        _perkManager = FindFirstObjectByType<PerkManager>();
    }

    private void OnEnable()
    {
        _perkManager ??= FindFirstObjectByType<PerkManager>();
        if (_perkManager == null)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        int gunIndex = ResolveGunIndexFromManager();
        if (gunIndex < 0)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        if (requirePrerequisites && !_perkManager.PrerequisitesMet(gameObject, gunIndex))
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        var gun = _perkManager.GetGun(gunIndex);
        _boundChannel = gun != null ? gun.cameraGunChannel : null;

        if (_boundChannel == null)
        {
            if (disableIfNotAllowed) enabled = false;
            return;
        }

        _configs[_boundChannel] = new Config
        {
            secondHitDamageMultiplier = Mathf.Clamp01(secondHitDamageMultiplier)
        };
    }

    private void OnDisable()
    {
        if (_boundChannel != null)
            _configs.Remove(_boundChannel);
    }

    private int ResolveGunIndexFromManager()
    {
        if (_perkManager == null) return -1;

        if (_perkManager.selectedPerksGunA != null && _perkManager.selectedPerksGunA.Contains(this)) return 0;
        if (_perkManager.selectedPerksGunB != null && _perkManager.selectedPerksGunB.Contains(this)) return 1;

        return -1;
    }
}
