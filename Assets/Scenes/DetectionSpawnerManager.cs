using System;
using System.Collections.Generic;
using UnityEngine;
using PassthroughCameraSamples;
using PassthroughCameraSamples.MultiObjectDetection;

public class DetectionSpawnerManager : MonoBehaviour
{
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

	public void OnDetections(string json)
	{
		var payload = TryParse(json);
		if (payload?.detections == null) return;
		
		// Adapt to server-provided frame size if available
		if (payload.width > 0 && payload.height > 0)
		{
			streamWidth = payload.width;
			streamHeight = payload.height;
		}
		ProcessDetections(payload.detections);
	}

	private DetectionsPayload TryParse(string json)
	{
		try { return JsonUtility.FromJson<DetectionsPayload>(json); }
		catch { return null; }
	}

	private void ProcessDetections(Detection[] detections)
	{
		if (webCamTextureManager == null || environmentRaycast == null) return;
		foreach (var d in detections)
		{
			var worldPos = ComputeWorldPosition(d);
			if (worldPos == Vector3.zero) continue;
			if (ShouldSkipDetection(d, worldPos)) continue;
			SpawnObject(d.label, worldPos);
		}
	}

	private bool ShouldSkipDetection(Detection detection, Vector3 worldPos)
	{
		if (!allowMultiplePerClass)
		{
			if (spawnedClasses.Contains(detection.label)) return true;
		}
		else
		{
			foreach (var kvp in spawnedObjects)
			{
				var go = kvp.Value;
				if (go == null) continue;
				if (!kvp.Key.StartsWith(detection.label + "_")) continue;
				if (Vector3.Distance(worldPos, go.transform.position) < minDistanceMeters) return true;
			}
		}
		return false;
	}

	private Vector3 ComputeWorldPosition(Detection detection)
	{
		var cx = (detection.bbox[0] + detection.bbox[2]) * 0.5f;
		var cy = (detection.bbox[1] + detection.bbox[3]) * 0.5f;
		
		var eye = webCamTextureManager.Eye;
		var camRes = PassthroughCameraUtils.GetCameraIntrinsics(eye).Resolution;
		
		var nx = cx / Mathf.Max(1, streamWidth);
		var ny = cy / Mathf.Max(1, streamHeight);
		
		if (invertY) ny = 1f - ny;
		var px = Mathf.RoundToInt(nx * camRes.x);
		var py = Mathf.RoundToInt(ny * camRes.y);
		
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
	}

	[Serializable]
	private class DetectionsPayload
	{
		public string type;
		public int frame;
		public int width;
		public int height;
		public Detection[] detections;
	}

	[Serializable]
	private class Detection
	{
		public string label;
		public float conf;
		public float[] bbox;
	}
}


