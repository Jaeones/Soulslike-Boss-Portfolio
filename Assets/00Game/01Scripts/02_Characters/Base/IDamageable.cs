using UnityEngine;

public interface IDamageable
{
    // hitPoint: 맞은 위치 (이펙트 생성용)
    // hitNormal: 맞은 각도 (피 튀기는 방향용)
    void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitNormal);
}