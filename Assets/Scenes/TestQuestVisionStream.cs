using UnityEngine;

public class TestQuestVisionStream : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("Calling QuestVisionStreamBridge...");
        QuestVisionStreamBridge.ShowToast("Hello from Unity + QuestVisionStreamPlugin!");
    }
}