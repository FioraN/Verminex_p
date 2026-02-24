using UnityEngine;

public static class DamageResolver
{
    /// <summary>
    /// Unified hit application for both raycast bullets and trigger bullets.
    /// - Applies hitbox multiplier/headshot
    /// - Applies armor payload if target supports IDamageableArmorEx
    /// - Applies status payload (StatusContainer)
    /// - Shows hit feedback UI
    /// - Raises CombatEventHub.OnHit (and relies on MonsterHealth to raise OnKill)
    /// </summary>
    public static bool ApplyHit(
        DamageInfo baseInfo,
        Collider hitCol,
        Vector3 hitPoint,
        CameraGunChannel source,
        BulletArmorPayload armorPayload = null,
        BulletStatusPayload statusPayload = null,
        bool showHitUI = true
    )
    {
        if (hitCol == null) return false;

        // Resolve target interfaces
        var armorEx = hitCol.GetComponentInParent<IDamageableArmorEx>();
        var dmgEx = hitCol.GetComponentInParent<IDamageableEx>();
        var dmg = hitCol.GetComponentInParent<IDamageable>();

        if (armorEx == null && dmgEx == null && dmg == null)
            return false;

        // Hitbox
        float partMult = 1f;
        bool isHeadshot = false;
        var hb = hitCol.GetComponent<Hitbox>();
        if (hb != null)
        {
            partMult = Mathf.Max(0f, hb.damageMultiplier);
            isHeadshot = hb.part == Hitbox.Part.Head;
        }

        var info = baseInfo;
        info.source = source;
        info.isHeadshot = isHeadshot;
        info.hitPoint = hitPoint;
        info.hitCollider = hitCol;
        info.damage = Mathf.Max(0f, info.damage) * partMult;

        // Apply damage (armor-aware first)
        if (armorEx != null && armorPayload != null)
        {
            ArmorHitInfo armorInfo = armorPayload.ToArmorHitInfo();
            armorEx.TakeDamage(info, armorInfo);
        }
        else if (dmgEx != null)
        {
            dmgEx.TakeDamage(info);
        }
        else
        {
            dmg.TakeDamage(info.damage);
        }

        // Status payload
        if (statusPayload != null && statusPayload.entries != null && statusPayload.entries.Length > 0)
        {
            var sc = hitCol.GetComponentInParent<StatusContainer>();
            if (sc != null)
            {
                for (int i = 0; i < statusPayload.entries.Length; i++)
                {
                    var e = statusPayload.entries[i];
                    sc.ApplyStatus(new StatusApplyRequest
                    {
                        type = e.type,
                        stacksToAdd = e.stacksToAdd,
                        duration = e.duration,
                        source = source,

                        tickInterval = e.tickInterval,
                        burnDamagePerTickPerStack = e.burnDamagePerTickPerStack,

                        slowPerStack = e.slowPerStack,
                        weakenPerStack = e.weakenPerStack,

                        shockChainDamagePerStack = e.shockChainDamagePerStack,
                        shockChainRadius = e.shockChainRadius,
                        shockMaxChains = e.shockMaxChains
                    });
                }
            }
        }

        // Hit feedback UI
        if (showHitUI)
        {
            var ui = HitFeedbackUI.Instance;
            if (ui == null) ui = Object.FindFirstObjectByType<HitFeedbackUI>();
            if (ui != null) ui.ShowHit(isHeadshot);
        }

        // Raise hit event (for perks / systems)
        CombatEventHub.RaiseHit(new CombatEventHub.HitEvent
        {
            source = source,
            target = (hitCol.GetComponentInParent<MonsterHealth>() != null)
                ? hitCol.GetComponentInParent<MonsterHealth>().gameObject
                : hitCol.gameObject,
            hitCollider = hitCol,
            hitPoint = hitPoint,
            damage = info.damage,
            isHeadshot = isHeadshot,
            time = Time.time,
            armorPayload = armorPayload,
            statusPayload = statusPayload,
            flags = info.flags
        });


        return true;
    }
}
