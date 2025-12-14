using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 20f;
    private float currentHealth;

    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        currentHealth -= amount;

        // Optional: play hit flash / animation here

        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    private void Die()
    {
        isDead = true;

        // Optional: disable AI, play death animation
        // For now, just destroy
        Destroy(gameObject);
    }
}
