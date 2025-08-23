using UnityEngine;

using UnityEngine.Events;

namespace QuestVisionStream
{
    public class QuestVisionStreamEvents : MonoBehaviour
    {
        public UnityEvent onConnectionStarted;
        public UnityEvent onConnectionClosed;
        public UnityEvent onVideoStreamStarted;
        public UnityEvent<string> onDetectionsJson;

        public void OnPeerConnectionStarted(string _)
        {
            onConnectionStarted?.Invoke();
        }

        public void OnPeerConnectionClosed(string _)
        {
            onConnectionClosed?.Invoke();
        }

        public void OnVideoStarted(string _)
        {
            onVideoStreamStarted?.Invoke();
        }

        public void OnDetections(string json)
        {
            onDetectionsJson?.Invoke(json);
        }
    } 
}



