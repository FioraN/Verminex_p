using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PerkManager : MonoBehaviour
{
    [Header("Guns (drag these)")]
    [SerializeField] private GameObject gun1;
    [SerializeField] private GameObject gun2;

    [Header("Gun Control Root (optional)")]
    [SerializeField] private GameObject gunControlRoot;

    [Header("Selected Perks (GunA / GunB)")]
    public List<MonoBehaviour> selectedPerksGunA = new();
    public List<MonoBehaviour> selectedPerksGunB = new();

    [Header("Auto Refresh")]
    [SerializeField] private bool autoRefreshInPlayMode = true;

    [Header("Optional: Filter")]
    [Tooltip("If set, the manager will prefer components under this child name (per gun). Leave empty to disable.")]
    [SerializeField] private string preferChildNameForGunSearch = "";

    [Serializable]
    public sealed class GunRefs
    {
        public GameObject root;

        public CameraGunChannel cameraGunChannel;
        public GunAmmo gunAmmo;
        public GunRecoil gunRecoil;
        public GunSpread gunSpread;
        public SpreadDiamondUI spreadDiamondUI;
        public AutoAimLockOn autoAimLock;
        public ShockChainProc shockChainProc;

        public bool IsComplete()
        {
            return root != null && cameraGunChannel != null;
        }

        public void Clear()
        {
            root = null;
            cameraGunChannel = null;
            gunAmmo = null;
            gunRecoil = null;
            gunSpread = null;
            spreadDiamondUI = null;
            autoAimLock = null;
            shockChainProc = null;
        }
    }

    [Serializable]
    public sealed class ControlRefs
    {
        public GameObject root;

        public AbilityKeyEmitter abilityKeyEmitter;
        public AmmoDualUI ammoDualUI;
        public CameraGunDual cameraGunDual;
        public AimScopeController aimScopeController;
        public MarkManager markManager;

        public bool IsComplete()
        {
            return root != null && cameraGunDual != null;
        }

        public void Clear()
        {
            root = null;
            abilityKeyEmitter = null;
            ammoDualUI = null;
            cameraGunDual = null;
            aimScopeController = null;
            markManager = null;
        }
    }

    [Header("Read-only cached refs")]
    public GunRefs gunARefs = new();
    public GunRefs gunBRefs = new();
    public ControlRefs controlRefs = new();

    public event Action RefsRefreshed;

    public GameObject GunAObject => gun1;
    public GameObject GunBObject => gun2;
    public GameObject GunControlRoot => gunControlRoot;

    public GunRefs GunA => gunARefs;
    public GunRefs GunB => gunBRefs;
    public ControlRefs Control => controlRefs;

    private int _lastSignature;

    private void Awake()
    {
        RefreshAll(force: true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshAll(force: false);
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!autoRefreshInPlayMode) return;

        int sig = ComputeSignature();
        if (sig != _lastSignature)
            RefreshAll(force: false);
    }

    private int ComputeSignature()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (gun1 != null ? gun1.GetInstanceID() : 0);
            h = h * 31 + (gun2 != null ? gun2.GetInstanceID() : 0);
            h = h * 31 + (gunControlRoot != null ? gunControlRoot.GetInstanceID() : 0);
            h = h * 31 + (autoRefreshInPlayMode ? 1 : 0);
            h = h * 31 + (preferChildNameForGunSearch != null ? preferChildNameForGunSearch.GetHashCode() : 0);
            return h;
        }
    }

    public void RefreshAll(bool force)
    {
        EnsureGunControlRoot(force);

        RefreshGunRefs(gun1, gunARefs, force, preferChildNameForGunSearch);
        RefreshGunRefs(gun2, gunBRefs, force, preferChildNameForGunSearch);

        RefreshControlRefs(gunControlRoot, controlRefs, force);

        _lastSignature = ComputeSignature();
        RefsRefreshed?.Invoke();
    }

    private static void RefreshGunRefs(GameObject gunObj, GunRefs refs, bool force, string preferChildName)
    {
        bool needsRefresh =
            force ||
            refs.root != gunObj ||
            (gunObj != null && !refs.IsComplete());

        if (!needsRefresh) return;

        refs.Clear();
        refs.root = gunObj;
        if (gunObj == null) return;

        Transform searchRoot = gunObj.transform;

        if (!string.IsNullOrWhiteSpace(preferChildName))
        {
            var child = FindChildRecursive(searchRoot, preferChildName);
            if (child != null) searchRoot = child;
        }

        refs.cameraGunChannel = searchRoot.GetComponentInChildren<CameraGunChannel>(true);
        refs.gunAmmo = searchRoot.GetComponentInChildren<GunAmmo>(true);
        refs.gunRecoil = searchRoot.GetComponentInChildren<GunRecoil>(true);
        refs.gunSpread = searchRoot.GetComponentInChildren<GunSpread>(true);
        refs.spreadDiamondUI = searchRoot.GetComponentInChildren<SpreadDiamondUI>(true);
        refs.autoAimLock = searchRoot.GetComponentInChildren<AutoAimLockOn>(true);
        refs.shockChainProc = searchRoot.GetComponentInChildren<ShockChainProc>(true);
    }

    private void EnsureGunControlRoot(bool force)
    {
        if (gunControlRoot != null) return;

        var dual = FindFirstObjectByType<CameraGunDual>();
        if (dual != null)
        {
            gunControlRoot = dual.gameObject;
            return;
        }

        var mark = FindFirstObjectByType<MarkManager>();
        if (mark != null)
        {
            gunControlRoot = mark.gameObject;
            return;
        }

        var existing = transform.Find("GunControlRoot");
        if (existing == null)
        {
            var go = new GameObject("GunControlRoot");
            go.transform.SetParent(transform, worldPositionStays: false);
            gunControlRoot = go;
        }
        else
        {
            gunControlRoot = existing.gameObject;
        }

        if (force)
        {
            controlRefs.Clear();
            controlRefs.root = gunControlRoot;
        }
    }

    private static void RefreshControlRefs(GameObject rootObj, ControlRefs refs, bool force)
    {
        bool needsRefresh =
            force ||
            refs.root != rootObj ||
            (rootObj != null && !refs.IsComplete());

        if (!needsRefresh) return;

        refs.Clear();
        refs.root = rootObj;
        if (rootObj == null) return;

        refs.abilityKeyEmitter = rootObj.GetComponentInChildren<AbilityKeyEmitter>(true);
        refs.ammoDualUI = rootObj.GetComponentInChildren<AmmoDualUI>(true);
        refs.cameraGunDual = rootObj.GetComponentInChildren<CameraGunDual>(true);
        refs.aimScopeController = rootObj.GetComponentInChildren<AimScopeController>(true);
        refs.markManager = rootObj.GetComponentInChildren<MarkManager>(true);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // -----------------------------
    // Perk lists (GunA / GunB)
    // -----------------------------

    public List<MonoBehaviour> GetPerkList(int gunIndex)
    {
        return gunIndex == 0 ? selectedPerksGunA : selectedPerksGunB;
    }

    public GunRefs GetGun(int gunIndex)
    {
        return gunIndex == 0 ? gunARefs : gunBRefs;
    }

    public bool HasPerk(string perkId, int gunIndex)
    {
        if (string.IsNullOrWhiteSpace(perkId)) return false;

        var list = GetPerkList(gunIndex);
        for (int i = 0; i < list.Count; i++)
        {
            var mb = list[i];
            if (mb == null) continue;

            var meta = mb.GetComponent<PerkMeta>();
            if (meta != null)
            {
                if (meta.EffectiveId == perkId) return true;
            }
            else
            {
                if (mb.GetType().Name == perkId) return true;
            }
        }
        return false;
    }

    public bool PrerequisitesMet(GameObject perkObject, int gunIndex)
    {
        if (perkObject == null) return false;

        var meta = perkObject.GetComponent<PerkMeta>();
        if (meta == null) return true;

        var req = meta.requiredPerkIds;
        if (req == null || req.Count == 0) return true;

        for (int i = 0; i < req.Count; i++)
        {
            var id = req[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            // By default: prerequisites are checked within the same gun list
            if (!HasPerk(id, gunIndex)) return false;
        }

        return true;
    }

    private static string GetPerkIdFromObject(GameObject perkObject)
    {
        if (perkObject == null) return "";
        var meta = perkObject.GetComponent<PerkMeta>();
        if (meta != null) return meta.EffectiveId;
        return perkObject.name;
    }

    public bool TryAddPerkInstanceToGun(MonoBehaviour perkInstance, int gunIndex)
    {
        if (perkInstance == null) return false;

        var list = GetPerkList(gunIndex);

        // Dedup by perk id (meta preferred), else type name
        string id = "";
        var meta = perkInstance.GetComponent<PerkMeta>();
        if (meta != null) id = meta.EffectiveId;
        if (string.IsNullOrWhiteSpace(id)) id = perkInstance.GetType().Name;

        if (HasPerk(id, gunIndex)) return false;

        if (!PrerequisitesMet(perkInstance.gameObject, gunIndex)) return false;

        list.Add(perkInstance);
        return true;
    }

    public MonoBehaviour InstantiatePerkToGun(GameObject perkPrefab, int gunIndex, Transform parent)
    {
        if (perkPrefab == null) return null;

        var p = parent != null ? parent : transform;
        var inst = Instantiate(perkPrefab, p);

        // Find a perk logic component to register (first MonoBehaviour excluding PerkMeta)
        var behaviours = inst.GetComponents<MonoBehaviour>();
        MonoBehaviour perkLogic = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;
            if (b is PerkMeta) continue;
            perkLogic = b;
            break;
        }

        if (perkLogic == null)
        {
            Destroy(inst);
            return null;
        }

        // Optional: set a target gun index field if present (no interface required)
        var t = perkLogic.GetType();
        var field = t.GetField("targetGunIndex");
        if (field != null && field.FieldType == typeof(int))
        {
            field.SetValue(perkLogic, gunIndex);
        }

        if (!TryAddPerkInstanceToGun(perkLogic, gunIndex))
        {
            Destroy(inst);
            return null;
        }

        return perkLogic;
    }

    public int GetPerkTier(GameObject perkObject)
    {
        if (perkObject == null) return 1;
        var meta = perkObject.GetComponent<PerkMeta>();
        if (meta == null) return 1;
        return Mathf.Clamp(meta.perkTier, 1, 2);
    }

    public bool HasValidRefs()
    {
        if (gun1 == null || gun2 == null) return false;
        if (!gunARefs.IsComplete() || !gunBRefs.IsComplete()) return false;
        return true;
    }
}
