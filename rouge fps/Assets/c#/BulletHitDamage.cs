using UnityEngine;

public class BulletHitDamage : MonoBehaviour
{
    [Header("Damage (set when spawned)")]
    public float baseDamage = 10f;

    [Tooltip("The gun channel that fired this bullet.")]
    public CameraGunChannel source;

    [Header("Destroy")]
    public bool destroyOnHit = true;

    private bool _didHit;

    public void Init(float damage, CameraGunChannel src)
    {
        baseDamage = damage;
        source = src;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_didHit) return;
        if (other == null) return;

        var armorPayload = GetComponentInChildren<BulletArmorPayload>(true);
        var statusPayload = GetComponentInChildren<BulletStatusPayload>(true);

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        var info = new DamageInfo
        {
            source = source,
            damage = baseDamage,
            isHeadshot = false,
            hitPoint = hitPoint,
            hitCollider = other,
            flags = DamageFlags.None
        };

        bool applied = DamageResolver.ApplyHit(
            baseInfo: info,
            hitCol: other,
            hitPoint: hitPoint,
            source: source,
            armorPayload: armorPayload,
            statusPayload: statusPayload,
            showHitUI: true
        );

        if (!applied) return;

        _didHit = true;
        if (destroyOnHit) Destroy(gameObject);
    }
}
