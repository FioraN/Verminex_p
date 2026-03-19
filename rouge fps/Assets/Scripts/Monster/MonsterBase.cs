using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterHealth))]
public class MonsterBase : MonoBehaviour
{
    public bool isCanPatrol = true;

    [Header("Base Stats")]
    public MonsterType type;

    [Header("Movement")]
    public float speed = 5f;

    [Header("Attack")]
    public float attack = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Perception")]
    [Tooltip("Detection range before the monster is aggroed.")]
    public float viewRange = 8f;

    [Range(0, 360)]
    public float viewAngle = 120f;

    [Tooltip("Max chase distance after the monster is aggroed.")]
    public float chaseRange = 15f;

    [Header("Hit Color Flash")]
    [Min(0f)] public float hitFlashDuration = 0.08f;
    public Color hitFlashColor = Color.white;
    public SpriteRenderer hitFlashSpriteRenderer;

    [HideInInspector] public bool isHurt = false;
    [HideInInspector] public bool hasAggro = false;
    [HideInInspector] public NavMeshAgent agent;

    protected bool isDead = false;
    protected MonsterHealth health;
    protected float lastAttackTime;
    [HideInInspector] public Transform playerTransform;
    protected Node rootNode;
    private Coroutine _lockDeathAnimationRoutine;
    private Coroutine _hitFlashRoutine;
    private Color _hitFlashOriginalColor = Color.white;
    private bool _hasHitFlashOriginalColor;

