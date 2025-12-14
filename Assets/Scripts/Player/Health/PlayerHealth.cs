using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }

    [Header("UI (Optional)")]
    [SerializeField] private TMP_Text healthText;       // e.g. "100/100"
    [SerializeField] private CanvasGroup damageFlashCanvasGroup;        // Full-screen red image with CanvasGroup
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Feedback (optional")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip deathClip;

    [Header("Death")]
    [SerializeField] private bool reloadSceneOnDeath = true;
    [SerializeField] private float reloadDelay = 1.5f;
    [SerializeField] private MonoBehaviour[] disableOnDeath;    // Drag movement + weapon scripts here

    private bool dead = false;
    private float flashTimer = 0f;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        RefreshUI();

        if (damageFlashCanvasGroup != null)
            damageFlashCanvasGroup.alpha = 0f;
    }

    private void Update()
    {
        // Fade the damage flash
        if (damageFlashCanvasGroup != null && damageFlashCanvasGroup.alpha > 0f)
        {
            flashTimer -= Time.deltaTime;
            damageFlashCanvasGroup.alpha = Mathf.Clamp01(flashTimer / flashDuration);
        }

        // DEBUG ONLY (remove later): press k to take damage
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(25f, transform.position, Vector3.up);
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (dead) return;

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(0f, CurrentHealth);

        RefreshUI();
        TriggerDamageFlash();
        PlayClip(hurtClip);

        if (CurrentHealth <= 0)
            Die();
    }

    public void Heal(float amount)
    {
        if (dead) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RefreshUI();
    }

    private void TriggerDamageFlash()
    {
        if (damageFlashCanvasGroup == null) return;

        flashTimer = flashDuration;
        damageFlashCanvasGroup.alpha = 1f;
    }

    private void RefreshUI()
    {
        if (healthText == null) return;
        healthText.text = $"{Mathf.CeilToInt(CurrentHealth)} / {Mathf.CeilToInt(maxHealth)}";
    }

    private void Die()
    {
        dead = true;

        CenterBanner.Message("OFFICER DOWN");
        PlayClip(deathClip);

        // Disable movement/shooting/etc.
        if (disableOnDeath != null)
        {
            foreach (var b in disableOnDeath)
                if (b != null) b.enabled = false;
        }

        if (reloadSceneOnDeath)
            Invoke(nameof(ReloadScene), reloadDelay);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
