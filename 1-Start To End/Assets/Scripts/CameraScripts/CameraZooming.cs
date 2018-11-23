using UnityEngine;

public class CameraZooming : MonoBehaviour {
    public float minFov = 4f;
    public float maxFov = 40f;
    public float sensitivity = 1f;

    private void Update() {
        float fov = Camera.main.orthographicSize;
        fov -= Input.GetAxis("Mouse ScrollWheel") * sensitivity;
        fov = Mathf.Clamp(fov, minFov, maxFov);
        Camera.main.orthographicSize = fov;
        RoundToNearestPixel(16, Camera.main);
    }

    public static float RoundToNearestPixel(float unityUnits, Camera viewingCamera) {
        float valueInPixels = (Screen.height / (viewingCamera.orthographicSize * 2)) * unityUnits;
        valueInPixels = Mathf.Round(valueInPixels);
        float adjustedUnityUnits = valueInPixels / (Screen.height / (viewingCamera.orthographicSize * 2));
        return adjustedUnityUnits;
    }
}