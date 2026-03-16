using UnityEngine;

public class MonsterProjectileDamage : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private bool destroyOnHit = true;

    private Transform _owner;
    private bool _didHit;

    public void Init(float damageAmount, Transform owner, float lifeSeconds)
    {
        damage = damageAmount;
        _owner = owner;
        lifetime = Mathf.Max(0.1f, lifeSeconds);
        _didHit = false;

        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        TryApplyDamage(collision.collider);
    }

    private void TryApplyDamage(Collider other)
    {
        if (_didHit || other == null) return;
        if (_owner != null && other.transform.IsChildOf(_owner)) return;

        PlayerVitals playerVitals = other.GetComponentInParent<PlayerVitals>();
        if (playerVitals == null || playerVitals.IsDead) return;

        _didHit = true;
        playerVitals.TakeDamage(damage);

        if (destroyOnHit)
        {
            DestroySelf();
        }
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
