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

        _nextFireTime = Time.time + (1f / fireRate);

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

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            float t01 = maxRange <= 0.0001f ? 1f : Mathf.Clamp01(hit.distance / maxRange);
            float mult = Mathf.Max(0f, damageFalloffByDistance01.Evaluate(t01));
            float finalDamage = baseDamage * mult;

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

        // 1) 生成弹体
        BulletProjectile p = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
        p.gameObject.SetActive(true);

        // 2) 如果弹体上（或子物体上）有 BulletHitDamage，也把 source 塞进去（兼容触发器伤害弹）
        //    注意：projectilePrefab 是 BulletProjectile 类型，但 prefab 内也可能同时挂了 BulletHitDamage（或在子物体）
        var hitDamage = p.GetComponentInChildren<BulletHitDamage>(true);
        if (hitDamage != null)
        {
            hitDamage.source = this;
            hitDamage.baseDamage = baseDamage; // 建议加上：让trigger子弹伤害跟枪一致
            hitDamage.Init(baseDamage, this);
        }


        // 3) 可选：确保可见
        var r = p.GetComponentInChildren<Renderer>();
        if (r != null) r.enabled = true;

        // 4) 初始化弹道配置（把 source 也写入 cfg）
        p.Init(new BulletProjectile.Config
        {
            source = this, // ✅ 关键：抛射物命中时就能知道来源枪

            speed = bulletSpeed,
            gravity = bulletGravity,
            lifetime = bulletLifetime,
            maxRange = maxRange,
            baseDamage = baseDamage,
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
