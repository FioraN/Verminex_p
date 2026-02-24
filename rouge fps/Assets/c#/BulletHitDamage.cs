using System.Collections.Generic;
using UnityEngine;

public class BulletHitDamage : MonoBehaviour
{
    [Header("Damage (set when spawned)")]
    public float baseDamage = 10f;

    [Tooltip("发射这颗子弹的枪（用于事件/Perk 区分主枪或副枪）。建议在生成时赋值。")]
    public CameraGunChannel source;

    [Header("Destroy")]
    public bool destroyOnHit = true;

    private bool _didHit;

    // 穿透相关（保留原逻辑）
    private bool _penetrationEnabled;
    private float _secondHitMultiplier = 1f;
    private int _enemyHitCount = 0;
    private MonsterHealth _firstEnemyHit;

    private void Awake()
    {
        // 如果生成时没有调用 Init，这里也能保证贯穿配置被读取
        if (Perk_PenetrateOneTarget.TryGetConfig(source, out var cfg))
        {
            _penetrationEnabled = true;
            _secondHitMultiplier = Mathf.Clamp01(cfg.secondHitDamageMultiplier);
        }
        else
        {
            _penetrationEnabled = false;
            _secondHitMultiplier = 1f;
        }
    }
    public void Init(float damage, CameraGunChannel src)
    {
        baseDamage = damage;
        source = src;

        // 读取穿透配置（如果有）
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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (_didHit) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        MonsterHealth mh = other.GetComponentInParent<MonsterHealth>();
        bool isEnemy = mh != null;

        if (_penetrationEnabled && !isEnemy)
        {
            _didHit = true;
            Destroy(gameObject);
            return;
        }

        if (_penetrationEnabled && isEnemy)
        {
            if (_enemyHitCount >= 1 && mh == _firstEnemyHit)
                return;
        }

        var armorPayload2 = GetComponentInChildren<BulletArmorPayload>(true);
        var statusPayload2 = GetComponentInChildren<BulletStatusPayload>(true);

        float damageToApply = baseDamage;

        if (_penetrationEnabled && isEnemy && _enemyHitCount == 1 && mh != _firstEnemyHit)
            damageToApply = baseDamage * _secondHitMultiplier;

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
            armorPayload: armorPayload2,
            statusPayload: statusPayload2,
            showHitUI: true
        );

        if (!applied) return;

        if (!_penetrationEnabled)
        {
            _didHit = true;
            if (destroyOnHit) Destroy(gameObject);
            return;
        }

        if (isEnemy)
        {
            if (_enemyHitCount == 0)
            {
                _enemyHitCount = 1;
                _firstEnemyHit = mh;

                IgnoreAllEnemyColliders(mh);
                return;
            }

            _enemyHitCount = 2;
            _didHit = true;
            Destroy(gameObject);
            return;
        }

        _didHit = true;
        Destroy(gameObject);
    }

    private void IgnoreAllEnemyColliders(MonsterHealth mh)
    {
        if (mh == null) return;

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