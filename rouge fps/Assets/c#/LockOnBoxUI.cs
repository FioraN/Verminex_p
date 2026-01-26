using UnityEngine;

/// <summary>
/// 锁敌框UI：
/// - 自瞄关闭：隐藏
/// - 自瞄开启：
///   - 有目标：锁定框跟随目标屏幕位置
///   - 无目标：锁定框显示在准星位置（默认屏幕中心，可选用 crosshairRect）
/// </summary>
public class LockOnBoxUI : MonoBehaviour
{
    [Header("绑定")]
    [Tooltip("要跟随的自瞄组件（主枪或副枪的 AutoAimLockOn）")]
    public AutoAimLockOn autoAim;

    [Tooltip("锁定框UI本体 RectTransform（一般就是挂脚本的这个对象）")]
    public RectTransform box;

    [Tooltip("用于 WorldToScreenPoint 的相机。一般填 Main Camera。")]
    public Camera cam;

    [Header("准星位置（无目标时用）")]
    [Tooltip("如果你已经有准星UI，把准星的 RectTransform 拖进来；不填则使用屏幕中心。")]
    public RectTransform crosshairRect;

    [Tooltip("准星偏移（像素）。例如你的准星不是正中心时可用。")]
    public Vector2 crosshairOffset = Vector2.zero;

    [Header("目标跟随偏移（有目标时用）")]
    [Tooltip("锁定目标时的屏幕空间偏移（像素）")]
    public Vector2 targetScreenOffset = Vector2.zero;

    [Header("显示逻辑")]
    [Tooltip("目标在屏幕外时是否隐藏（在准星模式下不会隐藏）")]
    public bool hideWhenOffscreen = true;

    [Header("外观")]
    [Tooltip("锁定框缩放")]
    public Vector3 boxScale = Vector3.one;
    // 用 CanvasGroup 控制显示隐藏，避免把自己 SetActive(false) 后脚本停掉
    private CanvasGroup _cg;


    private void Reset()
    {
        box = GetComponent<RectTransform>();
        cam = Camera.main;
    }

    private void Awake()
    {
        if (box == null) box = GetComponent<RectTransform>();
        if (cam == null) cam = Camera.main;

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        // 初始隐藏（但对象保持激活，脚本会持续运行）
        ApplyVisible(false);
    }



    private void LateUpdate()
    {
        if (autoAim == null || box == null || cam == null)
        {
            ApplyVisible(false);
            return;
        }

        // 自瞄没开 -> 直接隐藏
        if (!autoAim.active)
        {
            ApplyVisible(false);
            return;
        }

        // 自瞄开了 -> 一定显示（B模式：没目标也显示在准星）
        ApplyVisible(true);
        box.localScale = boxScale;

        // 有目标：跟随目标
        if (autoAim.HasTarget)
        {
            Vector3 world = autoAim.CurrentAimWorldPoint;
            Vector3 sp = cam.WorldToScreenPoint(world);

            // 在相机背后
            if (sp.z <= 0f)
            {
                // 背后就退回准星显示（更符合“正在索敌/目标丢失”的感觉）
                SetToCrosshair();
                return;
            }

            // 屏幕外
            bool off =
                sp.x < 0f || sp.x > Screen.width ||
                sp.y < 0f || sp.y > Screen.height;

            if (off && hideWhenOffscreen)
            {
                // 屏幕外就退回准星显示
                SetToCrosshair();
                return;
            }

            box.position = new Vector3(
                sp.x + targetScreenOffset.x,
                sp.y + targetScreenOffset.y,
                0f
            );
            return;
        }

        // 无目标：显示在准星
        SetToCrosshair();
    }

    /// <summary>
    /// 将锁定框放到准星位置
    /// </summary>
    private void SetToCrosshair()
    {
        Vector3 pos;

        if (crosshairRect != null)
        {
            // 如果你有准星UI，就直接用它的位置（适配各种Canvas模式）
            pos = crosshairRect.position;
        }
        else
        {
            // 否则使用屏幕中心
            pos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        box.position = new Vector3(
            pos.x + crosshairOffset.x,
            pos.y + crosshairOffset.y,
            0f
        );
    }

    private void ApplyVisible(bool v)
    {
        if (_cg == null) return;

        // 透明度控制显示隐藏（不会停脚本）
        _cg.alpha = v ? 1f : 0f;

        // 可选：不挡鼠标
        _cg.blocksRaycasts = false;
        _cg.interactable = false;
    }

}
