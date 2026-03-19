using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Condition node: succeeds when the target is inside the specified range.
public class CheckTargetRange : Node
{
    private readonly Transform _transform;
    private readonly Transform _target;
    private readonly float _sqrRange; // Store the squared range for cheaper distance checks.

    public CheckTargetRange(Transform transform, Transform target, float range)
    {
        _transform = transform;
        _target = target;
        _sqrRange = range * range; // Precompute the squared range once.
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        float sqrDistance = (_transform.position - _target.position).sqrMagnitude;
        return sqrDistance <= _sqrRange ? NodeState.Success : NodeState.Failure;
    }
}

// Condition node: succeeds when the target is inside the view cone.
public class CheckTargetSector : Node
{
    private readonly Transform _transform;
    private readonly Transform _target;
    private readonly float _sqrRange; // Squared view range.
    private readonly float _halfAngle; // Half of the total view angle.

    public CheckTargetSector(Transform transform, Transform target, float viewRange, float viewAngle)
    {
        _transform = transform;
        _target = target;
        _sqrRange = viewRange * viewRange;
        _halfAngle = viewAngle * 0.5f;
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        Vector3 dirToTarget = _target.position - _transform.position;

        // 1. Check distance first. SqrMagnitude is cheaper than Distance.
        if (dirToTarget.sqrMagnitude > _sqrRange)
            return NodeState.Failure;

        // 2. Then check the angle between forward and the target direction.
        // Vector3.Angle returns a value in the 0 to 180 degree range.
        float angle = Vector3.Angle(_transform.forward, dirToTarget);

        if (angle <= _halfAngle)
        {
            return NodeState.Success;
        }

        return NodeState.Failure;
    }
}

// Action node: move toward the target with NavMesh.
public class TaskNavMove : Node
{
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;
    private readonly Animator _ani;
    private readonly MonsterBase _monster;
    private readonly int _isMovingHash;

    public TaskNavMove(NavMeshAgent agent, Transform target, Animator ani, MonsterBase monster = null)
    {
        _agent = agent;
        _target = target;
        _ani = ani;
        _monster = monster;
        // Cache the animator hash to avoid repeated string lookups.
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        // Abort if the NavMeshAgent is not ready to move.
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            return NodeState.Failure;
        }

        // Make sure movement is resumed before assigning a destination.
        if (_agent.isStopped)
        {
            _agent.isStopped = false;
        }

        // Continuously update the destination to track the target.
        _agent.SetDestination(_target.position);

        Debug.Log(_target.position);

        // Play the movement animation while chasing.
        if (_ani != null) _ani.SetBool(_isMovingHash, true);

        // Keep returning Running while the target is being chased.
        return NodeState.Running;
    }
}

// Action node: stop near the target, show ready state, and trigger attacks.
public class TaskAttackWithMove : Node
{
    private readonly MonsterBase _monster;
    private readonly Animator _ani;
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;

    private float _lastAttackTime;
    // Duration used to keep the monster locked during the attack animation.
    private readonly float _attackAnimationDuration = 1.2f;

    // Cache the animator hash for repeated use.
    private readonly int _isMovingHash;

    public TaskAttackWithMove(MonsterBase monster, Animator ani, NavMeshAgent agent, Transform target)
    {
        _monster = monster;
        _ani = ani;
        _agent = agent;
        _target = target;
        _lastAttackTime = -9999f; // Initialize to a very old time so the first attack is allowed immediately.
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null)
        {
            _monster?.SetAttackReadyVisual(false);
            return NodeState.Failure;
        }

        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            _monster?.SetAttackReadyVisual(false);
            return NodeState.Failure;
        }

        // Stay in the attack lockout window while the attack animation is still playing.
        bool inAttackAnimation = (Time.time - _lastAttackTime) < _attackAnimationDuration;

        if (inAttackAnimation)
        {
            // Freeze movement while the attack animation is still active.
            _monster.SetAttackReadyVisual(false);
            SetStoppedState(true);
            return NodeState.Running;
        }

        // Try to attack. The monster handles cooldown, delay, and range checks internally.
        bool justAttacked = _monster.TryAttack();

        if (justAttacked)
        {
            _lastAttackTime = Time.time;
            // Stop immediately after the attack is triggered.
            _monster.SetAttackReadyVisual(false);
            SetStoppedState(true);
            // Force the monster to face the target during the attack.
            _monster.transform.LookAt(new Vector3(_target.position.x, _monster.transform.position.y, _target.position.z));
        }
        else
        {
            // Not attacking right now, so keep chasing and show the ready visual.
            _monster.SetAttackReadyVisual(true);
            SetStoppedState(false);
            _agent.SetDestination(_target.position);
        }

        return NodeState.Running;
    }

    private void SetStoppedState(bool isStopped)
    {
        if (_agent.isStopped != isStopped)
        {
            _agent.isStopped = isStopped;
            if (isStopped)
            {
                _agent.velocity = Vector3.zero; // Clear velocity to avoid sliding.
                _agent.ResetPath(); // Clear the current path to force a full stop.
            }
        }

        if (_ani != null)
        {
            _ani.SetBool(_isMovingHash, !isStopped);
        }
    }
}

// Action node: idle without moving.
public class TaskIdle : Node
{
    private readonly Animator _ani;
    private readonly MonsterBase _monster;
    private readonly int _isMovingHash;

