using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // can start from main scene, button to toggle 
    public void LoadTrainingScene()
    {
        SceneManager.LoadScene("TrainingScene");
    }
    public void LoadTestingScene()
    {
        SceneManager.LoadScene("TestingScene");
    }
}
