using System;
using UnityEngine;

namespace QuestVisionStream
{
    public static class QuestVisionStreamBridge
    {
        private static readonly AndroidJavaObject Plugin;

        static QuestVisionStreamBridge()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                Plugin = new AndroidJavaObject(
                    "com.questvisionstream.QuestVisionStreamManager",
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
            Plugin?.Call("connectToSignalingServer", url);
        }

        public static void SetExternalTexture(IntPtr texPtr, int width, int height)
        {
            Plugin?.Call("setExternalTexture", (long)texPtr, width, height);
        }

        public static void SetTargetFps(int fps)
        {
            Plugin?.Call("setTargetFps", fps);
        }

        public static void SetDesiredResolution(int width, int height)
        {
            Plugin?.Call("setDesiredResolution", width, height);
        }

        public static void CreateOffer()
        {
            Plugin?.Call("createOffer");
        }

        public static void SetIceServers(string[] iceServers)
        {
            var javaList = new AndroidJavaObject("java.util.ArrayList");
            foreach (var s in iceServers)
            {
                javaList.Call<bool>("add", s);
            }
            Plugin?.Call("setIceServers", javaList);
        }

        public static void UpdateFrameData(byte[] pixelData, int width, int height)
        {
            Plugin?.Call("updateFrameData", pixelData, width, height);
        }
        
        public static void UpdateFrameDataYuv(byte[] yData, byte[] uData, byte[] vData, int width, int height)
        {
            Plugin?.Call("updateFrameDataYUV", yData, uData, vData, width, height);
        }

        public static void SetUnityMessageTarget(string gameObjectName, string methodName)
        {
            Plugin?.Call("setUnityMessageTarget", gameObjectName, methodName);
        }

        public static void SendDataChannelMessage(string message)
        {
            Plugin?.Call("sendDataChannelMessage", message);
        }

        public static void CreateCustomDataChannel(string name)
        {
            Plugin?.Call("createCustomDataChannel", name);
        }

        public static void SendDataChannelMessageOn(string channel, string message)
        {
            Plugin?.Call("sendDataChannelMessageOn", channel, message);
        }

        public static void AddTurnServer(string turnServerUrl, string turnUsername, string turnCredential)
        {
            Plugin?.Call("addTurnServer", turnServerUrl, turnUsername, turnCredential);
        }
    }   
}
