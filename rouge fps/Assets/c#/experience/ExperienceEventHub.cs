using System;
using UnityEngine;

public static class ExperienceEventHub
{
    public struct OrbCollectedEvent
    {
        public PlayerExperience receiver;
        public ExperienceOrb orb;
        public int experienceValue;
        public Vector3 worldPosition;
        public float time;
    }

    public static event Action<OrbCollectedEvent> OnOrbCollected;

    public static void RaiseOrbCollected(in OrbCollectedEvent e) => OnOrbCollected?.Invoke(e);
}
