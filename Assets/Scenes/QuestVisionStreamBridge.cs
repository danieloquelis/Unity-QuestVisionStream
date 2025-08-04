using System;
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
            Debug.Log("QuestVisionStreamBridge initialized");
        }
        catch (Exception e)
        {
            Debug.LogError("QuestVisionStreamBridge init failed: " + e);
        }
    }

    public static void ConnectToSignalingServer(string url)
    {
        plugin?.Call("connectToSignalingServer", url);
    }

    public static void SetExternalTexture(IntPtr texPtr, int width, int height)
    {
        plugin?.Call("setExternalTexture", (long)texPtr, width, height);
    }

    public static void CreateOffer()
    {
        plugin?.Call("createOffer");
    }

    public static void SetIceServers(string[] iceServers)
    {
        var javaList = new AndroidJavaObject("java.util.ArrayList");
        foreach (var s in iceServers)
        {
            javaList.Call<bool>("add", s);
        }
        plugin?.Call("setIceServers", javaList);
    }

    // New method to send pixel data directly
    public static void UpdateFrameData(byte[] pixelData, int width, int height)
    {
        plugin?.Call("updateFrameData", pixelData, width, height);
    }
}
