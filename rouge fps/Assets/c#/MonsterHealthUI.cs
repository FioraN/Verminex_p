using UnityEngine;

public class MonsterHealth : MonoBehaviour, IDamageableEx
{
    public float maxHP = 100f;
    public float hp = 100f;

    public bool IsDead => hp <= 0f;

    private HitFeedbackUI _hitUI;

    private void Awake()
    {
        hp = Mathf.Clamp(hp <= 0 ? maxHP : hp, 0f, maxHP);

        _hitUI = HitFeedbackUI.Instance;
        if (_hitUI == null)
            _hitUI = FindFirstObjectByType<HitFeedbackUI>();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;
        hp = Mathf.Max(0f, hp - amount);
    }

    public void TakeDamage(DamageInfo info, ArmorHitInfo armorInfo)
    {
        if (info.damage <= 0f || IsDead) return;

        var armor = GetComponent<EnemyArmor>();
        if (armor == null) armor = GetComponentInParent<EnemyArmor>();

        if (armor == null || !armor.HasArmor)
        {
            TakeDamage(info);
            return;
        }

        float total = Mathf.Max(0f, info.damage);

        float pierce = Mathf.Clamp01(armorInfo.piercePercent) * total + Mathf.Max(0f, armorInfo.pierceFlat);
        pierce = Mathf.Clamp(pierce, 0f, total);

        float normal = Mathf.Max(0f, total - pierce);

        float armorDmg = normal * Mathf.Max(0f, armorInfo.armorDamageMultiplier);

        float armorTaken = armor.DamageArmor(armorDmg);

        if (armorTaken > 0f)
        {
            float extraShatter = Mathf.Max(0f, armorInfo.shatterFlat) + Mathf.Max(0f, armorInfo.shatterPercentOfArmorDamage) * armorDmg;
            if (extraShatter > 0f && armor.HasArmor)
                armor.DamageArmor(extraShatter);
        }

        float mult = Mathf.Max(0.0001f, armorInfo.armorDamageMultiplier);
        float normalAbsorbedEquivalent = armorTaken / mult;
        float normalSpillToHP = Mathf.Max(0f, normal - normalAbsorbedEquivalent);

        float hpDamage = pierce + normalSpillToHP;

        if (hpDamage > 0f)
        {
            var hpInfo = info;
            hpInfo.damage = hpDamage;
            TakeDamage(hpInfo);
        }
    }

    public void TakeDamage(DamageInfo info)
    {
        if (info.damage <= 0f || IsDead) return;

        hp = Mathf.Max(0f, hp - info.damage);

        if ((info.flags & DamageFlags.SkipHitEvent) != 0)
        {
            if (info.source != null)
            {
                if (_hitUI == null)
                {
                    _hitUI = HitFeedbackUI.Instance;
                    if (_hitUI == null)
                        _hitUI = FindFirstObjectByType<HitFeedbackUI>();
                }

                if (_hitUI != null)
                {
                    _hitUI.ShowHit(info.isHeadshot);
                }
            }
        }

        if ((info.flags & DamageFlags.SkipHitEvent) == 0)
        {
            CombatEventHub.RaiseHit(new CombatEventHub.HitEvent
            {
                source = info.source,
                target = gameObject,
                hitCollider = info.hitCollider,
                hitPoint = info.hitPoint,
                damage = info.damage,
                isHeadshot = info.isHeadshot,
                time = Time.time
            });
        }

        if (IsDead)
        {
            CombatEventHub.RaiseKill(new CombatEventHub.KillEvent
            {
                source = info.source,
                target = gameObject,
                time = Time.time
            });
        }
    }
}
