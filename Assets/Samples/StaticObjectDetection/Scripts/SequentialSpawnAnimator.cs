using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequentialSpawnAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private List<GameObject> objectsToAnimate = new List<GameObject>();
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float delayBetweenObjects = 0.2f;
    [SerializeField] private bool startAnimationOnStart = true;
    
    [Header("Animation Type")]
    [SerializeField] private AnimationType animationType = AnimationType.FadeIn;
    
    public enum AnimationType
    {
        FadeIn,
        ScaleUp,
        SlideUp,
        SlideDown,
        SlideLeft,
        SlideRight
    }
    
    private List<Renderer> _renderers = new List<Renderer>();
    private List<CanvasGroup> _canvasGroups = new List<CanvasGroup>();
    private List<Vector3> _originalScales = new List<Vector3>();
    private List<Vector3> _originalPositions = new List<Vector3>();
    
    private void Start()
    {
        if (startAnimationOnStart)
        {
            StartAnimation();
        }
    }
    
    public void StartAnimation()
    {
        InitializeObjects();
        StartCoroutine(AnimateSequence());
    }
    
    private void InitializeObjects()
    {
        _renderers.Clear();
        _canvasGroups.Clear();
        _originalScales.Clear();
        _originalPositions.Clear();
        
        foreach (GameObject obj in objectsToAnimate)
        {
            if (obj == null) continue;
            
            // Get renderer
            Renderer renderer = obj.GetComponent<Renderer>();
            _renderers.Add(renderer);
            
            // Get canvas group (for UI elements)
            CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = obj.AddComponent<CanvasGroup>();
            }
            _canvasGroups.Add(canvasGroup);
            
            // Store original scale and position
            _originalScales.Add(obj.transform.localScale);
            _originalPositions.Add(obj.transform.localPosition);
            
            // Set initial state based on animation type
            SetInitialState(obj, renderer, canvasGroup);
        }
    }
    
    private void SetInitialState(GameObject obj, Renderer renderer, CanvasGroup canvasGroup)
    {
        switch (animationType)
        {
            case AnimationType.FadeIn:
                if (renderer != null)
                {
                    Color color = renderer.material.color;
                    color.a = 0f;
                    renderer.material.color = color;
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                }
                break;
                
            case AnimationType.ScaleUp:
                obj.transform.localScale = Vector3.zero;
                break;
                
            case AnimationType.SlideUp:
                obj.transform.localPosition += Vector3.down * 2f;
                break;
                
            case AnimationType.SlideDown:
                obj.transform.localPosition += Vector3.up * 2f;
                break;
                
            case AnimationType.SlideLeft:
                obj.transform.localPosition += Vector3.right * 2f;
                break;
                
            case AnimationType.SlideRight:
                obj.transform.localPosition += Vector3.left * 2f;
                break;
        }
    }
    
    private IEnumerator AnimateSequence()
    {
        for (int i = 0; i < objectsToAnimate.Count; i++)
        {
            if (objectsToAnimate[i] == null) continue;
            
            // Start animation for current object
            StartCoroutine(AnimateObject(i));
            
            // Wait before starting next object
            yield return new WaitForSeconds(delayBetweenObjects);
        }
    }
    
    private IEnumerator AnimateObject(int index)
    {
        GameObject obj = objectsToAnimate[index];
        Renderer renderer = _renderers[index];
        CanvasGroup canvasGroup = _canvasGroups[index];
        Vector3 originalScale = _originalScales[index];
        Vector3 originalPosition = _originalPositions[index];
        
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeInDuration;
            float easeProgress = EaseOutCubic(progress);
            
            switch (animationType)
            {
                case AnimationType.FadeIn:
                    if (renderer != null)
                    {
                        Color color = renderer.material.color;
                        color.a = easeProgress;
                        renderer.material.color = color;
                    }
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = easeProgress;
                    }
                    break;
                    
                case AnimationType.ScaleUp:
                    obj.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, easeProgress);
                    break;
                    
                case AnimationType.SlideUp:
                    Vector3 slideUpStart = originalPosition + Vector3.down * 2f;
                    obj.transform.localPosition = Vector3.Lerp(slideUpStart, originalPosition, easeProgress);
                    break;
                    
                case AnimationType.SlideDown:
                    Vector3 slideDownStart = originalPosition + Vector3.up * 2f;
                    obj.transform.localPosition = Vector3.Lerp(slideDownStart, originalPosition, easeProgress);
                    break;
                    
                case AnimationType.SlideLeft:
                    Vector3 slideLeftStart = originalPosition + Vector3.right * 2f;
                    obj.transform.localPosition = Vector3.Lerp(slideLeftStart, originalPosition, easeProgress);
                    break;
                    
                case AnimationType.SlideRight:
                    Vector3 slideRightStart = originalPosition + Vector3.left * 2f;
                    obj.transform.localPosition = Vector3.Lerp(slideRightStart, originalPosition, easeProgress);
                    break;
            }
            
            yield return null;
        }
        
        // Ensure final state is correct
        SetFinalState(obj, renderer, canvasGroup, originalScale, originalPosition);
    }
    
    private void SetFinalState(GameObject obj, Renderer renderer, CanvasGroup canvasGroup, Vector3 originalScale, Vector3 originalPosition)
    {
        switch (animationType)
        {
            case AnimationType.FadeIn:
                if (renderer != null)
                {
                    Color color = renderer.material.color;
                    color.a = 1f;
                    renderer.material.color = color;
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
                break;
                
            case AnimationType.ScaleUp:
                obj.transform.localScale = originalScale;
                break;
                
            case AnimationType.SlideUp:
            case AnimationType.SlideDown:
            case AnimationType.SlideLeft:
            case AnimationType.SlideRight:
                obj.transform.localPosition = originalPosition;
                break;
        }
    }
    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
    
    // Public methods for external control
    public void SetObjectsToAnimate(List<GameObject> objects)
    {
        objectsToAnimate = objects;
    }
    
    public void AddObjectToAnimate(GameObject obj)
    {
        if (!objectsToAnimate.Contains(obj))
        {
            objectsToAnimate.Add(obj);
        }
    }
    
    public void RemoveObjectFromAnimate(GameObject obj)
    {
        objectsToAnimate.Remove(obj);
    }
    
    public void SetFadeInDuration(float duration)
    {
        fadeInDuration = duration;
    }
    
    public void SetDelayBetweenObjects(float delay)
    {
        delayBetweenObjects = delay;
    }
    
    public void SetAnimationType(AnimationType type)
    {
        animationType = type;
    }
}
