using UnityEngine;

public class BobbingBallSpawner : MonoBehaviour
{
    [Header("Ball")]
    public Vector3 spawnPosition = new Vector3(0f, 3f, 0f);
    public float radius = 1f;
    public Color color = Color.yellow;

    [Header("Bob Motion")]
    public float bobHeight = 0.75f;
    public float bobSpeed = 2f;

    void Start()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Bobbing Ball";
        ball.transform.position = spawnPosition;
        ball.transform.localScale = Vector3.one * radius * 2f;

        Collider collider = ball.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = ball.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateBallMaterial();
        }

        BobbingBallMotion motion = ball.AddComponent<BobbingBallMotion>();
        motion.height = bobHeight;
        motion.speed = bobSpeed;
    }

    Material CreateBallMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}

public class BobbingBallMotion : MonoBehaviour
{
    public float height = 0.75f;
    public float speed = 2f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * speed) * height;
        transform.position = startPosition + Vector3.up * yOffset;
    }
}
