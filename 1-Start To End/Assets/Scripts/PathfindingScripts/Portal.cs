using System.Collections;
using UnityEngine;

public class Portal : MonoBehaviour {
    public GameObject connectedTo;
    public float rotationSpeed = 10f;
    private Vector3 test = new Vector3(0, 0, 10);
    /// Use this for initialization

    private void Update() {
        transform.Rotate(test * (rotationSpeed * Time.deltaTime));
    }
}