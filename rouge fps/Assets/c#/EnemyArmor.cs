using UnityEngine;

/// <summary>
/// Enemy armor with Hades-like behavior:
/// - Damage hits armor first; overflow can hit HP (handled by MonsterHealth).
/// - Modes:
///   Permanent: armor never returns after depleted
///   Regen: armor starts regenerating after a delay once broken
///   BreakWindow: after broken, enter a vulnerable window; optionally restore armor when the window ends
/// </summary>
public sealed class EnemyArmor : MonoBehaviour
{
    public enum ArmorMode
    {
        Permanent,
        Regen,
        BreakWindow
    }

    public enum BreakRestoreMode
    {
        None,       // no armor restore after vulnerable window
        Instant,    // restore instantly to target amount
        Gradual     // restore gradually to target amount
    }

    [Header("Armor")]
    [Min(0f)] public float maxArmor = 50f;
    [Min(0f)] public float armor = 50f;

    [Header("Mode")]
    public ArmorMode mode = ArmorMode.Permanent;

    [Header("Regen")]
    [Min(0f)] public float regenDelay = 2f;
    [Min(0f)] public float regenRate = 10f;

    [Header("Break Window")]
    [Min(0f)] public float vulnerableDuration = 3f;

    [Tooltip("HP damage multiplier while in vulnerable window (MonsterHealth should use this).")]
    [Min(1f)] public float vulnerableHpDamageMultiplier = 1.5f;

    [Header("Break Window - Restore")]
    public BreakRestoreMode breakRestoreMode = BreakRestoreMode.Instant;

    [Tooltip("Target armor amount after vulnerable window. Set to maxArmor for full restore.")]
    [Min(0f)] public float restoreArmorAmount = 50f;

    [Tooltip("Only used when breakRestoreMode = Gradual. Armor restored per second.")]
    [Min(0f)] public float breakRestoreRate = 20f;

    [Header("Debug")]
    public bool isArmored;
    public bool inVulnerableWindow;
    public float remainingVulnerableTime;

    private float _regenStartTime;
    private bool _regenActive;

    private float _vulnerableEndTime;
    private bool _inVulnerable;

    private bool _breakRestoreActive;
    private float _breakRestoreTarget;

    public bool HasArmor => armor > 0.0001f;

    public bool InVulnerableWindow => mode == ArmorMode.BreakWindow && _inVulnerable && Time.time < _vulnerableEndTime;

    public float HpDamageMultiplier => InVulnerableWindow ? vulnerableHpDamageMultiplier : 1f;

    private void Awake()
    {
        maxArmor = Mathf.Max(0f, maxArmor);
        armor = Mathf.Clamp(armor, 0f, maxArmor);
        UpdateDebug();
    }

    private void Update()
    {
        if (mode == ArmorMode.Regen)
        {
            TickRegen();
        }
        else if (mode == ArmorMode.BreakWindow)
        {
            TickBreakWindow();
            TickBreakRestore();
        }

        UpdateDebug();
    }

    private void TickRegen()
    {
        if (!_regenActive) return;
        if (Time.time < _regenStartTime) return;

        if (armor < maxArmor)
        {
            armor = Mathf.Min(maxArmor, armor + regenRate * Time.deltaTime);
        }

        if (armor >= maxArmor - 0.0001f)
        {
            armor = maxArmor;
            _regenActive = false;
        }
    }

    private void TickBreakWindow()
    {
        if (!_inVulnerable)
        {
            remainingVulnerableTime = 0f;
            return;
        }

        if (Time.time < _vulnerableEndTime)
        {
            remainingVulnerableTime = _vulnerableEndTime - Time.time;
            return;
        }

        // End vulnerable window
        _inVulnerable = false;
        remainingVulnerableTime = 0f;

        StartBreakRestoreIfNeeded();
    }

    private void StartBreakRestoreIfNeeded()
    {
        _breakRestoreActive = false;

        if (breakRestoreMode == BreakRestoreMode.None) return;

        float target = Mathf.Clamp(restoreArmorAmount, 0f, maxArmor);

        if (breakRestoreMode == BreakRestoreMode.Instant)
        {
            armor = Mathf.Max(armor, target);
            return;
        }

        // Gradual
        _breakRestoreTarget = target;
        if (_breakRestoreTarget <= armor + 0.0001f) return;

        _breakRestoreActive = true;
    }

    private void TickBreakRestore()
    {
        if (!_breakRestoreActive) return;
        if (_inVulnerable) return; // do not restore during vulnerable window
        if (breakRestoreMode != BreakRestoreMode.Gradual) { _breakRestoreActive = false; return; }

        if (breakRestoreRate <= 0f)
        {
            armor = Mathf.Max(armor, _breakRestoreTarget);
            _breakRestoreActive = false;
            return;
        }

        armor = Mathf.Min(_breakRestoreTarget, armor + breakRestoreRate * Time.deltaTime);

        if (armor >= _breakRestoreTarget - 0.0001f)
        {
            armor = _breakRestoreTarget;
            _breakRestoreActive = false;
        }
    }

    /// <summary>
    /// Damages armor and returns how much armor was actually removed.
    /// </summary>
    public float DamageArmor(float amount)
    {
        if (amount <= 0f) return 0f;
        if (!HasArmor) return 0f;

        float before = armor;
        armor = Mathf.Max(0f, armor - amount);
        float taken = before - armor;

        if (before > 0f && armor <= 0f)
        {
            OnArmorBroken();
        }

        UpdateDebug();
        return taken;
    }

    private void OnArmorBroken()
    {
        if (mode == ArmorMode.Regen)
        {
            _regenActive = true;
            _regenStartTime = Time.time + regenDelay;
        }
        else if (mode == ArmorMode.BreakWindow)
        {
            _inVulnerable = true;
            _vulnerableEndTime = Time.time + vulnerableDuration;
            remainingVulnerableTime = Mathf.Max(0f, _vulnerableEndTime - Time.time);

            // Cancel any ongoing restore while entering vulnerable
            _breakRestoreActive = false;
        }
    }

    private void UpdateDebug()
    {
        isArmored = HasArmor;
        inVulnerableWindow = InVulnerableWindow;

        if (!_inVulnerable)
        {
            remainingVulnerableTime = 0f;
        }
    }
}
