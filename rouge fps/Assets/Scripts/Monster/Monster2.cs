using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class Monster2 : MonsterBase
{
    private Animator ani;
    [Header("Visual Sprites")]
    public SpriteRenderer pictureRenderer;
    public Sprite deathSprite;

    private Sprite _defaultSprite;

    [Header("Ranged Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;

    [Header("Projectile Launch")]
    [Tooltip("Multiplier applied when launching the projectile so it feels more like a fast shot.")]
    [Min(0.1f)] public float projectileLaunchSpeedMultiplier = 3f;

    [Tooltip("Disable gravity on the spawned projectile so it flies straight instead of drooping immediately.")]
    public bool disableProjectileGravityOnLaunch = true;

    [Tooltip("Use continuous collision detection on the spawned projectile to reduce fast-shot tunneling.")]
    public bool useContinuousCollisionOnLaunch = true;

    private TaskPatrol patrolTask;
    private List<Transform> patrolPoints;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Ranged;
        ResolvePictureRenderer();

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = speed;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (PatrolPointManager.Instance != null&&isCanPatrol)
            patrolPoints = PatrolPointManager.Instance.GetAllPatrolPoints().ToList();
        else
            patrolPoints = new List<Transform>();

        base.Start();
    }

    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            Transform nearest = GetNearestPatrolPoint();
            if (nearest != null && patrolTask != null)
            {
                patrolTask.SetNextPatrolPoint(nearest);
            }
        }
    }

    private Transform GetNearestPatrolPoint()
    {
        Transform nearest = null;
        float minDst = float.MaxValue;
        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            float d = Vector3.Distance(transform.position, p.position);
            if (d < minDst)
            {
                minDst = d;
                nearest = p;
            }
        }
        return nearest;
    }

    protected override void SetupBehaviorTree()
    {
        Node hurtNode = new TaskHurt(this, ani);
        Node checkAggro = new CheckAggro(this);
        Node checkViewSector = new CheckTargetSector(transform, playerTransform, viewRange, viewAngle);

        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewSector });
        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);
        Node chaseAction = new TaskNavMove(agent, playerTransform, ani, this);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani, this);
        Node idle5s = new TaskTimedIdle(ani, 5.0f, this);

        Sequence patrolIdleSeq = new Sequence(new List<Node>
        {
            patrolTask,
            idle5s
        });

        rootNode = new Selector(new List<Node>
        {
            hurtNode,
            combatSequence,
            patrolIdleSeq
        });
    }

    protected override void PerformAttack()
    {
        if (playerTransform == null) return;
        if (ani != null) ani.SetTrigger("Attack");

        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning("Monster2 missing projectilePrefab or firePoint!");
            return;
        }

        Vector3 targetPoint = GetPlayerBodyCenter();
        Vector3 toTarget = targetPoint - firePoint.position;
        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : firePoint.forward;
        float launchSpeed = Mathf.Max(0.01f, projectileSpeed * Mathf.Max(0.1f, projectileLaunchSpeedMultiplier));

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        proj.SetActive(true);
        IgnoreOwnerCollision(proj);

        MonsterProjectileDamage projectileDamage = proj.GetComponent<MonsterProjectileDamage>();
        if (projectileDamage == null)
        {
            projectileDamage = proj.AddComponent<MonsterProjectileDamage>();
        }
        projectileDamage.Init(attack, transform, projectileLifetime);

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (disableProjectileGravityOnLaunch)
                rb.useGravity = false;

            if (useContinuousCollisionOnLaunch)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            rb.velocity = direction * launchSpeed;
        }
    }

    private void IgnoreOwnerCollision(GameObject projectile)
    {
        if (projectile == null)
            return;

        var projectileColliders = projectile.GetComponentsInChildren<Collider>(true);
        var ownerColliders = GetComponentsInChildren<Collider>(true);
        if (projectileColliders == null || ownerColliders == null)
            return;

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            var projectileCollider = projectileColliders[i];
            if (projectileCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                var ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    private Vector3 GetPlayerBodyCenter()
    {
        if (playerTransform == null)
            return transform.position;

        CharacterController characterController = playerTransform.GetComponentInParent<CharacterController>();
        if (characterController != null)
            return characterController.bounds.center;

        CapsuleCollider capsuleCollider = playerTransform.GetComponentInParent<CapsuleCollider>();
        if (capsuleCollider != null)
            return capsuleCollider.bounds.center;

        Collider playerCollider = playerTransform.GetComponentInParent<Collider>();
        if (playerCollider != null)
            return playerCollider.bounds.center;

        return playerTransform.position;
    }

    private void LateUpdate()
    {
        if (!ResolvePictureRenderer())
            return;

        if (isDead && deathSprite != null)
        {
            pictureRenderer.sprite = deathSprite;
            return;
        }

        if (_defaultSprite != null)
            pictureRenderer.sprite = _defaultSprite;
    }

    private bool ResolvePictureRenderer()
    {
        if (pictureRenderer == null)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                if (renderers[i].gameObject.name == "picture")
                {
                    pictureRenderer = renderers[i];
                    break;
                }
            }

            if (pictureRenderer == null && renderers.Length > 0)
                pictureRenderer = renderers[0];
        }

        if (pictureRenderer == null)
            return false;

        if (_defaultSprite == null)
            _defaultSprite = pictureRenderer.sprite;

        return true;
    }
}
