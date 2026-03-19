using UnityEngine;
using System.Collections;

public sealed class PlayerEventAudioPlayer : MonoBehaviour
{
    public static PlayerEventAudioPlayer Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private PlayerExperience playerExperience;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource reloadAudioSource;
    [SerializeField] private Transform playerRoot;

    [Header("Experience Orb Pickup")]
    [SerializeField] private bool playExperienceOrbPickup = true;
    [SerializeField] private AudioClip[] experienceOrbPickupClips;
    [SerializeField] [Min(0f)] private float experienceOrbPickupVolume = 1f;
    [SerializeField] private Vector2 experienceOrbPickupPitchRange = new Vector2(1f, 1f);

    [Header("Fire")]
    [SerializeField] private bool playFireSound = true;
    [SerializeField] private AudioClip[] fireClips;
    [SerializeField] [Min(0f)] private float fireVolume = 1f;
    [SerializeField] private Vector2 firePitchRange = new Vector2(1f, 1f);
    [SerializeField] private bool repeatShotgunFireByPelletCount = true;
    [SerializeField] [Min(1)] private int maxShotgunFireRepeats = 6;
    [SerializeField] [Min(0f)] private float shotgunFireRepeatDelay = 0.015f;

    [Header("Headshot Hit")]
    [SerializeField] private bool playHeadshotHitSound = true;
    [SerializeField] private AudioClip[] headshotHitClips;
    [SerializeField] [Min(0f)] private float headshotHitVolume = 1f;
    [SerializeField] private Vector2 headshotHitPitchRange = new Vector2(1f, 1f);

    [Header("Body Hit")]
    [SerializeField] private bool playBodyHitSound = true;
    [SerializeField] private AudioClip[] bodyHitClips;
    [SerializeField] [Min(0f)] private float bodyHitVolume = 1f;
    [SerializeField] private Vector2 bodyHitPitchRange = new Vector2(1f, 1f);

    [Header("Kill")]
    [SerializeField] private bool playKillSound = true;
    [SerializeField] private AudioClip[] killClips;
    [SerializeField] [Min(0f)] private float killVolume = 1f;
    [SerializeField] private Vector2 killPitchRange = new Vector2(1f, 1f);

    [Header("Reload")]
    [SerializeField] private bool playReloadStartSound = true;
    [SerializeField] private AudioClip[] reloadClips;
    [SerializeField] [Min(0f)] private float reloadVolume = 1f;
    [SerializeField] [Min(0.01f)] private float minReloadPitch = 0.1f;
    [SerializeField] [Min(0.01f)] private float maxReloadPitch = 3f;

    [Header("Per-Bullet Reload Insert")]
    [SerializeField] private bool playPerBulletReloadInsertSound = true;
    [SerializeField] private AudioClip[] perBulletReloadInsertClips;
    [SerializeField] [Min(0f)] private float perBulletReloadInsertVolume = 1f;
    [SerializeField] private Vector2 perBulletReloadInsertPitchRange = new Vector2(1f, 1f);
    [SerializeField] [Min(0f)] private float perBulletReloadInsertRepeatDelay = 0.01f;

    [Header("Perk UI Click")]
    [SerializeField] private bool playPerkUiClickSound = true;
    [SerializeField] private AudioClip[] perkUiClickClips;
    [SerializeField] [Min(0f)] private float perkUiClickVolume = 1f;
    [SerializeField] private Vector2 perkUiClickPitchRange = new Vector2(1f, 1f);

    [Header("Combat Music")]
    [SerializeField] private bool playCombatMusic = true;
    [SerializeField] private AudioSource combatMusicAudioSource;
    [SerializeField] private AudioClip combatMusicClip;
    [SerializeField] [Min(1)] private int combatMusicMonsterThreshold = 4;
    [SerializeField] [Min(0f)] private float combatMusicStartVolume = 0f;
    [SerializeField] [Min(0f)] private float combatMusicTargetVolume = 1f;
    [SerializeField] [Min(0.01f)] private float combatMusicFadeInDuration = 2f;
    [SerializeField] [Min(0.01f)] private float combatMusicFadeOutDuration = 1.25f;
    [SerializeField] [Min(0f)] private float combatMusicHoldDuration = 6f;
    [SerializeField] [Min(0f)] private float combatMusicMinPlayDuration = 8f;
    [SerializeField] [Min(0.1f)] private float combatMusicCheckInterval = 0.5f;

