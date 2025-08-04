using UnityEngine;

public static class QuestVisionStreamBridge
{
    private static AndroidJavaObject plugin;

    static QuestVisionStreamBridge()
    {
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            plugin = new AndroidJavaObject(
                "com.xreducation.questvisionstreamplugin.QuestVisionStreamManager", 
                activity
            );
        }
        catch (System.Exception e)
        {
            Debug.LogError("QuestVisionStreamBridge init failed: " + e);
        }
    }

    public static void ShowToast(string message)
    {
        if (plugin == null)
        {
            Debug.LogError("Plugin not initialized!");
            return;
        }
        
        plugin.Call("showToast", message);
    }
}