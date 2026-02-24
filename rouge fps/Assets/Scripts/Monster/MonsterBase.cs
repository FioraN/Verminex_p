using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterBase : MonoBehaviour
{
    [Header("Base Stats")]
    public MonsterType type;
    public float hp = 100f;

    [Header("跟踪范围")]
    public float detectionRange = 10f;
    [Header("攻击范围")]
    public float attackRange = 2f;
    [Header("攻击冷却时间")]
    public float attackCooldown = 1.5f;

    [Header("Components")]
    public NavMeshAgent agent;

    protected float lastAttackTime;
    public Transform playerTransform;
    protected Node rootNode;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            
            agent.updateRotation = true;
            agent.updatePosition = true;
        }
        else
        {
            Debug.LogError($"NavMeshAgent not found on {gameObject.name}!");
        }
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"Player not found for {gameObject.name}!");
        }

        // 这里的配置会覆盖 NavMeshAgent 的默认值
        if (agent != null)
        {
            agent.stoppingDistance = attackRange * 0.3f;

            // 检查是否在 NavMesh 上
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"{gameObject.name} is not on NavMesh!");
            }
        }

        //设置怪物行为树
        SetupBehaviorTree();
    }

    protected virtual void Update()
    {
        if (rootNode != null)
        {
            rootNode.Evaluate();//执行怪物行为逻辑
        }

    }

    protected virtual void SetupBehaviorTree()
    {
        // 子类重写
    }

    // 尝试攻击，如果冷却完毕则执行并返回 true
    public bool TryAttack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
           

            // 面向目标
            if (playerTransform != null)
            {
                Vector3 lookPos = playerTransform.position;
                lookPos.y = transform.position.y; // 保持水平朝向
                transform.LookAt(lookPos);
            }

            PerformAttack();
            lastAttackTime = Time.time;
            return true;
        }
        return false;
    }

    protected virtual void PerformAttack()
    {
        Debug.Log("Monster Base Attack");
        // 这里只是单纯的数据/逻辑层攻击
        // 动画和特效在子类或者 BehaviorTree 节点后续处理
    }



    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

// 怪物类型枚举
public enum MonsterType 
{
    Melee, Ranged, Kamikaze
}