    private float _nextCombatMusicCheckAt;
    private float _combatMusicStartedAt = -9999f;
    private float _combatMusicHoldUntil = -9999f;
    private bool _combatMusicWantsToPlay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (playerExperience == null)
            playerExperience = GetComponentInParent<PlayerExperience>();

        if (playerExperience == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerExperience = player.GetComponentInParent<PlayerExperience>();
        }

        if (playerRoot == null)
        {
            if (playerExperience != null)
            {
                playerRoot = playerExperience.transform.root;
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerRoot = player.transform.root;
            }
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (reloadAudioSource == null)
            reloadAudioSource = audioSource;

        if (combatMusicAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != audioSource)
                {
                    combatMusicAudioSource = sources[i];
                    break;
                }
            }

            if (combatMusicAudioSource == null)
                combatMusicAudioSource = audioSource;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        ExperienceEventHub.OnOrbCollected += HandleOrbCollected;
        CombatEventHub.OnFire += HandleFire;
        CombatEventHub.OnHit += HandleHit;
        CombatEventHub.OnKill += HandleKill;
        CombatEventHub.OnReload += HandleReload;
        CombatEventHub.OnReloadInsert += HandleReloadInsert;
    }

    private void OnDisable()
    {
        ExperienceEventHub.OnOrbCollected -= HandleOrbCollected;
        CombatEventHub.OnFire -= HandleFire;
        CombatEventHub.OnHit -= HandleHit;
        CombatEventHub.OnKill -= HandleKill;
        CombatEventHub.OnReload -= HandleReload;
        CombatEventHub.OnReloadInsert -= HandleReloadInsert;

        if (combatMusicAudioSource != null && combatMusicAudioSource.isPlaying)
            combatMusicAudioSource.Stop();

        _combatMusicWantsToPlay = false;
    }

    private void Update()
    {
        UpdateCombatMusicState();
        AnimateCombatMusicVolume();
    }

    private void HandleOrbCollected(ExperienceEventHub.OrbCollectedEvent e)
    {
        if (!playExperienceOrbPickup)
            return;

        if (playerExperience != null && e.receiver != playerExperience)
            return;

        PlayClip(experienceOrbPickupClips, experienceOrbPickupVolume, experienceOrbPickupPitchRange);
    }

    private void HandleFire(CombatEventHub.FireEvent e)
    {
        if (!playFireSound)
            return;

        if (e.source == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        int playCount = 1;
        if (repeatShotgunFireByPelletCount && e.source.shotType == CameraGunChannel.ShotType.Shotgun)
            playCount = Mathf.Clamp(Mathf.Max(1, e.pellets), 1, Mathf.Max(1, maxShotgunFireRepeats));

        if (playCount <= 1 || shotgunFireRepeatDelay <= 0f)
        {
            for (int i = 0; i < playCount; i++)
                PlayClip(fireClips, fireVolume, firePitchRange);
            return;
        }

        StartCoroutine(PlayRepeatedFireClips(playCount));
    }

    private void HandleHit(CombatEventHub.HitEvent e)
    {
        if (e.source == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        AudioSource targetAudioSource = ResolveTargetAudioSource(e.target);

        if (e.isHeadshot)
        {
            if (!playHeadshotHitSound)
                return;

            PlayClipOnSource(targetAudioSource, headshotHitClips, headshotHitVolume, headshotHitPitchRange);
            return;
        }

        if (!playBodyHitSound)
            return;

        PlayClipOnSource(targetAudioSource, bodyHitClips, bodyHitVolume, bodyHitPitchRange);
    }

    private void HandleKill(CombatEventHub.KillEvent e)
    {
        if (!playKillSound)
            return;

        if (e.source == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        PlayClipOnSource(ResolveTargetAudioSource(e.target), killClips, killVolume, killPitchRange);
    }

    private void HandleReload(CombatEventHub.ReloadEvent e)
    {
        if (e.source == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        if (e.source.ammo == null)
            return;

        if (e.source.ammo.reloadType != GunAmmo.ReloadType.Magazine)
        {
            if (e.isStart)
                StopReloadClipIfPlaying();
            return;
        }

        if (e.isStart)
        {
            if (!playReloadStartSound)
                return;

            PlayReloadClipMatchedToDuration(e.source);
            return;
        }

        StopReloadClipIfPlaying();
    }

    private void HandleReloadInsert(CombatEventHub.ReloadInsertEvent e)
    {
        if (!playPerBulletReloadInsertSound)
            return;

        if (e.source == null || e.source.ammo == null)
            return;

        if (!IsOwnedByCurrentPlayer(e.source))
            return;

        if (e.source.ammo.reloadType != GunAmmo.ReloadType.PerBullet)
            return;

        int playCount = Mathf.Max(1, e.insertedCount);
        if (playCount <= 1 || perBulletReloadInsertRepeatDelay <= 0f)
        {
            for (int i = 0; i < playCount; i++)
                PlayClip(perBulletReloadInsertClips, perBulletReloadInsertVolume, perBulletReloadInsertPitchRange);
            return;
        }

        StartCoroutine(PlayRepeatedClips(
            perBulletReloadInsertClips,
            perBulletReloadInsertVolume,
            perBulletReloadInsertPitchRange,
            playCount,
            perBulletReloadInsertRepeatDelay));
    }

    private IEnumerator PlayRepeatedFireClips(int playCount)
    {
        yield return PlayRepeatedClips(fireClips, fireVolume, firePitchRange, playCount, shotgunFireRepeatDelay);
    }

    private bool IsOwnedByCurrentPlayer(CameraGunChannel source)
    {
        if (source == null)
            return false;

        if (playerExperience != null)
        {
            PlayerExperience sourceOwner = source.GetComponentInParent<PlayerExperience>();
            if (sourceOwner != null)
                return sourceOwner == playerExperience;

            if (playerRoot != null)
                return source.transform.root == playerRoot;

            return true;
        }

        if (playerRoot != null)
            return source.transform.root == playerRoot;

        return true;
    }

    public void PlayPerkUiClick()
    {
        if (!playPerkUiClickSound)
            return;

        PlayClip(perkUiClickClips, perkUiClickVolume, perkUiClickPitchRange);
    }

    private void UpdateCombatMusicState()
    {
        if (!playCombatMusic || combatMusicAudioSource == null || combatMusicClip == null)
            return;

        if (Time.time < _nextCombatMusicCheckAt)
            return;

        _nextCombatMusicCheckAt = Time.time + Mathf.Max(0.1f, combatMusicCheckInterval);

        int aliveMonsterCount = CountAliveMonsters();
        if (aliveMonsterCount >= Mathf.Max(1, combatMusicMonsterThreshold))
        {
            StartOrRefreshCombatMusic();
            return;
        }

        float minStopTime = _combatMusicStartedAt + Mathf.Max(0f, combatMusicMinPlayDuration);
        bool canStopNow = Time.time >= minStopTime && Time.time >= _combatMusicHoldUntil;
        if (canStopNow)
            _combatMusicWantsToPlay = false;
    }

    private void StartOrRefreshCombatMusic()
    {
        _combatMusicWantsToPlay = true;
        _combatMusicHoldUntil = Mathf.Max(_combatMusicHoldUntil, Time.time + Mathf.Max(0f, combatMusicHoldDuration));

        if (combatMusicAudioSource.clip != combatMusicClip)
            combatMusicAudioSource.clip = combatMusicClip;

        combatMusicAudioSource.loop = true;

        if (!combatMusicAudioSource.isPlaying)
        {
            _combatMusicStartedAt = Time.time;
            combatMusicAudioSource.volume = Mathf.Max(0f, combatMusicStartVolume);
            combatMusicAudioSource.Play();
        }
    }

    private void AnimateCombatMusicVolume()
    {
        if (!playCombatMusic || combatMusicAudioSource == null)
            return;

        if (_combatMusicWantsToPlay)
        {
            float fadeSpeed = Mathf.Approximately(combatMusicFadeInDuration, 0f)
                ? float.MaxValue
                : Mathf.Abs(combatMusicTargetVolume - combatMusicStartVolume) / combatMusicFadeInDuration;

            combatMusicAudioSource.volume = Mathf.MoveTowards(
                combatMusicAudioSource.volume,
                Mathf.Max(0f, combatMusicTargetVolume),
                fadeSpeed * Time.deltaTime);
            return;
        }

        if (!combatMusicAudioSource.isPlaying)
            return;

        float fadeOutSpeed = Mathf.Approximately(combatMusicFadeOutDuration, 0f)
            ? float.MaxValue
            : Mathf.Max(0f, combatMusicTargetVolume) / combatMusicFadeOutDuration;

        combatMusicAudioSource.volume = Mathf.MoveTowards(combatMusicAudioSource.volume, 0f, fadeOutSpeed * Time.deltaTime);
        if (combatMusicAudioSource.volume <= 0.0001f)
        {
            combatMusicAudioSource.Stop();
            combatMusicAudioSource.clip = combatMusicClip;
            combatMusicAudioSource.volume = 0f;
        }
    }

    private static int CountAliveMonsters()
    {
        MonsterHealth[] allMonsters = FindObjectsByType<MonsterHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int aliveCount = 0;

        for (int i = 0; i < allMonsters.Length; i++)
        {
            MonsterHealth monster = allMonsters[i];
            if (monster == null || monster.IsDead)
                continue;

            aliveCount++;
        }

        return aliveCount;
    }

    private IEnumerator PlayRepeatedClips(AudioClip[] clips, float volume, Vector2 pitchRange, int playCount, float repeatDelay)
    {
        int safeCount = Mathf.Max(1, playCount);
        float delay = Mathf.Max(0f, repeatDelay);

        for (int i = 0; i < safeCount; i++)
        {
            PlayClip(clips, volume, pitchRange);

            if (i < safeCount - 1 && delay > 0f)
                yield return new WaitForSeconds(delay);
        }
    }

    private void PlayReloadClipMatchedToDuration(CameraGunChannel source)
    {
        if (reloadAudioSource == null)
            return;

        AudioClip clip = ChooseRandomClip(reloadClips);
        if (clip == null)
            return;

        float targetDuration = EstimateReloadDuration(source);
        if (targetDuration <= 0.01f)
        {
            reloadAudioSource.pitch = 1f;
            reloadAudioSource.PlayOneShot(clip, Mathf.Max(0f, reloadVolume));
            return;
        }

        float pitch = clip.length / targetDuration;
        pitch = Mathf.Clamp(pitch, Mathf.Max(0.01f, minReloadPitch), Mathf.Max(minReloadPitch, maxReloadPitch));

        reloadAudioSource.Stop();
        reloadAudioSource.clip = clip;
        reloadAudioSource.loop = false;
        reloadAudioSource.volume = Mathf.Max(0f, reloadVolume);
        reloadAudioSource.pitch = pitch;
        reloadAudioSource.Play();
    }

    private void StopReloadClipIfPlaying()
    {
        if (reloadAudioSource == null)
            return;

        if (reloadAudioSource.isPlaying)
            reloadAudioSource.Stop();
    }

    private static float EstimateReloadDuration(CameraGunChannel source)
    {
        if (source == null || source.ammo == null)
            return 0f;

        GunAmmo ammo = source.ammo;
        if (ammo.reloadType == GunAmmo.ReloadType.Magazine)
            return Mathf.Max(0f, ammo.reloadTimeMagazine);

        int missingAmmo = Mathf.Max(0, ammo.magazineSize - ammo.ammoInMag);
        int availableReserve = Mathf.Max(0, ammo.ammoReserve);
        int insertCountPerStep = Mathf.Max(1, ammo.insertCountPerStep);
        int bulletsToInsert = Mathf.Min(missingAmmo, availableReserve);
        int insertSteps = bulletsToInsert > 0
            ? Mathf.CeilToInt((float)bulletsToInsert / insertCountPerStep)
            : 0;

        return Mathf.Max(0f, ammo.reloadStartTime)
             + Mathf.Max(0f, ammo.insertOneTime) * insertSteps
             + Mathf.Max(0f, ammo.reloadEndTime);
    }

    private void PlayClip(AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        PlayClipOnSource(audioSource, clips, volume, pitchRange);
    }

    private void PlayClipOnSource(AudioSource source, AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        AudioClip clip = ChooseRandomClip(clips);
        if (clip == null || source == null)
            return;

        float originalPitch = source.pitch;
        source.pitch = Random.Range(
            Mathf.Min(pitchRange.x, pitchRange.y),
            Mathf.Max(pitchRange.x, pitchRange.y));
        source.PlayOneShot(clip, Mathf.Max(0f, volume));
        source.pitch = originalPitch;
    }

    private AudioSource ResolveTargetAudioSource(GameObject target)
    {
        if (target == null)
            return audioSource;

        AudioSource direct = target.GetComponent<AudioSource>();
        if (direct != null)
            return direct;

        AudioSource inParent = target.GetComponentInParent<AudioSource>();
        if (inParent != null)
            return inParent;

        AudioSource inChildren = target.GetComponentInChildren<AudioSource>(true);
        if (inChildren != null)
            return inChildren;

        return audioSource;
    }

    private static AudioClip ChooseRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (pick == 0)
                return clips[i];

            pick--;
        }

        return null;
    }
}
