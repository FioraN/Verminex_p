using UnityEngine;

public class CameraGunChannel : MonoBehaviour
{
    public enum Role { Primary, Secondary }
    public enum FireMode { Semi, Auto }
    public enum ShotType { Single, Shotgun }
    public enum BallisticsMode { Hitscan, Projectile }

    [Header("Role")]
    public Role role = Role.Primary;

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    [Header("Refs")]
    public Transform firePoint;
    public GunAmmo ammo;
    public GunRecoil recoil;
    public GunSpread spread;

    [Header("Modes")]
    public FireMode fireMode = FireMode.Auto;
    public ShotType shotType = ShotType.Single;
    public BallisticsMode ballisticsMode = BallisticsMode.Hitscan;

    [Header("Fire")]
    [Min(0.01f)] public float fireRate = 10f;
    [Min(0f)] public float maxRange = 200f;
    [Min(1)] public int pelletsPerShot = 8;

    [Header("Semi Extra Cooldown")]
    [Min(0f)] public float semiFireCooldown = 0.15f;

    [Header("Projectile")]
    public BulletProjectile projectilePrefab;
    [Min(0.01f)] public float bulletSpeed = 80f;
    [Min(0f)] public float bulletGravity = 0f;
    [Min(0.01f)] public float bulletLifetime = 3f;

    [Header("Damage")]
    [Min(0f)] public float baseDamage = 10f;
    public AnimationCurve damageFalloffByDistance01 = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Hit")]
    public LayerMask hitMask = ~0;

    // ------------------------------------------------------------
    // Runtime Stat Stacking (Flat + Additive + Multiplicative)
    // D = (D0 + flat) * (1 + addPct) * mul
    // Note: D0 here is the CURRENT inspector field value (baseDamage/fireRate/etc),
    // so existing perks that directly modify those fields remain compatible.
    // ------------------------------------------------------------
    [Header("Runtime Stat Stacking (Optional)")]
    [Tooltip("If enabled, final runtime stats use: (base + flat) * (1 + addPct) * mul.")]
    [SerializeField] private bool enableRuntimeStacking = true;

    [Header("Damage Stack")]
    public float damageFlat = 0f;
    [Tooltip("0.2 = +20%")]
    public float damageAddPct = 0f;
    [Tooltip("1.5 = x1.5")]
    public float damageMul = 1f;

    [Header("FireRate Stack")]
    public float fireRateFlat = 0f;
    [Tooltip("0.2 = +20%")]
    public float fireRateAddPct = 0f;
    [Tooltip("1.5 = x1.5")]
    public float fireRateMul = 1f;

    [Header("BulletSpeed Stack")]
    public float bulletSpeedFlat = 0f;
    [Tooltip("0.2 = +20%")]
    public float bulletSpeedAddPct = 0f;
    [Tooltip("1.5 = x1.5")]
    public float bulletSpeedMul = 1f;

    [Header("MaxRange Stack")]
    public float maxRangeFlat = 0f;
    [Tooltip("0.2 = +20%")]
    public float maxRangeAddPct = 0f;
    [Tooltip("1.5 = x1.5")]
    public float maxRangeMul = 1f;

    // Helper API for perks (optional to use)
    public void ResetRuntimeStacks()
    {
        damageFlat = 0f; damageAddPct = 0f; damageMul = 1f;
        fireRateFlat = 0f; fireRateAddPct = 0f; fireRateMul = 1f;
        bulletSpeedFlat = 0f; bulletSpeedAddPct = 0f; bulletSpeedMul = 1f;
        maxRangeFlat = 0f; maxRangeAddPct = 0f; maxRangeMul = 1f;
    }

    private float EvalStack(float baseValue, float flat, float addPct, float mul, float minValue)
    {
        // guard mul
        if (mul <= 0f) mul = 1f;
        float v = (baseValue + flat) * (1f + addPct) * mul;
        if (v < minValue) v = minValue;
        return v;
    }

    private float RuntimeDamage =>
        enableRuntimeStacking ? EvalStack(baseDamage, damageFlat, damageAddPct, damageMul, 0f) : baseDamage;

