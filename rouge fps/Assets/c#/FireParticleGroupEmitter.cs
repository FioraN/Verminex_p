using Knife.Effects;
using UnityEngine;

public sealed class FireParticleGroupEmitter : MonoBehaviour
{
    [Header("Emitter")]
    [SerializeField] private ParticleGroupEmitter[] emitters;
    [SerializeField] [Min(1)] private int emitCount = 1;
    [SerializeField] private bool emitAtSourceFirePoint = true;
    [SerializeField] private bool matchSourceFirePointRotation = true;
    [SerializeField] private bool parentEmitterToSourceFirePoint = true;

    [Header("Filter")]
    [SerializeField] private CameraGunChannel[] sourceFilter;
    [SerializeField] private bool hitscanOnly = false;
    [SerializeField] private bool projectileOnly = false;

    private void Awake()
    {
        if (emitters == null || emitters.Length == 0)
            emitters = GetComponentsInChildren<ParticleGroupEmitter>(true);
    }

    private void OnEnable()
    {
        CombatEventHub.OnFire += HandleFire;
    }

    private void OnDisable()
    {
        CombatEventHub.OnFire -= HandleFire;
    }

    private void HandleFire(CombatEventHub.FireEvent e)
    {
        if (!PassesSourceFilter(e.source))
            return;

        if (hitscanOnly && e.isProjectile)
            return;

        if (projectileOnly && !e.isProjectile)
            return;

        if (emitters == null)
            return;

        for (int i = 0; i < emitters.Length; i++)
        {
            var emitter = emitters[i];
            if (emitter == null)
                continue;

            AlignEmitterToSourceFirePoint(emitter, e.source);
            emitter.Emit(emitCount);
        }
    }

    private bool PassesSourceFilter(CameraGunChannel source)
    {
        if (sourceFilter == null || sourceFilter.Length == 0)
            return true;

        for (int i = 0; i < sourceFilter.Length; i++)
        {
            if (sourceFilter[i] == source)
                return true;
        }

        return false;
    }

    private void AlignEmitterToSourceFirePoint(ParticleGroupEmitter emitter, CameraGunChannel source)
    {
        if (!emitAtSourceFirePoint || emitter == null || source == null || source.firePoint == null)
            return;

        if (parentEmitterToSourceFirePoint)
        {
            emitter.transform.SetParent(source.firePoint, worldPositionStays: false);
            emitter.transform.localPosition = Vector3.zero;

            if (matchSourceFirePointRotation)
                emitter.transform.localRotation = Quaternion.identity;
        }
        else
        {
            emitter.transform.position = source.firePoint.position;

            if (matchSourceFirePointRotation)
                emitter.transform.rotation = source.firePoint.rotation;
        }
    }
}
