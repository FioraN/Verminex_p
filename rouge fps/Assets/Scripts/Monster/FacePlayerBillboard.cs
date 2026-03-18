using UnityEngine;

public class FacePlayerBillboard : MonoBehaviour
{
    public Transform target;
    public bool keepUpright = true;
    public Vector3 rotationOffsetEuler;

    private void LateUpdate()
    {
        Transform currentTarget = ResolveTarget();
        if (currentTarget == null)
            return;

        Vector3 lookPosition = currentTarget.position;
        if (keepUpright)
            lookPosition.y = transform.position.y;

        Vector3 direction = lookPosition - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = lookRotation * Quaternion.Euler(rotationOffsetEuler);
    }

    private Transform ResolveTarget()
    {
        if (target != null)
            return target;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            target = playerObject.transform;

        return target;
    }
}
