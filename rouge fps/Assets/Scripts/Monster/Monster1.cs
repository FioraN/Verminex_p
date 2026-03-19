using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Melee monster that chases the player within range and returns to patrol after losing aggro.
public class Monster1 : MonsterBase
{
    private Animator ani;
    private List<Transform> patrolPoints;

    [Header("Attack Audio")]
    public AudioSource attackAudioSource;
    public AudioClip[] attackClips;
    [Min(0f)] public float attackVolume = 1f;
    public Vector2 attackPitchRange = new Vector2(1f, 1f);

    [Header("Attack Timing")]
    [Min(0f)] public float readyBeforeAttackDelay = 0.15f;

    [Header("Visual Sprites")]
    public SpriteRenderer pictureRenderer;
    public Sprite readySprite;
    public Sprite deathSprite;

    private Sprite _defaultSprite;
    private bool _showReadySprite;
    private bool _wasShowingReadySprite;
    private float _readyShownAt = -9999f;

    // Cached so the patrol task can resume from the nearest patrol point after combat.
    private TaskPatrol patrolTask;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Melee;
        ResolvePictureRenderer();

        if (attackAudioSource == null)
            attackAudioSource = GetComponent<AudioSource>();

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = speed;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (PatrolPointManager.Instance != null && isCanPatrol)
            patrolPoints = PatrolPointManager.Instance.GetAllPatrolPoints().ToList();
        else
            patrolPoints = new List<Transform>();

        base.Start();
    }

    // Return to the nearest patrol point after the monster loses the target.
    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            Transform nearest = GetNearestPatrolPoint();
            if (nearest != null && patrolTask != null)
            {
                // Force patrol to restart from the closest waypoint.
                patrolTask.SetNextPatrolPoint(nearest);
            }
        }
    }

    // Find the patrol point closest to the monster's current position.
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

    // Build the melee monster behavior tree.
    protected override void SetupBehaviorTree()
    {
        // 1. Hurt reaction
        Node hurtNode = new TaskHurt(this, ani);

        // 2. Detection: either already aggroed or the player is inside the vision cone.
        Node checkAggro = new CheckAggro(this);
        // If the player is within view range and angle, treat them as spotted.
        Node checkViewSector = new CheckTargetSector(transform, playerTransform, viewRange, viewAngle);

        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewSector });

        // Combat branch
        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);
        Node chaseAction = new TaskNavMove(agent, playerTransform, ani, this);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        // 3. Patrol branch
        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani, this);
        Node idle5s = new TaskTimedIdle(ani, 5.0f, this);

        // Patrol loop: move to a point, idle, then continue to the next one.
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

    // Apply melee damage once the monster is close enough to the player.
    protected override void PerformAttack()
    {
        // Validate the target and fetch the player's health component.
        if (playerTransform == null) return;

        PlayerVitals playerVitals = playerTransform.GetComponent<PlayerVitals>();
        if (playerVitals == null)
            playerVitals = playerTransform.GetComponentInParent<PlayerVitals>();

        if (playerVitals == null || playerVitals.IsDead) return;

        if (ani != null) ani.SetTrigger("Attack");

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= attackRange + 0.25f)
        {
            playerVitals.TakeDamage(attack);
        }
    }

    public override void SetAttackReadyVisual(bool active)
    {
        bool shouldShowReady = active && !isDead;
        if (shouldShowReady && !_showReadySprite)
            _readyShownAt = Time.time;

        _showReadySprite = shouldShowReady;
    }

    protected override bool TryPerformAttack()
    {
        if (!_showReadySprite)
            return false;

        float requiredDelay = Mathf.Max(0f, readyBeforeAttackDelay);
        if (Time.time - _readyShownAt < requiredDelay)
            return false;

        PerformAttack();
        return true;
    }

    private void LateUpdate()
    {
        if (!ResolvePictureRenderer())
            return;

        if (isDead && deathSprite != null)
        {
            _wasShowingReadySprite = false;
            pictureRenderer.sprite = deathSprite;
            return;
        }

        bool isShowingReadySprite = _showReadySprite && readySprite != null;
        if (isShowingReadySprite && !_wasShowingReadySprite)
            PlayAttackSound();

        _wasShowingReadySprite = isShowingReadySprite;

        if (isShowingReadySprite)
        {
            pictureRenderer.sprite = readySprite;
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

    private void PlayAttackSound()
    {
        if (attackAudioSource == null)
            return;

        AudioClip clip = ChooseRandomClip(attackClips);
        if (clip == null)
            return;

        float originalPitch = attackAudioSource.pitch;
        attackAudioSource.pitch = Random.Range(
            Mathf.Min(attackPitchRange.x, attackPitchRange.y),
            Mathf.Max(attackPitchRange.x, attackPitchRange.y));
        attackAudioSource.PlayOneShot(clip, Mathf.Max(0f, attackVolume));
        attackAudioSource.pitch = originalPitch;
    }

    private static AudioClip ChooseRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (pick == 0)
                return clips[i];

            pick--;
        }

        return null;
    }
}
