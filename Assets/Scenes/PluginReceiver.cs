using UnityEngine;

public class PluginReceiver : MonoBehaviour
{
    // Called from Kotlin
    public void OnPluginMessage(string message)
    {
        Debug.Log("PluginReceiver got message: " + message);

        // For testing: show on screen in VR
        GameObject textObj = new GameObject("PluginMessage");
        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = message;
        textMesh.characterSize = 0.1f;
        textObj.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2;
    }
}