using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyBase : MonoBehaviour, IDamageable, IParryResponder
{
    public enum State { Intro, Idle, Move, Action, HitReact, Groggy, Dead }
    public State currentState = State.Intro;

    [Header("Base Stats")]
    public Transform target;
    public float hp = 5000f;

    [Header("Groggy & Super Armor")]
    public float maxGroggyGauge = 100f;

    protected float currentGroggyGauge = 0f;
    public float groggyDuration = 3.0f;

    protected Animator anim;
    protected NavMeshAgent agent;

    public float MaxHp { get; private set; }
    public float CurrentHp => hp;
    public float CurrentGroggyGauge => currentGroggyGauge;

    // 일반적으로는 체력이 0이거나 상태가 Dead라면 죽은 것으로 판정합니다.
    // 일부 보스는 HP 0을 2페이즈 전환 연출로 소비할 수 있으므로 훅을 둡니다.
    public bool IsDead => currentState == State.Dead || (hp <= 0f && ShouldTreatZeroHpAsDead());

    public event Action<float, float> OnHpChanged;
    public event Action OnDied;

    protected virtual void Awake()
    {
        MaxHp = hp;
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (target == null)
            target = GameObject.FindWithTag("Player")?.transform;

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.angularSpeed = 0f;
        }
    }

    protected virtual void Start()
    {
        StartCoroutine(FSM());
        CallHpChanged();
    }

    public void CallHpChanged()
    {
        OnHpChanged?.Invoke(hp, MaxHp);
    }

    protected virtual bool ShouldTreatZeroHpAsDead()
    {
        return true;
    }

    protected void SetHealthValues(float currentHp, float maxHp, bool notify = true)
    {
        MaxHp = Mathf.Max(1f, maxHp);
        hp = Mathf.Clamp(currentHp, 0f, MaxHp);

        if (notify)
        {
            CallHpChanged();
        }
    }

    protected IEnumerator FSM()
    {
        // currentState가 Dead가 되는 순간 이 while 문은 즉시 종료됩니다.
        while (currentState != State.Dead)
        {
            switch (currentState)
            {
                case State.Intro: yield return StartCoroutine(Intro()); break;
                case State.Idle: yield return StartCoroutine(Idle()); break;
                case State.Move: yield return StartCoroutine(Move()); break;
                case State.Action: yield return StartCoroutine(Action()); break;
                case State.HitReact: yield return StartCoroutine(HitReact()); break;
                case State.Groggy: yield return StartCoroutine(Groggy()); break;
            }
            yield return null;
        }

        // [이곳이 실행되어야 합니다] 루프 탈출 후 사망 연출 시작 
        Debug.Log("<color=red><b>[System]</b></color> FSM 루프 탈출, 사망 연출 시작!");
        yield return StartCoroutine(Dead());
    }

    public virtual void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead)
        {
            return;
        }

        hp = Mathf.Max(0f, hp - amount);
        CallHpChanged();

        if (hp <= 0f)
        {
            Die();
            return;
        }

        ApplyIncomingCombatPressure(amount, CombatDamageContext.CurrentAttack);

        if (ShouldEnterGroggyState())
        {
            currentState = State.Groggy;
        }
    }

    protected virtual bool CanEnterGroggyState()
    {
        return currentState != State.Groggy && currentState != State.Dead;
    }

    protected virtual void ApplyIncomingCombatPressure(int amount, CombatAttackDefinition incomingAttack)
    {
        currentGroggyGauge += ResolveGroggyGain(amount, incomingAttack);
    }

    protected virtual float ResolveGroggyGain(int amount, CombatAttackDefinition incomingAttack)
    {
        if (incomingAttack != null && incomingAttack.groggyDamage > 0f)
        {
            return incomingAttack.groggyDamage;
        }

        return amount * 0.2f;
    }

    protected virtual bool ShouldEnterGroggyState()
    {
        return currentGroggyGauge >= maxGroggyGauge && CanEnterGroggyState();
    }

    protected void ForceGroggyGaugeToMax()
    {
        currentGroggyGauge = maxGroggyGauge;
    }

    protected void ResetGroggyGauge()
    {
        currentGroggyGauge = 0f;
    }

    public virtual bool OnParried(CombatAttackDefinition incomingAttack, Transform defender, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead)
        {
            return false;
        }

        float parryGroggyGain = Mathf.Max(12f, maxGroggyGauge * 0.45f);
        if (maxGroggyGauge > 0f)
        {
            currentGroggyGauge = Mathf.Min(maxGroggyGauge, currentGroggyGauge + parryGroggyGain);
        }

        if (ShouldEnterGroggyState())
        {
            currentState = State.Groggy;
            return true;
        }

        if (currentState != State.Dead && currentState != State.Groggy)
        {
            currentState = State.HitReact;
            return true;
        }

        return false;
    }

    protected virtual void Die()
    {
        // 중복 실행 방지
        if (currentState == State.Dead) return;

        currentState = State.Dead;

        // [핵심] 현재 실행 중인 모든 코루틴(공격, 이동 등)을 즉시 강제 종료
        // 이 명령이 있어야 Action 루프에서 빠져나와 FSM의 Dead() 코루틴으로 넘어갑니다.
        StopAllCoroutines();

        // 멈춘 후 다시 FSM을 시작하거나, 직접 Dead 코루틴을 실행해야 합니다.
        StartCoroutine(Dead());

        OnDied?.Invoke();
        Debug.Log($"<color=red><b>[System]</b></color> {gameObject.name} 사망 처리 및 모든 동작 정지.");
    }

    public void RotateToTarget()
    {
        if (target == null) return;
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    protected abstract IEnumerator Intro();
    protected abstract IEnumerator Idle();
    protected abstract IEnumerator Move();
    protected abstract IEnumerator Action();
    protected abstract IEnumerator HitReact();
    protected abstract IEnumerator Groggy();
    protected abstract IEnumerator Dead();
}
