using System;
using UnityEngine;

public class MonsterHealth : MonoBehaviour, IDamageable, IDamageableEx, IDamageableArmorEx
{
    [Header("Health")]
    [Min(1f)] public float maxHp = 100f;
    [Min(0f)] public float hp = 100f;
    [SerializeField] private bool isDeadDebug;

    [Header("Experience Drop")]
    public ExperienceOrb experienceOrbPrefab;
    [Min(1)] public int totalExperience = 1;
    [Min(1)] public int minOrbCount = 1;
    [Min(1)] public int maxOrbCount = 1;
    public Vector3 experienceSpawnOffset = new Vector3(0f, 1f, 0f);
    [Min(0f)] public float experienceScatterRadius = 0.6f;

    [Header("Hit UI")]
    public bool autoFindHitUI = true;

    private HitFeedbackUI _hitUI;
    private bool _didDie;

    private CameraGunChannel _lastHitSource;
    private float _lastHitTime;

    public event Action<DamageInfo> Damaged;
    public event Action<DamageInfo> Died;

    public CameraGunChannel LastHitSource => _lastHitSource;
    public float LastHitTime => _lastHitTime;
    public bool IsDead => hp <= 0.0001f;

    private void Awake()
    {
        if (maxHp < 1f) maxHp = 1f;
        hp = Mathf.Clamp(hp, 0f, maxHp);
        SyncDebugState();

        if (autoFindHitUI)
            TryResolveHitUI();
    }

    private void OnEnable()
    {
        SyncDebugState();
        CombatEventHub.OnHit += HandleHitEvent;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= HandleHitEvent;
    }

    private void TryResolveHitUI()
    {
        _hitUI = HitFeedbackUI.Instance;
        if (_hitUI == null)
            _hitUI = FindFirstObjectByType<HitFeedbackUI>();
    }

    private void HandleHitEvent(CombatEventHub.HitEvent e)
    {
        if (IsDead) return;
        if (e.target == null) return;

        if (e.target == gameObject)
        {
            if (e.source != null)
            {
                _lastHitSource = e.source;
                _lastHitTime = Time.time;
            }
            return;
        }

        if (e.target.transform != null && e.target.transform.IsChildOf(transform))
        {
            if (e.source != null)
            {
                _lastHitSource = e.source;
                _lastHitTime = Time.time;
            }
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(new DamageInfo
        {
            damage = damage,
            source = null,
            isHeadshot = false,
            hitPoint = transform.position,
            hitCollider = null,
            flags = DamageFlags.SkipHitEvent
        });
    }

    public void TakeDamage(DamageInfo info)
    {
        if (info.damage <= 0f || IsDead) return;

        hp = Mathf.Max(0f, hp - info.damage);
        SyncDebugState();
        Damaged?.Invoke(info);

        if ((info.flags & DamageFlags.SkipHitEvent) == 0 && info.source != null)
        {
            if (_hitUI == null && autoFindHitUI) TryResolveHitUI();
            if (_hitUI != null) _hitUI.ShowHit(info.isHeadshot);
        }

        TryRaiseKill(info);
    }

    public void TakeDamage(DamageInfo info, ArmorHitInfo armorInfo)
    {
        if (info.damage <= 0f || IsDead) return;

        float damage = info.damage;
        float overflowDamage = damage;

        EnemyArmor armor = GetComponentInParent<EnemyArmor>();

        if (armor != null && armor.HasArmor)
        {
            float armorTaken = armor.DamageArmor(damage);
            overflowDamage = damage - armorTaken;
        }

        if (overflowDamage > 0f)
        {
            float hpDamage = overflowDamage;

            if (armor != null && armor.InVulnerableWindow)
            {
                hpDamage *= armor.HpDamageMultiplier;
            }

            hp = Mathf.Max(0f, hp - hpDamage);
        }

        SyncDebugState();
        Damaged?.Invoke(info);

        if ((info.flags & DamageFlags.SkipHitEvent) == 0 && info.source != null)
        {
            if (_hitUI == null && autoFindHitUI) TryResolveHitUI();
            if (_hitUI != null) _hitUI.ShowHit(info.isHeadshot);
        }

        TryRaiseKill(info);
    }

    private void TryRaiseKill(DamageInfo info)
    {
        if (_didDie) return;
        if (!IsDead) return;

        _didDie = true;
        SyncDebugState();
        SpawnExperienceOrbs();
        Died?.Invoke(info);

        CombatEventHub.RaiseKill(new CombatEventHub.KillEvent
        {
            source = info.source,
            target = gameObject,
            time = Time.time
        });
    }

    private void SyncDebugState()
    {
        isDeadDebug = IsDead;
    }

    private void SpawnExperienceOrbs()
    {
        if (experienceOrbPrefab == null)
            return;

        int safeMin = Mathf.Max(1, minOrbCount);
        int safeMax = Mathf.Max(safeMin, maxOrbCount);
        int orbCount = UnityEngine.Random.Range(safeMin, safeMax + 1);
        int safeTotal = Mathf.Max(1, totalExperience);
        Vector3 origin = transform.position + experienceSpawnOffset;

        for (int i = 0; i < orbCount; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * experienceScatterRadius;
            Vector3 position = origin + new Vector3(circle.x, 0f, circle.y);
            ExperienceOrb orb = Instantiate(experienceOrbPrefab, position, Quaternion.identity);
            orb.experienceValue = GetOrbValue(i, orbCount, safeTotal);
        }
    }

    private static int GetOrbValue(int index, int count, int total)
    {
        int baseValue = total / count;
        int remainder = total % count;
        return baseValue + (index < remainder ? 1 : 0);
    }
}