    private float RuntimeFireRate =>
        enableRuntimeStacking ? EvalStack(fireRate, fireRateFlat, fireRateAddPct, fireRateMul, 0.01f) : Mathf.Max(0.01f, fireRate);

    private float RuntimeBulletSpeed =>
        enableRuntimeStacking ? EvalStack(bulletSpeed, bulletSpeedFlat, bulletSpeedAddPct, bulletSpeedMul, 0.01f) : Mathf.Max(0.01f, bulletSpeed);

    private float RuntimeMaxRange =>
        enableRuntimeStacking ? EvalStack(maxRange, maxRangeFlat, maxRangeAddPct, maxRangeMul, 0f) : maxRange;

    // ------------------------------------------------------------

    public System.Action<CameraGunChannel> OnShot;

    private float _nextFireTime;
    private float _nextSemiAllowedTime;

    private CameraGunDual _dual;

    private void Awake()
    {
        if (firePoint == null) firePoint = transform;

        AutoAssignIfSafe();

        _dual = GetComponent<CameraGunDual>();
        if (_dual == null) _dual = FindFirstObjectByType<CameraGunDual>();

        HookAmmoEvents();
    }

    private void OnEnable()
    {
        HookAmmoEvents();
    }

    private void OnDisable()
    {
        UnhookAmmoEvents();
    }

    private void OnDestroy()
    {
        UnhookAmmoEvents();
    }

    private void Update()
    {
        HandleFireInput();
    }

    public bool HasValidSetup()
    {
        return firePoint != null && ammo != null && recoil != null && spread != null;
    }

    public void ApplyInterferenceScaled(float scale)
    {
        if (spread == null) return;
        spread.ApplyInterferenceScaled(scale);
    }

    private void HandleFireInput()
    {
        if (!HasValidSetup()) return;

        // Block ALL firing during uninterruptible magazine stage
        if (_dual != null && _dual.ShouldBlockFiring())
            return;

        // wantsFire MUST be decided by Dual so semi can preempt auto in exclusive mode
        bool wantsFire = (_dual != null)
            ? _dual.GetWantsFire(this)
            : (fireMode == FireMode.Auto ? Input.GetKey(fireKey) : Input.GetKeyDown(fireKey));

        if (!wantsFire) return;

        if (fireMode == FireMode.Semi && semiFireCooldown > 0f)
        {
            if (Time.time < _nextSemiAllowedTime) return;
        }

        // If per-bullet reload is active, firing should interrupt it (magazine never interruptible)
        if (_dual != null)
            _dual.InterruptPerBulletReloadForFire();

        if (ammo.IsReloading && ammo.IsUninterruptible) return;
        if (Time.time < _nextFireTime) return;

        if (!ammo.HasAmmoInMag())
        {
            // Auto reload is handled by Dual (empty -> release -> wait)
            return;
        }

        if (!ammo.TryConsumeOne())
            return;

        OnShot?.Invoke(this);

        recoil.Kick();
        spread.OnShotFired();

        int shots = (shotType == ShotType.Shotgun) ? pelletsPerShot : 1;
        bool isShotgun = (shotType == ShotType.Shotgun);

        for (int i = 0; i < shots; i++)
        {
            Vector3 dir = spread.GetDirection(
                firePoint.forward,
                firePoint.right,
                firePoint.up,
                isShotgun
            );

            if (ballisticsMode == BallisticsMode.Hitscan)
                FireHitscan(dir);
            else
                FireProjectile(dir);
        }

        // Use runtime fire rate (supports additive stacking)
        _nextFireTime = Time.time + (1f / RuntimeFireRate);

        // Fire event: pellets should be the actual count fired this trigger
        CombatEventHub.RaiseFire(new CombatEventHub.FireEvent
        {
            source = this,
            pellets = shots,
            isProjectile = (ballisticsMode == BallisticsMode.Projectile),
            time = Time.time
        });

        if (fireMode == FireMode.Semi && semiFireCooldown > 0f)
            _nextSemiAllowedTime = Time.time + semiFireCooldown;
    }