    public bool isDie = false;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<MonsterHealth>();

        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        if (health != null)
        {
            isDead = health.IsDead;
        }
        else
        {
            Debug.LogError($"{name} requires MonsterHealth on the same GameObject.", this);
        }
    }

    protected virtual void OnEnable()
    {
        if (health == null)
            health = GetComponent<MonsterHealth>();

        if (health != null)
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }
    }

    protected virtual void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        RestoreHitFlashColor();
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (agent != null)
        {
            agent.stoppingDistance = attackRange * 0.8f;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }
            }
        }

        SetupBehaviorTree();
    }

    protected virtual void Update()
    {
        if (isDead) return;

        CheckAggroState();

        if (rootNode != null)
        {
            rootNode.Evaluate();
        }

        if (isDie)
        {
            // Keep the corpse facing the player.
            if (playerTransform != null)
            {
                Vector3 lookPos = playerTransform.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }
        }
    }

    protected virtual void CheckAggroState()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (hasAggro)
        {
            if (distanceToPlayer > chaseRange)
            {
                hasAggro = false;
                Debug.Log($"{name} lost target. Returning to patrol.");
                OnLostTarget();
            }
        }
        else if (distanceToPlayer <= viewRange)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle <= viewAngle * 0.5f)
            {
                hasAggro = true;
            }
        }
    }

    protected virtual void OnLostTarget()
    {
        SetAttackReadyVisual(false);
        isHurt = false;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    protected virtual void SetupBehaviorTree() { }

    public virtual void SetAttackReadyVisual(bool active) { }

    protected virtual void HandleDamaged(DamageInfo info)
    {
        if (isDead) return;

        PlayHitWhiteFlash();
        SetAttackReadyVisual(false);
        isHurt = true;
        hasAggro = true;
    }

    protected virtual void HandleDied(DamageInfo info)
    {
        Die();
    }

    public bool TryAttack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            if (playerTransform != null)
            {
                Vector3 lookPos = playerTransform.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            if (!TryPerformAttack())
                return false;

            lastAttackTime = Time.time;
            return true;
        }

        return false;
    }

    protected virtual bool TryPerformAttack()
    {
        PerformAttack();
        return true;
    }

    protected virtual void PerformAttack() { }

    // Legacy wrapper kept for compatibility with old callers.
    // New damage flow should go through MonsterHealth / DamageResolver directly.
    public virtual void TakeDamage(float amount)
    {
        if (health == null || isDead || amount <= 0f) return;

        health.TakeDamage(amount);
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        SetAttackReadyVisual(false);
        IgnoreCollisionWithPlayerOnDeath();

        if (agent != null)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Animator ani = GetComponent<Animator>();
        if (ani != null)
        {
            ani.ResetTrigger("Attack");
            ani.ResetTrigger("Hit");
            ani.SetBool("IsMoving", false);
            ani.speed = 1f;
            ani.Play("Die", 0, 0f);
            ani.Update(0f);

            if (_lockDeathAnimationRoutine != null)
                StopCoroutine(_lockDeathAnimationRoutine);

            _lockDeathAnimationRoutine = StartCoroutine(LockDeathAnimationOnLastFrame(ani));
            isDie = true;
        }
        else
        {
            // Destroy(gameObject);
        }
    }

    public virtual void OnDeathAnimationEnd()
    {
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator LockDeathAnimationOnLastFrame(Animator ani)
    {
        if (ani == null)
            yield break;

        const int layer = 0;

        while (ani != null)
        {
            var state = ani.GetCurrentAnimatorStateInfo(layer);

            if (!state.IsName("Die"))
            {
                ani.Play("Die", layer, 0f);
                ani.Update(0f);
                yield return null;
                continue;
            }

            if (state.normalizedTime >= 0.99f)
                break;

            yield return null;
        }

        if (ani != null)
        {
            ani.Play("Die", layer, 0.999f);
            ani.Update(0f);
            ani.speed = 0f;
        }
    }

    private void IgnoreCollisionWithPlayerOnDeath()
    {
        if (playerTransform == null)
            return;

        var monsterColliders = GetComponentsInChildren<Collider>(true);
        var playerColliders = playerTransform.GetComponentsInChildren<Collider>(true);

        if (monsterColliders == null || playerColliders == null)
            return;

        for (int i = 0; i < monsterColliders.Length; i++)
        {
            var monsterCollider = monsterColliders[i];
            if (monsterCollider == null)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                var playerCollider = playerColliders[j];
                if (playerCollider == null)
                    continue;

                Physics.IgnoreCollision(monsterCollider, playerCollider, true);
            }
        }
    }

    private void PlayHitWhiteFlash()
    {
        SpriteRenderer targetSpriteRenderer = ResolveHitFlashSpriteRenderer();
        if (targetSpriteRenderer == null)
            return;

        if (!_hasHitFlashOriginalColor)
        {
            _hitFlashOriginalColor = targetSpriteRenderer.color;
            _hasHitFlashOriginalColor = true;
        }

        if (_hitFlashRoutine != null)
            StopCoroutine(_hitFlashRoutine);

        targetSpriteRenderer.color = hitFlashColor;
        _hitFlashRoutine = StartCoroutine(HitWhiteFlashRoutine(targetSpriteRenderer));
    }

    private System.Collections.IEnumerator HitWhiteFlashRoutine(SpriteRenderer targetSpriteRenderer)
    {
        float elapsed = 0f;
        while (elapsed < hitFlashDuration)
        {
            if (targetSpriteRenderer == null)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetSpriteRenderer != null)
            targetSpriteRenderer.color = _hitFlashOriginalColor;

        _hitFlashRoutine = null;
        _hasHitFlashOriginalColor = false;
    }

    private SpriteRenderer ResolveHitFlashSpriteRenderer()
    {
        if (hitFlashSpriteRenderer != null)
            return hitFlashSpriteRenderer;

        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (renderers[i].gameObject.name == "picture")
            {
                hitFlashSpriteRenderer = renderers[i];
                return hitFlashSpriteRenderer;
            }
        }

        if (renderers.Length > 0)
            hitFlashSpriteRenderer = renderers[0];

        return hitFlashSpriteRenderer;
    }

    private void RestoreHitFlashColor()
    {
        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
        }

        if (hitFlashSpriteRenderer != null && _hasHitFlashOriginalColor)
            hitFlashSpriteRenderer.color = _hitFlashOriginalColor;

        _hasHitFlashOriginalColor = false;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, viewRange);

        Vector3 leftDir = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * viewRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * viewRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

public enum MonsterType
{
    Melee,
    Ranged
}
