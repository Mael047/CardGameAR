using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;

    private void Start()
    {
        cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        Vector3 direction = transform.position - cam.position;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}