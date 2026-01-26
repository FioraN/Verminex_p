using UnityEngine;
using PrototypeFPC;

public class GunRecoil : MonoBehaviour
{
    [Header("Look Reference")]
    [Tooltip("Assign the Perspective component here (PrototypeFPC).")]
    public Perspective look;

    [Header("Recoil (degrees)")]
    [Min(0f)] public float kickPitchPerShot = 1.2f; // view goes up
    [Min(0f)] public float kickYawRandom = 0.6f;    // left/right random

    private void Awake()
    {
        if (look == null)
            look = GetComponentInParent<Perspective>();
    }

    public void Kick()
    {
        if (look == null) return;

        float yaw = Random.Range(-kickYawRandom, kickYawRandom);
        look.AddRecoil(kickPitchPerShot, yaw);
    }
}
