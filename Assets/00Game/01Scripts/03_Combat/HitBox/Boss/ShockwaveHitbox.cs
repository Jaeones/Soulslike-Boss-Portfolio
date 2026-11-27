using UnityEngine;

public class ShockwaveHitbox : BossHitbox
{
    [Header("Shockwave Settings")]
    public float speed = 10f;
    public float lifeTime = 3f;

    private void OnEnable()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }
}