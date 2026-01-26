using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public enum Part { Body, Head }

    [Header("Part")]
    public Part part = Part.Body;

    [Header("Damage Multiplier")]
    [Min(0f)] public float damageMultiplier = 1f;
}
