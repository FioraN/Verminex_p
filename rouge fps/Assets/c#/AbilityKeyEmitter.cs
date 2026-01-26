using UnityEngine;

public class AbilityKeyEmitter : MonoBehaviour
{
    public KeyCode abilityKey = KeyCode.F;

    private void Update()
    {
        if (Input.GetKeyDown(abilityKey))
        {
            CombatEventHub.RaiseAbility(new CombatEventHub.AbilityEvent
            {
                key = abilityKey,
                time = Time.time
            });
        }
    }
}
