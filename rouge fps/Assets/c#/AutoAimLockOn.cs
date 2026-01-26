using UnityEngine;

/// <summary>
/// 自瞄 / 索敌组件（类似 OW 士兵76 大招）
/// 关键点：
/// - active=true：旋转 firePoint 指向目标
/// - active=false：自动回正 firePoint（避免关闭后子弹偏）
/// </summary>
public class AutoAimLockOn : MonoBehaviour
{
    [Header("状态 State")]
    [Tooltip("启用时会自动选择目标，并让 firePoint 朝向目标点。关闭时会回正 firePoint。")]
    public bool active = false;

    [Tooltip("默认持续时间（秒）。Activate() 不传 duration 时使用；<=0 表示不自动关闭。")]
    public float defaultDuration = 6f;

    [Header("引用 Refs")]
    [Tooltip("玩家视角来源。一般填 Main Camera Transform。不填则尝试 Camera.main。")]
    public Transform viewTransform;

    [Tooltip("枪口/射线起点。必须填（对应 CameraGunChannel 的 firePoint）。")]
    public Transform firePoint;

    [Header("索敌 Targeting")]
    public float maxDistance = 80f;

    [Range(1f, 90f)]
    public float maxAngle = 18f;

    [Range(0.02f, 0.5f)]
    public float refreshInterval = 0.06f;

    public LayerMask targetMask = ~0;
    public LayerMask occlusionMask = ~0;

    [Header("评分 Scoring")]
    public float angleWeight = 1.0f;
    public float distanceWeight = 0.25f;

    [Header("瞄准点 Aim Point")]
    public bool preferHeadHitbox = false;

    private float _activeUntil = -1f;
    private float _nextRefreshTime = 0f;

    private Transform _targetRoot;
    private Collider _targetCollider;

    private Quaternion _defaultLocalRotation;
    private bool _hasDefaultLocalRotation;

    // 新增：用于检测 active 的状态切换（true->false）
    private bool _prevActive;


    // ===== 提供给UI/外部系统的只读接口 =====

    /// <summary>当前是否有锁定目标</summary>
    public bool HasTarget => _targetCollider != null;

    /// <summary>当前锁定目标的碰撞体（可能用于UI或调试）</summary>
    public Collider CurrentTargetCollider => _targetCollider;

    /// <summary>当前锁定目标的屏幕跟随点（世界坐标）</summary>
    public Vector3 CurrentAimWorldPoint
    {
        get
        {
            if (_targetCollider == null) return Vector3.zero;
            return GetAimPoint(_targetCollider);
        }
    }

    public void Activate(float duration = -1f)
    {
        active = true;

        float d = duration > 0f ? duration : defaultDuration;
        _activeUntil = d > 0f ? Time.time + d : -1f;
    }

    public void Deactivate()
    {
        active = false;
        _activeUntil = -1f;
        _targetRoot = null;
        _targetCollider = null;

        RestoreFirePointRotation();
    }

    private void Awake()
    {
        if (viewTransform == null && Camera.main != null)
            viewTransform = Camera.main.transform;

        CacheDefaultFirePointRotation();

        _prevActive = active;
        if (!active)
        {
            // 如果一开始就是关闭状态，确保 firePoint 在正确姿态
            RestoreFirePointRotation();
        }
    }

    private void OnEnable()
    {
        // 防止运行时重新启用组件，但 default rotation 没缓存
        CacheDefaultFirePointRotation();

        _prevActive = active;
        if (!active)
        {
            RestoreFirePointRotation();
        }
    }

    private void OnDisable()
    {
        // 组件被禁用时也要回正
        RestoreFirePointRotation();
    }

    private void Update()
    {
        // ✅ 关键修复：检测 active 从 true -> false 的切换时，自动回正
        if (_prevActive && !active)
        {
            _activeUntil = -1f;
            _targetRoot = null;
            _targetCollider = null;

            RestoreFirePointRotation();
        }
        _prevActive = active;

        if (!active)
            return;

        if (_activeUntil > 0f && Time.time >= _activeUntil)
        {
            Deactivate();
            return;
        }

        if (viewTransform == null || firePoint == null)
            return;

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + refreshInterval;
            RefreshTarget();
        }

