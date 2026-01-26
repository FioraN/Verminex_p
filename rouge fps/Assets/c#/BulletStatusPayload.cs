using UnityEngine;

public class BulletStatusPayload : MonoBehaviour
{
    [System.Serializable]
    public class StatusEntry
    {
        public StatusType type = StatusType.Burn;

        [Min(1)] public int stacksToAdd = 1;
        [Min(0.01f)] public float duration = 3f;

        // Burn
        [Min(0f)] public float tickInterval = 1f;
        [Min(0f)] public float burnDamagePerTickPerStack = 0f;

        // Slow
        [Min(0f)] public float slowPerStack = 0f;

        // Poison(Weaken)
        [Min(0f)] public float weakenPerStack = 0f;

        // Shock-A
        [Min(0f)] public float shockChainDamagePerStack = 0f;
        [Min(0.1f)] public float shockChainRadius = 6f;
        [Min(1)] public int shockMaxChains = 2;
    }

    [Header("Status Entries applied on hit")]
    public StatusEntry[] entries;
}
