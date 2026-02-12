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

    // Penetration runtime
    private bool _penetrationEnabled;
    private float _secondHitMultiplier = 1f;
    private int _enemyHitCount = 0;
    private MonsterHealth _firstEnemyHit;

    // Small cooldown to avoid multi-trigger in the same overlap/frame
    private float _nextHitAllowedTime = 0f;



    public void Init(float damage, CameraGunChannel src)
    {
        baseDamage = damage;
        source = src;

        if (Perk_PenetrateOneTarget.TryGetConfig(src, out var cfg))
        {
            _penetrationEnabled = true;
            _secondHitMultiplier = Mathf.Clamp01(cfg.secondHitDamageMultiplier);
        }
        else
        {
            _penetrationEnabled = false;
            _secondHitMultiplier = 1f;
        }

        _enemyHitCount = 0;
        _firstEnemyHit = null;
        _didHit = false;
        _nextHitAllowedTime = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (Time.time < _nextHitAllowedTime) return;
        if (_didHit) return;

        MonsterHealth mh = other.GetComponentInParent<MonsterHealth>();
        bool isEnemy = mh != null;

        // If penetration enabled and we hit a non-enemy (wall/props), destroy immediately.
        if (_penetrationEnabled && !isEnemy)
        {
            _didHit = true;
            Destroy(gameObject);
            return;
        }

        // Penetration enabled: prevent counting the same enemy twice (multi-collider)
        if (_penetrationEnabled && isEnemy)
        {
            if (_enemyHitCount >= 1 && mh == _firstEnemyHit)
            {
                // Ignore this overlap; allow trying again once we've moved out
                _nextHitAllowedTime = Time.time + 0.02f;
                return;
            }
        }

        var armorPayload = GetComponentInChildren<BulletArmorPayload>(true);
        var statusPayload = GetComponentInChildren<BulletStatusPayload>(true);

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        float damageToApply = baseDamage;

        // Second enemy hit only gets reduced damage
        if (_penetrationEnabled && isEnemy && _enemyHitCount == 1 && mh != _firstEnemyHit)
        {
            damageToApply = baseDamage * _secondHitMultiplier;
        }

        var info = new DamageInfo
        {
            source = source,
            damage = damageToApply,
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

        // If we hit something that doesn't accept damage:
        // - With penetration enabled: if it's non-enemy, destroy (already handled above)
        // - Without penetration: keep original behavior (do nothing)
        if (!applied)
        {
            if (_penetrationEnabled && !isEnemy)
            {
                _didHit = true;
                Destroy(gameObject);
            }
            return;
        }

        // Original behavior when penetration is NOT enabled
        if (!_penetrationEnabled)
        {
            _didHit = true;
            if (destroyOnHit) Destroy(gameObject);
            return;
        }

        // Penetration enabled: bookkeeping + collision ignore
        if (isEnemy)
        {
            if (_enemyHitCount == 0)
            {
                _enemyHitCount = 1;
                _firstEnemyHit = mh;

                // Key fix: actually allow the bullet to pass through the first enemy physically
                IgnoreAllEnemyColliders(mh);

                // Do NOT destroy on first enemy hit
                _nextHitAllowedTime = Time.time + 0.02f; // tiny cooldown to prevent immediate re-hit
                return;
            }

            // Second enemy hit (different enemy) -> destroy
            _enemyHitCount = 2;
            _didHit = true;
            Destroy(gameObject);
            return;
        }

        // Safety fallback
        _didHit = true;
        Destroy(gameObject);
    }

    private void IgnoreAllEnemyColliders(MonsterHealth mh)
    {
        if (mh == null) return;

        // Bullet may have multiple colliders
        var bulletCols = GetComponentsInChildren<Collider>(true);
        if (bulletCols == null || bulletCols.Length == 0) return;

        var enemyCols = mh.GetComponentsInChildren<Collider>(true);
        if (enemyCols == null || enemyCols.Length == 0) return;

        for (int i = 0; i < bulletCols.Length; i++)
        {
            var bc = bulletCols[i];
            if (bc == null) continue;

            for (int j = 0; j < enemyCols.Length; j++)
            {
                var ec = enemyCols[j];
                if (ec == null) continue;

                Physics.IgnoreCollision(bc, ec, true);
            }
        }
    }
}