        // 没锁到目标：也要回正 firePoint，避免残留上一次锁定方向导致子弹偏
        if (_targetCollider == null)
        {
            RestoreFirePointRotation();
            return;
        }


        Vector3 aimPoint = GetAimPoint(_targetCollider);
        Vector3 dir = aimPoint - firePoint.position;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        Quaternion look = Quaternion.LookRotation(dir, viewTransform.up);
        firePoint.rotation = look;
    }

    private void RefreshTarget()
    {
        if (_targetCollider != null && IsTargetValid(_targetCollider))
            return;

        _targetRoot = null;
        _targetCollider = null;

        // 目标丢失时立刻回正，避免残留方向
        RestoreFirePointRotation();


        Collider best = FindBestTarget();
        if (best != null)
        {
            _targetCollider = best;
            _targetRoot = best.transform.root;
        }
    }

    private Collider FindBestTarget()
    {
        Vector3 origin = viewTransform.position;
        Collider[] cols = Physics.OverlapSphere(origin, maxDistance, targetMask, QueryTriggerInteraction.Ignore);

        float bestScore = float.PositiveInfinity;
        Collider best = null;

        Vector3 fwd = viewTransform.forward;

        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null) continue;

            if (c.GetComponentInParent<IDamageable>() == null)
                continue;

            if (!IsTargetValid(c))
                continue;

            Vector3 p = GetAimPoint(c);
            Vector3 to = p - origin;
            float dist = to.magnitude;
            if (dist <= 0.001f) continue;

            Vector3 dir = to / dist;
            float angle = Vector3.Angle(fwd, dir);
            if (angle > maxAngle) continue;

            float score = angle * angleWeight + dist * distanceWeight;
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private bool IsTargetValid(Collider c)
    {
        if (c == null) return false;

        if (c.GetComponentInParent<IDamageable>() == null)
            return false;

        Vector3 origin = viewTransform.position;
        Vector3 fwd = viewTransform.forward;

        Vector3 p = GetAimPoint(c);
        Vector3 to = p - origin;

        float dist = to.magnitude;
        if (dist <= 0.001f || dist > maxDistance)
            return false;

        float angle = Vector3.Angle(fwd, to / dist);
        if (angle > maxAngle)
            return false;

        Vector3 rayStart = origin;
        Vector3 rayDir = (p - rayStart);
        float rayLen = rayDir.magnitude;
        if (rayLen <= 0.001f) return false;

        rayDir /= rayLen;

        if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, rayLen, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            Transform ht = hit.collider != null ? hit.collider.transform : null;
            if (ht == null) return false;

            if (ht != c.transform && ht.root != c.transform.root)
                return false;
        }

        return true;
    }

    private Vector3 GetAimPoint(Collider c)
    {
        if (c == null) return Vector3.zero;

        if (preferHeadHitbox)
        {
            Hitbox[] hitboxes = c.transform.root.GetComponentsInChildren<Hitbox>(true);
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].part == Hitbox.Part.Head)
                {
                    Collider hc = hitboxes[i].GetComponent<Collider>();
                    if (hc != null) return hc.bounds.center;
                    return hitboxes[i].transform.position;
                }
            }
        }

        return c.bounds.center;
    }

    /// <summary>
    /// 缓存 firePoint 的默认 localRotation（用于关闭自瞄后回正）
    /// 注意：只在 firePoint 存在且还没缓存过时记录，避免运行时被覆盖。
    /// </summary>
    private void CacheDefaultFirePointRotation()
    {
        if (firePoint == null) return;
        if (_hasDefaultLocalRotation) return;

        _defaultLocalRotation = firePoint.localRotation;
        _hasDefaultLocalRotation = true;
    }

    /// <summary>
    /// 回正 firePoint：恢复初始 localRotation
    /// </summary>
    private void RestoreFirePointRotation()
    {
        if (firePoint == null) return;
        if (!_hasDefaultLocalRotation) return;

        firePoint.localRotation = _defaultLocalRotation;
    }
}
