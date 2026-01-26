using UnityEngine;

public class SpreadDiamondUI : MonoBehaviour
{
    public enum Channel { Primary, Secondary }

    [Header("Refs")]
    public CameraGunDual dual;
    public CameraGunChannel primary;
    public CameraGunChannel secondary;
    public Channel channel = Channel.Primary;

    [Header("UI Points")]
    public RectTransform top;
    public RectTransform bottom;
    public RectTransform left;
    public RectTransform right;

    [Header("Scale")]
    public float pixelsPerDegree = 6f;
    public float maxPixels = 250f;
    public float smooth = 20f;

    private float _uiRadius;

    private void Awake()
    {
        TryAutoWire();
    }

    private void Update()
    {
        TryAutoWireIfMissing();

        CameraGunChannel ch = (channel == Channel.Primary) ? primary : secondary;
        if (ch == null || ch.spread == null) return;

        bool isShotgun = ch.shotType == CameraGunChannel.ShotType.Shotgun;
        float spreadDeg = ch.spread.CurrentMaxDiamondSpread(isShotgun);

        float targetRadius = Mathf.Min(maxPixels, spreadDeg * pixelsPerDegree);
        _uiRadius = Mathf.Lerp(_uiRadius, targetRadius, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        if (top != null) top.anchoredPosition = new Vector2(0f, _uiRadius);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -_uiRadius);
        if (left != null) left.anchoredPosition = new Vector2(-_uiRadius, 0f);
        if (right != null) right.anchoredPosition = new Vector2(_uiRadius, 0f);
    }

    private void TryAutoWire()
    {
        DualGunResolver.TryResolve(ref dual, ref primary, ref secondary);
    }

    private void TryAutoWireIfMissing()
    {
        if (primary == null || secondary == null)
            TryAutoWire();
    }
}
