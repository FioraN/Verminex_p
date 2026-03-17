using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class Monster2 : MonsterBase
{
    private Animator ani;

    [Header("Ranged Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;

    private TaskPatrol patrolTask;
    private List<Transform> patrolPoints;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Ranged;

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
        Node chaseAction = new TaskNavMove(agent, playerTransform, ani);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani);
        Node idle5s = new TaskTimedIdle(ani, 5.0f);

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

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.SetActive(true);

        Vector3 targetPoint = playerTransform.position + Vector3.up * 1.2f;
        proj.transform.LookAt(targetPoint);

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
            Vector3 direction = (targetPoint - firePoint.position).normalized;
            rb.velocity = direction * projectileSpeed;
        }
    }
}
