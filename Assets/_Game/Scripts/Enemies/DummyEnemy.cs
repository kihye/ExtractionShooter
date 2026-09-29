using System.Collections;
using UnityEngine;

public sealed class DummyEnemy : MonoBehaviour, IDamageable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField, Min(1f)] private float maxHp = 100f;
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;

    private float currentHp;
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color originalColor = Color.gray;
    private Coroutine flashRoutine;
    private EnemyDrop enemyDrop;

    public float CurrentHealth => currentHp;
    public float MaxHealth => maxHp;

    private void Awake()
    {
        currentHp = maxHp;
        enemyDrop = GetComponent<EnemyDrop>();
        cachedRenderer = GetComponentInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (cachedRenderer != null && cachedRenderer.sharedMaterial != null)
        {
            Material material = cachedRenderer.sharedMaterial;
            if (material.HasProperty(BaseColorId))
            {
                originalColor = material.GetColor(BaseColorId);
            }
            else if (material.HasProperty(ColorId))
            {
                originalColor = material.GetColor(ColorId);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentHp <= 0f)
        {
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - damage);
        Debug.Log($"{name} took {damage:0.#} damage. HP: {currentHp:0.#}/{maxHp:0.#}", this);

        if (currentHp <= 0f)
        {
            enemyDrop?.DropItems();
            Destroy(gameObject);
            return;
        }

        FlashHitColor();
    }

    public void RestoreHealth(float health)
    {
        currentHp = Mathf.Clamp(health, 0f, maxHp);
        if (currentHp <= 0f)
        {
            gameObject.SetActive(false);
        }
    }

    private void FlashHitColor()
    {
        if (cachedRenderer == null || hitFlashDuration <= 0f)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SetRendererColor(hitFlashColor);
        yield return new WaitForSeconds(hitFlashDuration);
        SetRendererColor(originalColor);
        flashRoutine = null;
    }

    private void SetRendererColor(Color color)
    {
        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
