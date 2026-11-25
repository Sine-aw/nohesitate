using UnityEngine;

[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    public float baseIntensity = 2f;
    public float flickerAmount = 0.6f;
    public float speed = 8f;
    [Tooltip("0이면 부드럽게, 4~6이면 단계별로 끊겨서 픽셀 느낌")]
    public int stepCount = 5;
    public Color colorA = new Color(1f, 0.5f, 0.12f);
    public Color colorB = new Color(1f, 0.8f, 0.3f);

    Light l;
    float seed;

    void Awake()
    {
        l = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        if (stepCount > 1) n = Mathf.Round(n * stepCount) / stepCount;
        l.intensity = baseIntensity + (n - 0.5f) * 2f * flickerAmount;
        l.color = Color.Lerp(colorA, colorB, n);
    }
}