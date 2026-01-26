using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public struct Config
    {
        public CameraGunChannel source;

        public float speed;
        public float gravity;
        public float lifetime;

        public float maxRange;
        public float baseDamage;
        public AnimationCurve falloff;

        public LayerMask hitMask;
    }

    private Config _cfg;
    private Vector3 _velocity;
    private Vector3 _startPos;
    private float _life;

    public void Init(Config cfg)
    {
        _cfg = cfg;
        _velocity = transform.forward * Mathf.Max(0.01f, cfg.speed);
        _startPos = transform.position;
        _life = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _life += dt;

        if (_life >= _cfg.lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_cfg.gravity > 0f)
            _velocity += Vector3.down * _cfg.gravity * dt;

        Vector3 step = _velocity * dt;
        float dist = step.magnitude;

        if (dist > 0.0001f)
        {
            if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, dist, _cfg.hitMask, QueryTriggerInteraction.Ignore))
            {
                float traveled = Vector3.Distance(_startPos, hit.point);
                float t01 = _cfg.maxRange <= 0.0001f ? 1f : Mathf.Clamp01(traveled / _cfg.maxRange);

                float mult = 1f;
                if (_cfg.falloff != null)
                    mult = Mathf.Max(0f, _cfg.falloff.Evaluate(t01));

                float damageBeforeHitbox = _cfg.baseDamage * mult;

                var armorPayload = GetComponentInChildren<BulletArmorPayload>(true);
                var statusPayload = GetComponentInChildren<BulletStatusPayload>(true);

                var info = new DamageInfo
                {
                    source = _cfg.source,
                    damage = damageBeforeHitbox,
                    isHeadshot = false,
                    hitPoint = hit.point,
                    hitCollider = hit.collider,
                    flags = DamageFlags.None
                };

                DamageResolver.ApplyHit(
                    baseInfo: info,
                    hitCol: hit.collider,
                    hitPoint: hit.point,
                    source: _cfg.source,
                    armorPayload: armorPayload,
                    statusPayload: statusPayload,
                    showHitUI: true
                );

                Destroy(gameObject);
                return;
            }
        }

        transform.position += step;

        float totalTraveled = Vector3.Distance(_startPos, transform.position);
        if (_cfg.maxRange > 0f && totalTraveled >= _cfg.maxRange)
        {
            Destroy(gameObject);
        }
    }
}
