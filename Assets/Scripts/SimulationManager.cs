using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public GameObject simulationPrefab;

    //public string simulationMode;
    public int NoOfSimulations = 0; 
    public float spaceBetween = 0;
    public int rowCount = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //SpawnTraining(simulationPrefab);
        SpawnTesting(simulationPrefab);
    }
    /*
        
     
     */
    void SpawnTraining(GameObject simulationPrefab)
    {
        //GameObject spawnedObject = Object.Instantiate(simulationPrefab);
        for (int  i = 0; i < rowCount; i++) //Row
        {

            for (int j = 0; j < rowCount; j++) //Column
            {
                GameObject go = Instantiate(simulationPrefab);
                go.transform.position = new Vector3(
                    i  * spaceBetween, 
                    0, 
                    j  * spaceBetween);
            }
        }
    }

    void SpawnTesting(GameObject simulationPrefab)
    {
        GameObject go = Instantiate(simulationPrefab);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
