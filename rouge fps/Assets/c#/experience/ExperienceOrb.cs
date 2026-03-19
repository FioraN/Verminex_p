using UnityEngine;

/// <summary>
/// Minecraft-like XP orb:
/// - Pops out on spawn
/// - Falls to the ground and settles near the surface
/// - Hovers/bobs above the ground
/// - Flies to the player and is collected by distance, not collider blocking
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public sealed class ExperienceOrb : MonoBehaviour
{
    [Min(1)] public int experienceValue = 1;

    [Header("Spawn Motion")]
    [Min(0f)] public float spawnHorizontalSpeed = 1.5f;
    [Min(0f)] public float spawnUpSpeedMin = 1.5f;
    [Min(0f)] public float spawnUpSpeedMax = 3f;

    [Header("World Motion")]
    [Min(0f)] public float gravity = 14f;
    [Range(0f, 1f)] public float groundBounceDamping = 0.35f;
    [Range(0f, 1f)] public float groundFriction = 0.82f;
    [Min(0f)] public float stopSpeed = 0.15f;
    [Min(0f)] public float probePadding = 0.02f;
    [Min(0f)] public float maxStepDistance = 0.5f;

    [Header("Hover")]
    [Min(0f)] public float hoverHeight = 0.2f;
    [Min(0f)] public float hoverAmplitude = 0.08f;
    [Min(0f)] public float hoverFrequency = 2.5f;
    [Min(0f)] public float groundSnapSpeed = 6f;

    [Header("Pickup")]
    public string playerTag = "Player";
    [Min(0f)] public float seekRadius = 6f;
    [Min(0f)] public float minSeekSpeed = 2f;
    [Min(0f)] public float maxSeekSpeed = 14f;
    [Min(0f)] public float seekAcceleration = 28f;
    [Min(0f)] public float collectDistance = 0.6f;

    private SphereCollider _collider;
    private PlayerExperience _targetExperience;
    private Transform _targetTransform;
    private Vector3 _velocity;
    private bool _isGrounded;
    private float _baseGroundY;
    private float _hoverSeed;

    private void Awake()
    {
        _collider = GetComponent<SphereCollider>();
        _collider.isTrigger = true;

        Vector2 horizontal = UnityEngine.Random.insideUnitCircle * spawnHorizontalSpeed;
        float up = UnityEngine.Random.Range(spawnUpSpeedMin, Mathf.Max(spawnUpSpeedMin, spawnUpSpeedMax));
        _velocity = new Vector3(horizontal.x, up, horizontal.y);
        _hoverSeed = UnityEngine.Random.Range(0f, 10f);
    }

    private void Update()
    {
        if (_targetTransform == null)
            TryFindTarget();

        if (ShouldSeekTarget())
        {
            TickSeek();
        }
        else
        {
            TickWorldMotion();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void TryFindTarget()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return;

        _targetTransform = player.transform;
        _targetExperience = player.GetComponentInParent<PlayerExperience>();
    }

    private bool ShouldSeekTarget()
    {
        if (_targetTransform == null)
            return false;

        float sqrDistance = (_targetTransform.position - transform.position).sqrMagnitude;
        return sqrDistance <= seekRadius * seekRadius;
    }

    private void TickSeek()
    {
        if (_targetTransform == null)
            return;

        Vector3 targetPoint = _targetTransform.position + Vector3.up * 0.7f;
        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= collectDistance)
        {
            CollectTarget();
            return;
        }

        if (distance <= 0.001f)
            return;

        Vector3 desiredDir = toTarget / distance;
        float desiredSpeed = Mathf.Lerp(minSeekSpeed, maxSeekSpeed, 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, seekRadius)));
        Vector3 desiredVelocity = desiredDir * desiredSpeed;
        _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, seekAcceleration * Time.deltaTime);

        transform.position += _velocity * Time.deltaTime;
    }

    private void TickWorldMotion()
    {
        if (!_isGrounded)
        {
            _velocity += Vector3.down * gravity * Time.deltaTime;
            MoveWithCollisions(_velocity * Time.deltaTime);

            if (_isGrounded)
            {
                _baseGroundY = transform.position.y;
            }
            return;
        }

        _velocity.x *= groundFriction;
        _velocity.z *= groundFriction;
        if (new Vector2(_velocity.x, _velocity.z).magnitude <= stopSpeed)
        {
            _velocity.x = 0f;
            _velocity.z = 0f;
        }

        transform.position += new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;
        UpdateGroundHover();
    }

    private void MoveWithCollisions(Vector3 delta)
    {
        _isGrounded = false;

        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return;

        Vector3 direction = delta / distance;
        float remaining = distance;

        while (remaining > 0f)
        {
            float step = Mathf.Min(maxStepDistance, remaining);
            if (CastAndResolve(direction, step))
                return;

            transform.position += direction * step;
            remaining -= step;
        }
    }

    private bool CastAndResolve(Vector3 direction, float stepDistance)
    {
        float radius = GetProbeRadius();
        Vector3 origin = transform.position;
        float castDistance = stepDistance + probePadding;

        if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit, castDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        transform.position = hit.point + hit.normal * (radius + 0.001f);

        float upDot = Vector3.Dot(hit.normal, Vector3.up);
        if (upDot > 0.35f)
        {
            _isGrounded = true;
            if (_velocity.y < 0f)
                _velocity.y = -_velocity.y * groundBounceDamping;

            if (Mathf.Abs(_velocity.y) <= stopSpeed)
                _velocity.y = 0f;
        }
        else
        {
            _velocity = Vector3.Reflect(_velocity, hit.normal) * 0.35f;
        }

        return true;
    }

    private void UpdateGroundHover()
    {
        float radius = GetProbeRadius();
        Vector3 probeOrigin = transform.position + Vector3.up * (radius + 0.5f);
        if (Physics.SphereCast(probeOrigin, radius, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            _baseGroundY = Mathf.MoveTowards(_baseGroundY, hit.point.y + hoverHeight, groundSnapSpeed * Time.deltaTime);
        }

        float bob = Mathf.Sin((Time.time + _hoverSeed) * hoverFrequency) * hoverAmplitude;
        Vector3 pos = transform.position;
        pos.y = _baseGroundY + bob;
        transform.position = pos;
    }

    private void TryCollect(Collider other)
    {
        if (other == null) return;

        PlayerExperience receiver = other.GetComponentInParent<PlayerExperience>();
        if (receiver == null)
            return;

        bool matchesTag = string.IsNullOrWhiteSpace(playerTag)
            || other.CompareTag(playerTag)
            || other.transform.root.CompareTag(playerTag);
        if (!matchesTag)
            return;

        Collect(receiver);
    }

    private void CollectTarget()
    {
        if (_targetExperience == null && _targetTransform != null)
            _targetExperience = _targetTransform.GetComponentInParent<PlayerExperience>();

        if (_targetExperience == null)
            return;

        Collect(_targetExperience);
    }

    private void Collect(PlayerExperience receiver)
    {
        if (receiver == null)
            return;

        receiver.AddExperience(experienceValue);

        ExperienceEventHub.RaiseOrbCollected(new ExperienceEventHub.OrbCollectedEvent
        {
            receiver = receiver,
            orb = this,
            experienceValue = experienceValue,
            worldPosition = transform.position,
            time = Time.time
        });

        Destroy(gameObject);
    }

    private float GetProbeRadius()
    {
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        return Mathf.Max(0.01f, _collider.radius * scale);
    }
}
