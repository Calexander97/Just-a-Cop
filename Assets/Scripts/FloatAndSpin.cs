using UnityEngine;

/// Attach to the visual of the pickup (not the root that has the trigger).
/// Classic bob + rotate, independent of Time.timeScale.
public class FloatAndSpin : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] float amplitude = 0.15f;      // how high it moves (units)
    [SerializeField] float frequency = 1.5f;       // cycles per second
    [SerializeField] float phaseOffset = 0f;       // radians, use random per prefab to desync

    [Header("Spin")]
    [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f); // Y-spin

    Vector3 _startPos;

    void Awake()
    {
        _startPos = transform.localPosition;
        if (phaseOffset == 0f) phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // Spin (world or local—local feels best for sprites)
        transform.Rotate(degreesPerSecond * Time.unscaledDeltaTime, Space.Self);

        // Bob
        float bob = Mathf.Sin((Time.unscaledTime * frequency * Mathf.PI * 2f) + phaseOffset) * amplitude;
        transform.localPosition = _startPos + new Vector3(0f, bob, 0f);
    }
}
