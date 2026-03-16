using System;
using UnityEngine;

public class MonsterHealth : MonoBehaviour, IDamageable, IDamageableEx, IDamageableArmorEx
{
    [Header("Health")]
    [Min(1f)] public float maxHp = 100f;
    [Min(0f)] public float hp = 100f;
    [SerializeField] private bool isDeadDebug;

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
}
