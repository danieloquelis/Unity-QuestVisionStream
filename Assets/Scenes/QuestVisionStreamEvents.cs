using System;
using UnityEngine;

using UnityEngine.Events;

public class QuestVisionStreamEvents : MonoBehaviour
{
    [Serializable] public class StringEvent : UnityEvent<string> {}
    public UnityEvent OnConnectionStarted;
    public UnityEvent OnConnectionClosed;
    public UnityEvent OnVideoStreamStarted;
    public StringEvent OnDetectionsJson;

    public void OnPeerConnectionStarted(string _)
    {
        OnConnectionStarted?.Invoke();
    }

    public void OnPeerConnectionClosed(string _)
    {
        OnConnectionClosed?.Invoke();
    }

    public void OnVideoStarted(string _)
    {
        OnVideoStreamStarted?.Invoke();
    }

    public void OnDetections(string json)
    {
        OnDetectionsJson?.Invoke(json);
    }
}