    private void FireHitscan(Vector3 dir)
    {
        Ray ray = new Ray(firePoint.position, dir);

        if (Physics.Raycast(ray, out RaycastHit hit, RuntimeMaxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            float mr = RuntimeMaxRange;
            float t01 = mr <= 0.0001f ? 1f : Mathf.Clamp01(hit.distance / mr);
            float mult = Mathf.Max(0f, damageFalloffByDistance01.Evaluate(t01));
            float finalDamage = RuntimeDamage * mult;

            Hitbox hb = hit.collider.GetComponent<Hitbox>();
            bool isHead = hb != null && hb.part == Hitbox.Part.Head;

            var dmgEx = hit.collider.GetComponentInParent<IDamageableEx>();
            if (dmgEx != null)
            {
                dmgEx.TakeDamage(new DamageInfo
                {
                    source = this,
                    damage = finalDamage,
                    isHeadshot = isHead,
                    hitPoint = hit.point,
                    hitCollider = hit.collider
                });
            }
            else
            {
                IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(finalDamage);
            }
        }
    }

    private void FireProjectile(Vector3 dir)
    {
        if (projectilePrefab == null) return;

        // 1) spawn projectile
        BulletProjectile p = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
        p.gameObject.SetActive(true);

        // 2) if projectile has BulletHitDamage, assign source + runtime damage
        var hitDamage = p.GetComponentInChildren<BulletHitDamage>(true);
        if (hitDamage != null)
        {
            hitDamage.source = this;
            hitDamage.baseDamage = RuntimeDamage;
            hitDamage.Init(RuntimeDamage, this);
        }

        // 3) optional: ensure visible
        var r = p.GetComponentInChildren<Renderer>();
        if (r != null) r.enabled = true;

        // 4) init config with runtime stats
        p.Init(new BulletProjectile.Config
        {
            source = this,

            speed = RuntimeBulletSpeed,
            gravity = bulletGravity,
            lifetime = bulletLifetime,
            maxRange = RuntimeMaxRange,
            baseDamage = RuntimeDamage,
            falloff = damageFalloffByDistance01,
            hitMask = hitMask
        });
    }

    public void FireBonusPellets(int pellets)
    {
        // Bonus shots should not consume cooldown / trigger OnFire again.
        // They reuse the current spread sampling and ballistic mode.
        if (!HasValidSetup()) return;
        if (pellets <= 0) return;

        bool isShotgun = (shotType == ShotType.Shotgun);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = spread.GetDirection(
                firePoint.forward,
                firePoint.right,
                firePoint.up,
                isShotgun
            );

            if (ballisticsMode == BallisticsMode.Hitscan)
                FireHitscan(dir);
            else
                FireProjectile(dir);
        }
    }

    private void AutoAssignIfSafe()
    {
        if (ammo == null)
        {
            var arr = GetComponents<GunAmmo>();
            if (arr.Length == 1) ammo = arr[0];
        }

        if (recoil == null)
        {
            var arr = GetComponents<GunRecoil>();
            if (arr.Length == 1) recoil = arr[0];
        }

        if (spread == null)
        {
            var arr = GetComponents<GunSpread>();
            if (arr.Length == 1) spread = arr[0];
        }
    }

    private void HookAmmoEvents()
    {
        if (ammo == null) return;

        // prevent double subscribe
        ammo.OnReloadStart -= HandleReloadStart;
        ammo.OnReloadEnd -= HandleReloadEnd;

        ammo.OnReloadStart += HandleReloadStart;
        ammo.OnReloadEnd += HandleReloadEnd;
    }

    private void UnhookAmmoEvents()
    {
        if (ammo == null) return;
        ammo.OnReloadStart -= HandleReloadStart;
        ammo.OnReloadEnd -= HandleReloadEnd;
    }

    private void HandleReloadStart()
    {
        CombatEventHub.RaiseReload(new CombatEventHub.ReloadEvent
        {
            source = this,
            isStart = true,
            time = Time.time
        });
    }

    private void HandleReloadEnd()
    {
        CombatEventHub.RaiseReload(new CombatEventHub.ReloadEvent
        {
            source = this,
            isStart = false,
            time = Time.time
        });
    }
}

// Keep ONLY ONE copy of this interface in your whole project.
public interface IDamageable
{
    void TakeDamage(float amount);
}