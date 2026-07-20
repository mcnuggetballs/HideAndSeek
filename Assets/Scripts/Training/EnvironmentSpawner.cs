using UnityEngine;

// to be used by scenariogrid ONLY! ScenarioGrid will be the only place that received DATA
// create game servers, "server 1 server 2 server 3"

public class EnvironmentSpawner : MonoBehaviour
{
    public GameObject environmentPrefab;

    [SerializeField] public int environmentCount = 16;
    [SerializeField] public float environmentSize = 30f;
    public int maxCount = 50;

    private float spacing = 80f;

    private void Start()
    {

        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(environmentCount));
        float totalWorldSize = gridSize * environmentSize;

        if (environmentCount > maxCount)
        {
            Debug.LogWarning("Maximum environments that can be spawned is up to 50");
            return;
        }

        for (int i = 0; i < environmentCount; i++)
        {
            int x = i % gridSize;
            int z = i / gridSize;

            Vector3 position = new Vector3(
                x * spacing,
                0,
                z * spacing
                );

            Instantiate(environmentPrefab, position, Quaternion.identity);
        }

    }

}
