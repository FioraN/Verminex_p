using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
// 杩戞垬鏀诲嚮鎬墿锛氫細鍦ㄤ竴瀹氳寖鍥村唴杩藉嚮鐜╁锛岃劚鎴樺悗鍥炴渶杩戠殑宸￠€荤偣
public class Monster1 : MonsterBase
{
    private Animator ani;
    private List<Transform> patrolPoints;
    [Header("Visual Sprites")]
    public SpriteRenderer pictureRenderer;
    public Sprite readySprite;
    public Sprite deathSprite;

    private Sprite _defaultSprite;
    private bool _showReadySprite;

    // 鎴戜滑闇€瑕佸紩鐢ㄨ繖涓猅ask锛屼互渚垮湪鑴辨垬鏃堕噸缃畠鐨勭姸鎬佹垨鐩爣
    private TaskPatrol patrolTask;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Melee;
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


    //鑴辨垬
    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        // 鏍稿績閫昏緫锛氳劚鎴樺悗锛屾壘鍒版渶杩戠殑宸￠€荤偣
        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            Transform nearest = GetNearestPatrolPoint();
            if (nearest != null && patrolTask != null)
            {
                // 鍛婅瘔宸￠€讳换鍔★細涓嬫寮€濮嬪贰閫绘椂锛屽厛鍘昏繖涓渶杩戠殑鐐?
                patrolTask.SetNextPatrolPoint(nearest);
            }
        }
    }

    //鑾峰彇涓磋繎宸￠€荤偣
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


    //璁剧疆琛屼负鏍?
    protected override void SetupBehaviorTree()
    {
        // 1. 鍙椾激
        Node hurtNode = new TaskHurt(this, ani);

        // 2. 鎴樻枟妫€娴?(琚縺鎬?OR 鐪嬭浜?
        Node checkAggro = new CheckAggro(this);
        // 濡傛灉璺濈 <= viewRange锛岃€屼笖鍦ㄨ搴﹁寖鍥村唴 瑙嗕负鍙戠幇鏁屼汉
        Node checkViewSector = new CheckTargetSector(transform, playerTransform, viewRange, viewAngle);

        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewSector });


        // 鎴樻枟琛屼负
        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);
        Node chaseAction = new TaskNavMove(agent, playerTransform, ani, this);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        // 3. 宸￠€?(鍒涘缓瀹炰緥骞朵繚瀛樺紩鐢?
        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani, this);
        Node idle5s = new TaskTimedIdle(ani, 5.0f, this);

        // 宸￠€婚€昏緫锛氬厛宸￠€?-> 鍒颁簡浼戞伅 -> 閲嶅
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


    //鍙互鏀诲嚮
    protected override void PerformAttack()
    {
        // 1. 妫€鏌ョ帺瀹舵槸鍚﹀瓨鍦ㄤ笖瀛樻椿
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
        _showReadySprite = active && !isDead;
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

        if (_showReadySprite && readySprite != null)
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




}
