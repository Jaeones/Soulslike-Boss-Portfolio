using UnityEngine;

// IDamageable: 이걸 가진 오브젝트는 다 맞을 수 있다"는 선언
public abstract class CharacterBase : MonoBehaviour, IDamageable
{
    [Header("Base Stats")]
    [SerializeField] protected float moveSpeed = 5f; // 자식들이 고칠 수 있게 protected
    [SerializeField] protected float maxHp = 100f;
    protected float currentHp;

    protected Rigidbody rb;
    protected CapsuleCollider col;

    [SerializeField] protected Animator animator;
    public Animator Anim => animator;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        currentHp = maxHp;
    }

    // 1. 이동 로직 (자식들이 방향만 알려주면 실제로 움직이는 건 부모가 함)
    public virtual void Move(Vector3 direction)
    {
        // Y축(중력) 속도는 유지하면서 X, Z축(이동) 속도만 변경
        Vector3 currentVelocity = rb.velocity;
        Vector3 targetVelocity = direction * moveSpeed;

        // 공중에서도 움직일 수 있게 하려면 Y값 유지 필수
        targetVelocity.y = currentVelocity.y;

        rb.velocity = targetVelocity;

        // 이동하는 방향 바라보기 (회전)
        if (direction.magnitude > 0.1f)
        {
            transform.forward = Vector3.Slerp(transform.forward, direction, Time.deltaTime * 10f);
        }
    }

    // 2. IDamageable 인터페이스 구현 (공통 피격 로직)
    public virtual void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        currentHp -= amount;
        Debug.Log($"{gameObject.name} 피격! 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        // 자식 클래스에서 오버라이딩(덮어쓰기)해서 애니메이션 등을 추가할 예정
        Debug.Log($"{gameObject.name} 사망");
        // Destroy(gameObject); // 일단은 주석 처리 (테스트용)
    }
}