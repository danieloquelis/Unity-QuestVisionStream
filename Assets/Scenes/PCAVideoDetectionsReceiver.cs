using System;
using System.Collections.Generic;
using UnityEngine;
using PassthroughCameraSamples;
using PassthroughCameraSamples.MultiObjectDetection;

public class PCAVideoDetectionsReceiver : MonoBehaviour
{
    [Header("Anchoring")]
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private EnvironmentRayCastSampleManager environmentRaycast;
    [SerializeField] private GameObject bboxPrefab;
    [SerializeField] private bool invertY = true;
    [SerializeField] private float minDistanceMeters = 0.3f;
    [SerializeField] private bool allowMultiplePerClass = false;
    private int streamWidth = 640;
    private int streamHeight = 480;

    private readonly Dictionary<string, GameObject> spawnedObjects = new();
    private readonly HashSet<string> spawnedClasses = new();

    private void Awake()
    {
        if (environmentRaycast == null) environmentRaycast = FindObjectOfType<EnvironmentRayCastSampleManager>();
        if (webCamTextureManager == null) webCamTextureManager = FindObjectOfType<WebCamTextureManager>();
    }

    // Called from Android via UnitySendMessage
    public void OnDetections(string json)
    {
        try
        {
            var payload = JsonUtility.FromJson<DetectionsPayload>(json);
            if (payload == null || payload.detections == null) return;
            ProcessDetections(payload.detections);
        }
        catch (Exception e)
        {
            // ignore parsing errors
        }
    }

    private void ProcessDetections(Detection[] detections)
    {
        if (webCamTextureManager == null || environmentRaycast == null) return;

        foreach (var d in detections)
        {
            // Check if this class+position combination already exists
            if (ShouldSkipDetection(d)) continue;

            Vector3 hitPos = GetWorldPosition(d);
            if (hitPos == Vector3.zero) continue;

            // Spawn object
            SpawnObject(d.label, hitPos);
        }
    }

    private bool ShouldSkipDetection(Detection detection)
    {
        Vector3 worldPos = GetWorldPosition(detection);
        if (worldPos == Vector3.zero) return true;

        // Strict mode: only one object per class allowed
        if (!allowMultiplePerClass)
        {
            if (spawnedClasses.Contains(detection.label))
            {
                return true; // Skip - class already spawned
            }
        }
        else
        {
            // Distance-based checking for multiple instances
            foreach (var kvp in spawnedObjects)
            {
                string existingKey = kvp.Key;
                GameObject existingObject = kvp.Value;
                
                if (existingObject != null && existingKey.StartsWith(detection.label + "_"))
                {
                    float distance = Vector3.Distance(worldPos, existingObject.transform.position);
                    if (distance < minDistanceMeters)
                    {
                        return true; // Skip - too close to existing
                    }
                }
            }
        }
        
        return false;
    }

    private Vector3 GetWorldPosition(Detection detection)
    {
        float cx = (detection.bbox[0] + detection.bbox[2]) * 0.5f;
        float cy = (detection.bbox[1] + detection.bbox[3]) * 0.5f;

        var eye = webCamTextureManager.Eye;
        var camRes = PassthroughCameraUtils.GetCameraIntrinsics(eye).Resolution;
        float nx = cx / Mathf.Max(1, streamWidth);
        float ny = cy / Mathf.Max(1, streamHeight);
        if (invertY) ny = 1f - ny;
        int px = Mathf.RoundToInt(nx * camRes.x);
        int py = Mathf.RoundToInt(ny * camRes.y);
        var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(eye, new Vector2Int(px, py));
        var posOpt = environmentRaycast.PlaceGameObjectByScreenPos(ray);
        
        return posOpt.HasValue ? posOpt.Value : Vector3.zero;
    }

    private void SpawnObject(string className, Vector3 position)
    {
        var uniqueKey = $"{className}_{spawnedObjects.Count}";

        var go = Instantiate(bboxPrefab, position, Quaternion.identity);
        var tagController = go.GetComponent<DetectionTagController>();
        tagController.SetYoloClassName(className);
        
        go.name = $"Detection_{className}_{spawnedObjects.Count}";
        spawnedObjects[uniqueKey] = go;
        spawnedClasses.Add(className);
        
        Debug.Log($"Spawned {className} at {position} (Total classes: {spawnedClasses.Count})");
    }

    // Helper to parse arrays with JsonUtility
    [Serializable]
    private class DetectionsPayload
    {
        public string type;
        public int frame;
        public Detection[] detections;
    }

    [Serializable]
    private class Detection
    {
        public string label;
        public float conf;
        public float[] bbox;
    }

    public void SetStreamDimensions(int width, int height)
    {
        streamWidth = Mathf.Max(1, width);
        streamHeight = Mathf.Max(1, height);
    }
}


