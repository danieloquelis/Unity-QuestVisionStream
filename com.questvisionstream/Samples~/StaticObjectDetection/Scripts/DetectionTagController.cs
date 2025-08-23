using UnityEngine;

public class DetectionTagController : MonoBehaviour
{
    [SerializeField] private Vector3 anglesSpeed = new(20.0f, 40.0f, 60.0f);
    
    [Header("Tag text")]
    [SerializeField] private Transform tagLabel;
    [SerializeField] private TextMesh textMesh;
    
    private Vector3 _mAngles;
    private OVRCameraRig _mCamera;

    private void Update()
    {
        _mAngles.x = AddAngle(_mAngles.x, anglesSpeed.x * Time.deltaTime);
        _mAngles.y = AddAngle(_mAngles.y, anglesSpeed.y * Time.deltaTime);
        _mAngles.z = AddAngle(_mAngles.z, anglesSpeed.z * Time.deltaTime);
        
        if (!_mCamera)
        {
            _mCamera = FindFirstObjectByType<OVRCameraRig>();
        }
        else
        {
            tagLabel.gameObject.transform.LookAt(_mCamera.centerEyeAnchor);
        }
    }

    private static float AddAngle(float value, float toAdd)
    {
        value += toAdd;
        if (value > 360.0f)
        {
            value -= 360.0f;
        }

        if (value < 0.0f)
        {
            value = 360.0f - value;
        }

        return value;
    }

    public void SetYoloClassName(string className)
    {
        textMesh.text = className;
    }
}
