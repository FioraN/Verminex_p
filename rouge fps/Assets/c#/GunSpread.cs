using UnityEngine;

public class GunSpread : MonoBehaviour
{
    public enum SpreadShape
    {
        Cone,
        Diamond
    }

    [Header("Spread Settings")]
    [Min(0f)] public float baseSpread = 0.2f;
    [Min(0f)] public float spreadIncreasePerShot = 0.12f;
    [Min(0f)] public float spreadRecoverSpeed = 2.5f;
    [Min(0f)] public float maxSpread = 6f;

    [Header("Growth Curve")]
    [Tooltip("Input: current spread progress 0..1 (between baseSpread and maxSpread). Output: multiplier for spreadIncreasePerShot.")]
    public AnimationCurve spreadGrowthCurve01 = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [Min(0f)] public float minIncreaseMultiplier = 0f;
    [Min(0f)] public float maxIncreaseMultiplier = 3f;

    [Header("Shotgun Extra Spread")]
    [Min(0f)] public float shotgunExtraSpread = 3.5f;

    [Header("Shapes")]
    public SpreadShape nonShotgunShape = SpreadShape.Diamond;
    public SpreadShape shotgunShape = SpreadShape.Cone;

    private float _currentSpread;

    public float CurrentSpread => _currentSpread;

    private void Awake()
    {
        _currentSpread = baseSpread;
    }

    private void Update()
    {
        _currentSpread = Mathf.MoveTowards(_currentSpread, baseSpread, spreadRecoverSpeed * Time.deltaTime);
    }

    // Normal self-bloom
    public void OnShotFired()
    {
        AddBloomDegrees(ComputePerShotAddDegrees());
    }

    // Interference: add raw degrees directly
    public void ApplyInterferenceDegrees(float degrees)
    {
        AddBloomDegrees(degrees);
    }

    // Interference: add scaled per-shot bloom (recommended)
    public void ApplyInterferenceScaled(float scale)
    {
        AddBloomDegrees(ComputePerShotAddDegrees() * Mathf.Max(0f, scale));
    }

    private float ComputePerShotAddDegrees()
    {
        float denom = Mathf.Max(0.0001f, maxSpread - baseSpread);
        float t01 = Mathf.Clamp01((_currentSpread - baseSpread) / denom);

        float mult = spreadGrowthCurve01.Evaluate(t01);
        mult = Mathf.Clamp(mult, minIncreaseMultiplier, maxIncreaseMultiplier);

        return spreadIncreasePerShot * mult;
    }

    private void AddBloomDegrees(float add)
    {
        if (add <= 0f) return;
        _currentSpread = Mathf.Min(maxSpread, _currentSpread + add);
    }

    public float GetFinalSpread(bool isShotgun)
    {
        return _currentSpread + (isShotgun ? shotgunExtraSpread : 0f);
    }

    public Vector3 GetDirection(Vector3 forward, Vector3 right, Vector3 up, bool isShotgun)
    {
        float spreadDeg = GetFinalSpread(isShotgun);
        if (spreadDeg <= 0.001f) return forward.normalized;

        SpreadShape shape = isShotgun ? shotgunShape : nonShotgunShape;

        if (shape == SpreadShape.Diamond)
            return ApplyDiamondSpread(forward, right, up, spreadDeg);

        return ApplyConeSpread(forward, spreadDeg);
    }

    private Vector3 ApplyConeSpread(Vector3 forward, float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float u = Random.value;
        float v = Random.value;

        float theta = 2f * Mathf.PI * u;
        float phi = Mathf.Acos(1f - v * (1f - Mathf.Cos(angleRad)));

        float x = Mathf.Sin(phi) * Mathf.Cos(theta);
        float y = Mathf.Sin(phi) * Mathf.Sin(theta);
        float z = Mathf.Cos(phi);

        Vector3 w = forward.normalized;
        Vector3 a = Mathf.Abs(Vector3.Dot(w, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        Vector3 uAxis = Vector3.Normalize(Vector3.Cross(a, w));
        Vector3 vAxis = Vector3.Cross(w, uAxis);

        return (uAxis * x + vAxis * y + w * z).normalized;
    }

    private Vector3 ApplyDiamondSpread(Vector3 forward, Vector3 right, Vector3 up, float angleDeg)
    {
        Vector2 p = SampleDiamond();
        float t = Mathf.Tan(angleDeg * Mathf.Deg2Rad);

        Vector3 dir = forward.normalized + right.normalized * (p.x * t) + up.normalized * (p.y * t);
        return dir.normalized;
    }

    private Vector2 SampleDiamond()
    {
        while (true)
        {
            float x = Random.Range(-1f, 1f);
            float y = Random.Range(-1f, 1f);
            if (Mathf.Abs(x) + Mathf.Abs(y) <= 1f)
                return new Vector2(x, y);
        }
    }

    public float CurrentMaxDiamondSpread(bool isShotgun)
    {
        return GetFinalSpread(isShotgun);
    }
}
