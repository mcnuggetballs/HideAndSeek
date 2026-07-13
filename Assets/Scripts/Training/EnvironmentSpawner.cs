using UnityEngine;

public class EnvironmentSpawner : MonoBehaviour
{
    public GameObject environmentPrefab;
    public int environmentCount = 4;
    public int maxCount = 50;
    public float spacing = 20f;

    private void Start()
    {
        if (environmentCount > maxCount)
        {
            Debug.LogWarning("Maximum environments that can be spawned is up to 50");
            return;
        }
        for (int i = 0; i < environmentCount; i++)
        {
            Vector3 pos = new Vector3(i * spacing, 0, 0);
            Instantiate(environmentPrefab, pos, Quaternion.identity);
        }
    }

}
