using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns monsters on configured spawn points only when the point is outside the player's view.
/// Spawned monsters are forced into aggro and given a large chase range so they keep pursuing the player.
/// </summary>
public class MonsterSpawnDirector : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private bool useChildrenAsSpawnPoints = true;
    [SerializeField] private Transform spawnedMonsterRoot;

    [Header("Player References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Camera playerCamera;

    [Header("Spawn Timing")]
    [Min(0f)] [SerializeField] private float initialDelay = 1f;
    [Min(0.05f)] [SerializeField] private float spawnInterval = 3f;
    [Min(1)] [SerializeField] private int spawnCountPerCycle = 1;
    [Min(1)] [SerializeField] private int maxAliveMonsters = 8;

    [Header("Difficulty Ramp")]
    [SerializeField] private bool enableProgressiveSpawnScaling = true;
    [Min(0f)] [SerializeField] private float maxAliveIncreasePerMinute = 1f;
    [Min(0f)] [SerializeField] private float spawnRateIncreasePercentPerMinute = 2f;
    [Min(0.05f)] [SerializeField] private float minimumSpawnInterval = 0.35f;

    [Header("Monster Stat Ramp")]
    [SerializeField] private bool scaleSpawnedMonsterStatsOverTime = true;
    [Min(0.1f)] [SerializeField] private float statIncreaseIntervalSeconds = 30f;
    [Min(0f)] [SerializeField] private float healthIncreasePerInterval = 10f;
    [Min(0f)] [SerializeField] private float armorIncreasePerInterval = 5f;

    [Header("Armor Mode Ramp")]
    [SerializeField] private bool convertSpawnedArmorToRegenAfterTime = false;
    [Min(0f)] [SerializeField] private float armorBecomesRegenAfterSeconds = 180f;

    [Header("Spawn Visibility")]
    [SerializeField] private bool requireSpawnPointOutsideView = true;
    [SerializeField] private bool requireClearLineOfSightToCountAsVisible = true;
    [SerializeField] private LayerMask visibilityBlockerMask = ~0;
    [Min(0f)] [SerializeField] private float visibilityCheckRadius = 0.25f;

    [Header("Spawn Distance")]
    [SerializeField] private bool requireSpawnPointWithinPlayerDistance = true;
    [Min(0f)] [SerializeField] private float maxSpawnDistanceFromPlayer = 30f;

    [Header("Spawn Position")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [Min(0.1f)] [SerializeField] private float navMeshSampleRadius = 2f;

    [Header("Spawned Monster State")]
    [SerializeField] private bool disablePatrolOnSpawn = true;
    [Min(0f)] [SerializeField] private float forcedAggroChaseRange = 9999f;

    private readonly List<MonsterBase> _aliveMonsters = new List<MonsterBase>();
    private Coroutine _spawnRoutine;
    private float _spawnLoopStartedAt;

    private void Awake()
    {
        ResolveReferences();
        CollectChildSpawnPointsIfNeeded();
    }

    private void OnEnable()
    {
        if (_spawnRoutine == null)
            _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    private void ResolveReferences()
    {
        if (playerRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerRoot = player.transform;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void CollectChildSpawnPointsIfNeeded()
    {
        if (!useChildrenAsSpawnPoints || spawnPoints.Count > 0)
            return;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
                spawnPoints.Add(child);
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        _spawnLoopStartedAt = Time.time;

        while (enabled)
        {
            TrySpawnCycle();
            yield return new WaitForSeconds(GetCurrentSpawnInterval());
        }
    }

    private void TrySpawnCycle()
    {
        CleanupAliveMonsters();

        if (monsterPrefabs.Count == 0 || spawnPoints.Count == 0)
            return;

        int availableSlots = Mathf.Max(0, GetCurrentMaxAliveMonsters() - _aliveMonsters.Count);
        if (availableSlots <= 0)
            return;

        int spawnCount = Mathf.Min(spawnCountPerCycle, availableSlots);
        for (int i = 0; i < spawnCount; i++)
        {
            if (!TrySpawnOneMonster())
                break;
        }
    }

    private bool TrySpawnOneMonster()
    {
        List<Transform> candidates = GetValidSpawnPoints();
        if (candidates.Count == 0)
            return false;

        Transform spawnPoint = candidates[Random.Range(0, candidates.Count)];
        GameObject prefab = GetRandomMonsterPrefab();
        if (spawnPoint == null || prefab == null)
            return false;

        Vector3 spawnPosition = spawnPoint.position + spawnOffset;
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            spawnPosition = hit.position;

        Transform parent = spawnedMonsterRoot != null ? spawnedMonsterRoot : null;
        GameObject instance = Instantiate(prefab, spawnPosition, spawnPoint.rotation, parent);
        MonsterBase monster = instance.GetComponent<MonsterBase>();
        if (monster == null)
            monster = instance.GetComponentInChildren<MonsterBase>();

        if (monster != null)
        {
            monster.hasAggro = true;
            monster.playerTransform = playerRoot;
            monster.chaseRange = Mathf.Max(monster.chaseRange, forcedAggroChaseRange);

            if (disablePatrolOnSpawn)
                monster.isCanPatrol = false;

            ApplyProgressiveStats(instance);

            _aliveMonsters.Add(monster);
        }

        return true;
    }

    private List<Transform> GetValidSpawnPoints()
    {
        List<Transform> validPoints = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform point = spawnPoints[i];
            if (point == null)
                continue;

            if (requireSpawnPointWithinPlayerDistance && playerRoot != null)
            {
                float distanceToPlayer = Vector3.Distance(point.position, playerRoot.position);
                if (distanceToPlayer > maxSpawnDistanceFromPlayer)
                    continue;
            }

            if (requireSpawnPointOutsideView && IsPointVisibleToPlayer(point.position))
                continue;

            validPoints.Add(point);
        }

        return validPoints;
    }

    private int GetCurrentMaxAliveMonsters()
    {
        int baseLimit = Mathf.Max(1, maxAliveMonsters);
        if (!enableProgressiveSpawnScaling)
            return baseLimit;

        float elapsedMinutes = GetElapsedMinutesSinceSpawnLoopStart();
        int extraLimit = Mathf.FloorToInt(Mathf.Max(0f, elapsedMinutes * maxAliveIncreasePerMinute));
        return Mathf.Max(1, baseLimit + extraLimit);
    }

    private float GetCurrentSpawnInterval()
    {
        float baseInterval = Mathf.Max(0.05f, spawnInterval);
        if (!enableProgressiveSpawnScaling)
            return baseInterval;

        float elapsedMinutes = GetElapsedMinutesSinceSpawnLoopStart();
        float frequencyMultiplier = 1f + Mathf.Max(0f, elapsedMinutes * (spawnRateIncreasePercentPerMinute / 100f));
        float scaledInterval = baseInterval / Mathf.Max(0.01f, frequencyMultiplier);
        return Mathf.Max(minimumSpawnInterval, scaledInterval);
    }

    private float GetElapsedMinutesSinceSpawnLoopStart()
    {
        if (_spawnLoopStartedAt <= 0f)
            return 0f;

        return Mathf.Max(0f, Time.time - _spawnLoopStartedAt) / 60f;
    }

    private int GetElapsedStatRampSteps()
    {
        if (!scaleSpawnedMonsterStatsOverTime)
            return 0;

        if (_spawnLoopStartedAt <= 0f)
            return 0;

        float safeInterval = Mathf.Max(0.1f, statIncreaseIntervalSeconds);
        float elapsedSeconds = Mathf.Max(0f, Time.time - _spawnLoopStartedAt);
        return Mathf.FloorToInt(elapsedSeconds / safeInterval);
    }

    private void ApplyProgressiveStats(GameObject spawnedInstance)
    {
        if (spawnedInstance == null)
            return;

        int steps = GetElapsedStatRampSteps();
        if (steps <= 0)
            return;

        float totalHealthBonus = Mathf.Max(0f, healthIncreasePerInterval) * steps;
        float totalArmorBonus = Mathf.Max(0f, armorIncreasePerInterval) * steps;

        MonsterHealth health = spawnedInstance.GetComponentInChildren<MonsterHealth>();
        if (health != null && totalHealthBonus > 0f)
        {
            health.maxHp = Mathf.Max(1f, health.maxHp + totalHealthBonus);
            health.hp = Mathf.Clamp(health.hp + totalHealthBonus, 0f, health.maxHp);
        }

        EnemyArmor armor = spawnedInstance.GetComponentInChildren<EnemyArmor>();
        if (armor != null && totalArmorBonus > 0f)
        {
            armor.maxArmor = Mathf.Max(0f, armor.maxArmor + totalArmorBonus);
            armor.armor = Mathf.Clamp(armor.armor + totalArmorBonus, 0f, armor.maxArmor);
            armor.restoreArmorAmount = Mathf.Clamp(armor.restoreArmorAmount + totalArmorBonus, 0f, armor.maxArmor);
        }

        if (armor != null && ShouldUseRegenArmorForNewSpawn())
        {
            armor.mode = EnemyArmor.ArmorMode.Regen;
        }
    }

    private bool ShouldUseRegenArmorForNewSpawn()
    {
        if (!convertSpawnedArmorToRegenAfterTime)
            return false;

        if (_spawnLoopStartedAt <= 0f)
            return false;

        return Time.time - _spawnLoopStartedAt >= Mathf.Max(0f, armorBecomesRegenAfterSeconds);
    }

    private bool IsPointVisibleToPlayer(Vector3 worldPoint)
    {
        if (playerCamera == null)
            return false;

        Vector3 viewport = playerCamera.WorldToViewportPoint(worldPoint);
        bool insideViewport =
            viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;

        if (!insideViewport)
            return false;

        if (!requireClearLineOfSightToCountAsVisible)
            return true;

        Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);

        Bounds pointBounds = new Bounds(worldPoint, Vector3.one * Mathf.Max(visibilityCheckRadius, 0.01f));
        if (!GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, pointBounds))
            return false;

        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 direction = worldPoint - cameraPos;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        direction /= distance;

        if (Physics.Raycast(cameraPos, direction, out RaycastHit hit, distance, visibilityBlockerMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.transform;
            bool hitIsPlayer = playerRoot != null && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot));
            if (hitTransform != null && !hitIsPlayer)
                return false;
        }

        return true;
    }

    private GameObject GetRandomMonsterPrefab()
    {
        int validCount = 0;
        for (int i = 0; i < monsterPrefabs.Count; i++)
        {
            if (monsterPrefabs[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < monsterPrefabs.Count; i++)
        {
            if (monsterPrefabs[i] == null)
                continue;

            if (pick == 0)
                return monsterPrefabs[i];

            pick--;
        }

        return null;
    }

    private void CleanupAliveMonsters()
    {
        for (int i = _aliveMonsters.Count - 1; i >= 0; i--)
        {
            MonsterBase monster = _aliveMonsters[i];
            if (monster == null)
            {
                _aliveMonsters.RemoveAt(i);
                continue;
            }

            MonsterHealth health = monster.GetComponent<MonsterHealth>();
            if (health != null && health.IsDead)
            {
                _aliveMonsters.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 1f, 0.75f);

        List<Transform> pointsToDraw = new List<Transform>();
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            pointsToDraw.AddRange(spawnPoints);
        }
        else if (useChildrenAsSpawnPoints)
        {
            for (int i = 0; i < transform.childCount; i++)
                pointsToDraw.Add(transform.GetChild(i));
        }

        for (int i = 0; i < pointsToDraw.Count; i++)
        {
            Transform point = pointsToDraw[i];
            if (point == null)
                continue;

            Vector3 position = point.position + spawnOffset;
            Gizmos.DrawWireSphere(position, 0.35f);
            Gizmos.DrawLine(position, position + point.forward * 0.75f);
        }
    }
}
