using UnityEngine;

public class ButtonPurposeLogger : MonoBehaviour
{
    public string purposeMessage = "Button clicked.";

    public void LogPurpose()
    {
        Debug.Log(purposeMessage);
    }
}