    public TaskIdle(Animator ani, MonsterBase monster = null)
    {
        _ani = ani;
        _monster = monster;
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        _monster?.SetAttackReadyVisual(false);
        if (_ani != null) _ani.SetBool(_isMovingHash, false);
        return NodeState.Success;
    }
}

// Action node: patrol between waypoints.
public class TaskPatrol : Node
{
    private Transform _transform;
    private List<Transform> _waypoints;
    private NavMeshAgent _agent;
    private Animator _ani;
    private MonsterBase _monster;
    private int _currentWaypointIndex = 0;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;

    // Prevent SetDestination from being called every frame when the destination is unchanged.
    private bool _destinationSet = false;

    private Transform _forcedTarget;

    public TaskPatrol(Transform transform, List<Transform> waypoints, NavMeshAgent agent, Animator ani, MonsterBase monster = null)
    {
        _transform = transform;
        _waypoints = waypoints;
        _agent = agent;
        _ani = ani;
        _monster = monster;
    }

    public void SetNextPatrolPoint(Transform point)
    {
        _forcedTarget = point;
        _isWaiting = false;
        _destinationSet = false; // Force the patrol destination to refresh.
    }

    public override NodeState Evaluate()
    {
        _monster?.SetAttackReadyVisual(false);
        if (_waypoints == null || _waypoints.Count == 0) return NodeState.Failure;

        // 1. Consume a forced patrol point, usually after returning from combat.
        if (_forcedTarget != null)
        {
            int index = _waypoints.IndexOf(_forcedTarget);
            if (index != -1) _currentWaypointIndex = index;

            _forcedTarget = null;
            _isWaiting = false;
            _waitTimer = 0f;
            _destinationSet = false; // Force a new path calculation.
        }

        // 2. Wait at the current waypoint.
        if (_isWaiting)
        {
            if (_ani) _ani.SetBool("IsMoving", false);
            if (_agent.isActiveAndEnabled) _agent.isStopped = true; // Keep the agent stopped while waiting.

            _waitTimer += Time.deltaTime;
            if (_waitTimer > 1.5f)
            {
                // Waiting finished, move on to the next waypoint.
                _isWaiting = false;
                _waitTimer = 0f;
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
                _destinationSet = false; // Mark the new waypoint as needing a destination update.
            }
            return NodeState.Running;
        }

        // 3. Set the next patrol destination once.
        if (!_destinationSet)
        {
            Transform wp = _waypoints[_currentWaypointIndex];
            if (wp != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.SetDestination(wp.position);
                _agent.isStopped = false;
                if (_ani) _ani.SetBool("IsMoving", true);
                _destinationSet = true; // Avoid resetting the same destination every frame.
            }
        }

        // 4. Detect arrival. pathPending prevents false positives right after SetDestination.
        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            bool reachedDestination =
                !_agent.pathPending &&
                _agent.pathStatus == NavMeshPathStatus.PathComplete &&
                _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, 0.05f) &&
                _agent.velocity.sqrMagnitude <= 0.01f;

            if (reachedDestination)
            {
                _isWaiting = true;
                _waitTimer = 0f;
                _destinationSet = false;

                _agent.isStopped = true;
                if (_ani) _ani.SetBool("IsMoving", false);
            }
        }

        return NodeState.Running;
    }
}

// Action node: play the hurt reaction for a short duration.
public class TaskHurt : Node
{
    private MonsterBase _monster;
    private Animator _ani;
    private float _duration = 0.5f; // Hurt stun duration.
    private float _timer = 0f;
    private bool _started = false;

    public TaskHurt(MonsterBase monster, Animator ani)
    {
        _monster = monster;
        _ani = ani;
    }

    public override NodeState Evaluate()
    {
        if (!_monster.isHurt)
        {
            _started = false;
            return NodeState.Failure;
        }

        _monster.SetAttackReadyVisual(false);

        if (!_started)
        {
            _started = true;
            _timer = 0f;
            if (_ani) _ani.SetTrigger("Hit"); // Trigger the hurt animation once.
            // Stop movement while the hurt reaction is active.
            if (_monster.agent != null && _monster.agent.isOnNavMesh)
                _monster.agent.isStopped = true;
        }

        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            _monster.isHurt = false; // Clear the hurt state after the stun ends.
            _monster.hasAggro = true; // Being hit forces the monster back into combat.
            _started = false;
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}

// Action node: idle for a fixed amount of time.
public class TaskTimedIdle : Node
{
    private Animator _ani;
    private MonsterBase _monster;
    private float _duration;
    private float _timer;

    public TaskTimedIdle(Animator ani, float duration, MonsterBase monster = null)
    {
        _ani = ani;
        _duration = duration;
        _timer = 0f;
        _monster = monster;
    }

    public override NodeState Evaluate()
    {
        _monster?.SetAttackReadyVisual(false);
        if (_ani) _ani.SetBool("IsMoving", false);

        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            _timer = 0f; // Reset the timer so the node can be reused next time.
            return NodeState.Success; // Timed idle finished.
        }
        return NodeState.Running; // Still idling.
    }
}

// Condition node: succeeds when the monster already has aggro.
public class CheckAggro : Node
{
    private MonsterBase _monster;
    public CheckAggro(MonsterBase monster) { _monster = monster; }
    public override NodeState Evaluate()
    {
        return _monster.hasAggro ? NodeState.Success : NodeState.Failure;
    }
}
