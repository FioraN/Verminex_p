using PrototypeFPC;
using UnityEngine;

/// <summary>
/// Simple local-space gun model shake driven by fire events.
/// Attach it to the gun model root or a dedicated shake pivot.
/// </summary>
public sealed class GunModelFireShake : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Dependencies movementDependencies;

    [Header("Filter")]
    [SerializeField] private CameraGunChannel[] sourceFilter;
    [SerializeField] private bool hitscanOnly = false;
    [SerializeField] private bool projectileOnly = false;

    [Header("Shake")]
    [SerializeField] [Min(0f)] private float intensityPerShot = 1f;
    [SerializeField] [Min(0f)] private float maxIntensity = 3f;
    [SerializeField] [Min(0.01f)] private float damping = 12f;
    [SerializeField] [Min(0.01f)] private float frequency = 22f;

    [Header("Position Offset (Local XYZ)")]
    [SerializeField] private Vector3 maxLocalPositionOffset = new Vector3(0.015f, 0.01f, 0.03f);

    [Header("Rotation Offset")]
    [SerializeField] private Vector3 maxLocalRotationOffset = new Vector3(2.5f, 1.5f, 1f);

    [Header("Movement Sway")]
    [SerializeField] private bool enableMovementSway = true;
    [SerializeField] [Min(0f)] private float movementInputMultiplier = 1f;
    [SerializeField] [Min(0.01f)] private float movementSwaySmoothness = 10f;
    [SerializeField] private Vector3 movementPositionOffset = new Vector3(0.02f, 0.01f, 0.015f);
    [SerializeField] private Vector3 movementRotationOffset = new Vector3(1.5f, 1f, 2f);
    [SerializeField] [Min(0.01f)] private float movementBobFrequency = 8f;
    [SerializeField] [Min(0f)] private Vector3 movementBobOffset = new Vector3(0.004f, 0.006f, 0.002f);
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private float _currentIntensity;
    private float _time;
    private float _movementBobTime;
    private Vector3 _movementPositionCurrent;
    private Vector3 _movementRotationCurrent;

    private void Awake()
    {
        if (target == null)
            target = transform;

        if (movementDependencies == null)
            movementDependencies = GetComponentInParent<Dependencies>();

        CacheBaseTransform();
    }

    private void OnEnable()
    {
        CacheBaseTransform();
        CombatEventHub.OnFire += HandleFire;
    }

    private void OnDisable()
    {
        CombatEventHub.OnFire -= HandleFire;
        ResetTargetTransform();
        _currentIntensity = 0f;
        _movementPositionCurrent = Vector3.zero;
        _movementRotationCurrent = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        _time += Time.deltaTime;
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, 0f, damping * Time.deltaTime);

        float waveX = Mathf.Sin(_time * frequency);
        float waveY = Mathf.Sin(_time * frequency * 1.37f + 1.1f);
        float waveZ = Mathf.Sin(_time * frequency * 0.83f + 2.4f);

        Vector3 localPosOffset = Vector3.Scale(new Vector3(waveX, waveY, waveZ), maxLocalPositionOffset) * _currentIntensity;
        Vector3 localRotOffset = Vector3.Scale(new Vector3(waveY, waveZ, waveX), maxLocalRotationOffset) * _currentIntensity;

        UpdateMovementSway();

        Vector3 finalPosition = _baseLocalPosition + localPosOffset + _movementPositionCurrent;
        Vector3 finalRotation = localRotOffset + _movementRotationCurrent;

        target.localPosition = finalPosition;
        target.localRotation = _baseLocalRotation * Quaternion.Euler(finalRotation);
    }

    private void HandleFire(CombatEventHub.FireEvent e)
    {
        if (!PassesSourceFilter(e.source))
            return;

        if (hitscanOnly && e.isProjectile)
            return;

        if (projectileOnly && !e.isProjectile)
            return;

        _currentIntensity = Mathf.Min(maxIntensity, _currentIntensity + intensityPerShot);
    }

    private bool PassesSourceFilter(CameraGunChannel source)
    {
        if (sourceFilter == null || sourceFilter.Length == 0)
            return true;

        for (int i = 0; i < sourceFilter.Length; i++)
        {
            if (sourceFilter[i] == source)
                return true;
        }

        return false;
    }

    private void CacheBaseTransform()
    {
        if (target == null)
            return;

        _baseLocalPosition = target.localPosition;
        _baseLocalRotation = target.localRotation;
    }

    private void ResetTargetTransform()
    {
        if (target == null)
            return;

        target.localPosition = _baseLocalPosition;
        target.localRotation = _baseLocalRotation;
    }

    private void UpdateMovementSway()
    {
        if (!enableMovementSway)
        {
            _movementPositionCurrent = Vector3.Lerp(_movementPositionCurrent, Vector3.zero, movementSwaySmoothness * Time.deltaTime);
            _movementRotationCurrent = Vector3.Lerp(_movementRotationCurrent, Vector3.zero, movementSwaySmoothness * Time.deltaTime);
            return;
        }

        float inputX = Input.GetAxisRaw(horizontalAxis) * movementInputMultiplier;
        float inputY = Input.GetAxisRaw(verticalAxis) * movementInputMultiplier;
        Vector2 moveInput = Vector2.ClampMagnitude(new Vector2(inputX, inputY), 1f);
        float moveAmount = moveInput.magnitude;
        bool isGrounded = movementDependencies == null || movementDependencies.isGrounded;

        Vector3 swayTargetPosition = new Vector3(
            -moveInput.x * movementPositionOffset.x,
            0f,
            -moveInput.y * movementPositionOffset.z
        );

        Vector3 swayTargetRotation = new Vector3(
            -moveInput.y * movementRotationOffset.x,
            moveInput.x * movementRotationOffset.y,
            moveInput.x * movementRotationOffset.z
        );

        if (isGrounded && moveAmount > 0.001f)
        {
            _movementBobTime += Time.deltaTime * movementBobFrequency * (0.5f + moveAmount);
            swayTargetPosition += new Vector3(
                Mathf.Sin(_movementBobTime) * movementBobOffset.x,
                Mathf.Cos(_movementBobTime * 2f) * movementBobOffset.y,
                Mathf.Sin(_movementBobTime) * movementBobOffset.z
            ) * moveAmount;
        }

        _movementPositionCurrent = Vector3.Lerp(
            _movementPositionCurrent,
            swayTargetPosition,
            movementSwaySmoothness * Time.deltaTime
        );

        _movementRotationCurrent = Vector3.Lerp(
            _movementRotationCurrent,
            swayTargetRotation,
            movementSwaySmoothness * Time.deltaTime
        );
    }
}
