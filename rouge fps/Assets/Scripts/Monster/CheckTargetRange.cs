using UnityEngine;
using UnityEngine.AI;

// 条件节点：检测是否有目标在范围内
public class CheckTargetRange : Node
{
    private readonly Transform _transform;
    private readonly Transform _target;
    private readonly float _sqrRange; // 存储距离的平方，用于比较

    public CheckTargetRange(Transform transform, Transform target, float range)
    {
        _transform = transform;
        _target = target;
        _sqrRange = range * range; // 预计算平方值
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        float sqrDistance = (_transform.position - _target.position).sqrMagnitude;
        return sqrDistance <= _sqrRange ? NodeState.Success : NodeState.Failure;
    }
}

// 动作节点：使用 NavMesh 移动
public class TaskNavMove : Node
{
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;
    private readonly Animator _ani;
    private readonly int _isMovingHash;

    public TaskNavMove(NavMeshAgent agent, Transform target, Animator ani)
    {
        _agent = agent;
        _target = target;
        _ani = ani;
        // 缓存动画参数ID，提升性能
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        // 检查 NavMeshAgent 是否有效
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            return NodeState.Failure;
        }

        // 确保 NavMeshAgent 没有被手动停止
        if (_agent.isStopped)
        {
            _agent.isStopped = false;
        }

        // 持续更新目的地（追踪目标）
        _agent.SetDestination(_target.position);

        Debug.Log(_target.position);


        // 播放移动动画
        if (_ani != null) _ani.SetBool(_isMovingHash, true);

        // 始终返回 Running，保持持续追踪
        return NodeState.Running;
    }
}

// 动作节点：边移动边攻击（攻击范围内的行为）
public class TaskAttackWithMove : Node
{
    private readonly MonsterBase _monster;
    private readonly Animator _ani;
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;

    private float _lastAttackTime;
    private readonly float _attackAnimationDuration = 0.5f; // 攻击动画（僵直）持续时间

    // 缓存动画参数ID
    private readonly int _isMovingHash;

    public TaskAttackWithMove(MonsterBase monster, Animator ani, NavMeshAgent agent, Transform target)
    {
        _monster = monster;
        _ani = ani;
        _agent = agent;
        _target = target;
        _lastAttackTime = -999f;
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return NodeState.Failure;

        // 尝试执行攻击逻辑（内部检查冷却等条件）
        bool justAttacked = _monster.TryAttack();

        if (justAttacked)
        {
            _lastAttackTime = Time.time;
            SetStoppedState(true); // 攻击瞬间停止移动
        }
        else
        {
            // 检查是否处于攻击后摇/僵直阶段
            bool inAttackAnimation = (Time.time - _lastAttackTime) < _attackAnimationDuration;

            if (inAttackAnimation)
            {
                SetStoppedState(true); // 僵直期间保持停止
            }
            else
            {
                // 既没有刚攻击，也不在僵直期 -> 恢复追踪
                SetStoppedState(false);
                _agent.SetDestination(_target.position);
            }
        }

        return NodeState.Running;
    }


    private void SetStoppedState(bool isStopped)
    {
        if (_agent.isStopped != isStopped)
        {
            _agent.isStopped = isStopped;
        }

        if (_ani != null)
        {
            _ani.SetBool(_isMovingHash, !isStopped);
        }
    }
}

// 动作节点：待机
public class TaskIdle : Node
{
    private readonly Animator _ani;
    private readonly int _isMovingHash;

    public TaskIdle(Animator ani)
    {
        _ani = ani;
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_ani != null) _ani.SetBool(_isMovingHash, false);
        return NodeState.Success;
    }
}