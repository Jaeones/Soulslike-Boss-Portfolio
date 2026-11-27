using UnityEngine;

public interface IParryResponder
{
    bool OnParried(CombatAttackDefinition incomingAttack, Transform defender, Vector3 hitPoint, Vector3 hitNormal);
}
