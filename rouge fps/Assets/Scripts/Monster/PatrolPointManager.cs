using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PatrolPointManager : MonoBehaviour
{
    public static PatrolPointManager Instance { get; private set; }

    [SerializeField]
    private List<Transform> patrolPoints = new List<Transform>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 如果在编辑器中没有手动赋值，且该物体下有子物体，则自动获取子物体作为巡逻点
        if (patrolPoints.Count == 0 && transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                patrolPoints.Add(child);
            }
        }
    }

    /// <summary>
    /// 获取一个随机的巡逻点
    /// </summary>
    /// <returns>由于可能为空，返回Transform</returns>
    public Transform GetRandomPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Count == 0)
            return null;

        return patrolPoints[Random.Range(0, patrolPoints.Count)];
    }

    /// <summary>
    /// 获取一条由多个随机点组成的路径
    /// </summary>
    /// <param name="pathCount">路径点的数量</param>
    /// <returns>Transform列表</returns>
    public List<Transform> GetRandomPatrolPath(int pathCount = 4)
    {
        if (patrolPoints == null || patrolPoints.Count == 0)
            return new List<Transform>();

        List<Transform> path = new List<Transform>();
        for (int i = 0; i < pathCount; i++)
        {
            path.Add(patrolPoints[Random.Range(0, patrolPoints.Count)]);
        }
        return path;
    }

    /// <summary>
    /// 获取按顺序排列的所有巡逻点（如果是固定路线巡逻）
    /// </summary>
    public Transform[] GetAllPatrolPoints()
    {
        return patrolPoints.ToArray();
    }

    // 可视化巡逻点，方便在Scene视图调试
    private void OnDrawGizmos()
    {
        if (patrolPoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (var point in patrolPoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(point.position, 0.5f);
            }
        }
    }
}