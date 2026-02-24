using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Monster1 : MonsterBase
{
    public Animator ani;
    public float speed=3;

    protected override void Start()
    {
        // 设置基本数值
        type = MonsterType.Melee;

        // 注意：使用 NavMeshAgent 的 speed 替代自定义的 moveSpeed
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = speed;
        }
        else
        {
            Debug.LogWarning("NavMeshAgent component not found on Monster1.");
        }
        base.Start(); // 调用基类 Start 以初始化 Player 和 BT
    }

    protected override void SetupBehaviorTree()
    {
        // --- 重新设计的行为树逻辑 ---

        // 1. 检测是否在侦测范围内
        Node checkDetectRange = new CheckTargetRange(transform, playerTransform, detectionRange);

        // 2. 在侦测范围内的行为 (Parallel: 同时追踪和攻击)
        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);

        // 3. 追击和攻击的组合逻辑
        // 优先尝试攻击，如果不在攻击范围则只移动
        Node moveAction = new TaskNavMove(agent, playerTransform, ani);
        Selector combatBehavior = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }), // 在攻击范围内：边移动边攻击
            moveAction // 不在攻击范围：只移动
        });

        // 4. 完整的追踪序列
        Sequence chaseSeq = new Sequence(new List<Node> { checkDetectRange, combatBehavior });

        // 5. 待机分支
        Node idleAction = new TaskIdle(ani);

        // --- 根节点 (Selector) ---
        // 优先追踪/战斗 -> 否则待机
        rootNode = new Selector(new List<Node> { chaseSeq, idleAction });
    }

    protected override void PerformAttack()
    {
        Debug.Log("Monster1 performs specific attack");
        if (ani != null)
        {
            ani.SetTrigger("Attack");
        }

        // 可以在这里添加具体的伤害判定逻辑
        // Collider[] hits = Physics.OverlapSphere(...)
    }
}