using UnityEngine;

[CreateAssetMenu(fileName = "EstusFlaskData", menuName = "Data/Items/Estus Flask Data")]
public class EstusFlaskData : ScriptableObject
{
    [Header("횟수")]
    [Min(0)] public int maxUses = 3;

    [Header("회복량")]
    public bool usePercentHeal = false;
    [Min(1f)] public float healAmount = 250f;
    [Range(0.01f, 1f)] public float healPercent = 0.35f;

    [Header("타이밍")]
    [Min(0.05f)] public float drinkDuration = 1.0f;
    [Min(0f)] public float healApplyTime = 0.55f;
    [Min(0f)] public float cooldown = 0.25f;
}
