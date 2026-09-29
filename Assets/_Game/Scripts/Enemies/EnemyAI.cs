using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Chase,
        Attack
    }

    [SerializeField] private PlayerHealth target;
    [SerializeField, Min(0f)] private float detectionRange = 8f;
    [SerializeField, Min(0f)] private float attackRange = 1.5f;
    [SerializeField, Min(0f)] private float moveSpeed = 2.75f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    private Rigidbody body;
    private EnemyState state;
    private float nextAttackTime;

    public void ResetAfterRestore()
    {
        body = GetComponent<Rigidbody>();
        state = EnemyState.Idle;
        nextAttackTime = Time.time + attackInterval;
        target = FindFirstObjectByType<PlayerHealth>();
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        if (target == null)
        {
            target = FindFirstObjectByType<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (target == null || target.IsDead)
        {
            state = EnemyState.Idle;
            return;
        }

        float distanceSqr = DistanceToTargetSqr();
        float detectionRangeSqr = detectionRange * detectionRange;
        float attackRangeSqr = attackRange * attackRange;

        switch (state)
        {
            case EnemyState.Idle:
                if (distanceSqr <= detectionRangeSqr && HasLineOfSightToTarget())
                {
                    state = EnemyState.Chase;
                }
                break;

            case EnemyState.Chase:
                if (distanceSqr > detectionRangeSqr * 1.44f || !HasLineOfSightToTarget())
                {
                    state = EnemyState.Idle;
                }
                else if (distanceSqr <= attackRangeSqr)
                {
                    state = EnemyState.Attack;
                }
                break;

            case EnemyState.Attack:
                if (distanceSqr > attackRangeSqr)
                {
                    state = EnemyState.Chase;
                }
                else
                {
                    TryAttack();
                }
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void FixedUpdate()
    {
        if (target == null || target.IsDead)
        {
            return;
        }

        if (state == EnemyState.Idle)
        {
            return;
        }

        Vector3 direction = DirectionToTarget();
        RotateToward(direction);

        if (state != EnemyState.Chase)
        {
            return;
        }

        Vector3 nextPosition = body.position + direction * (moveSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime || target == null || target.IsDead)
        {
            return;
        }

        target.TakeDamage(attackDamage);
        nextAttackTime = Time.time + attackInterval;
    }

    private Vector3 DirectionToTarget()
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void RotateToward(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private float DistanceToTargetSqr()
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private bool HasLineOfSightToTarget()
    {
        if (!requireLineOfSight)
        {
            return true;
        }

        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPoint = target.transform.position + Vector3.up;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<DummyEnemy>() != null)
            {
                continue;
            }

            return hit.collider.GetComponentInParent<PlayerHealth>() == target;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
