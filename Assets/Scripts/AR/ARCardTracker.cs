using UnityEngine;
using Vuforia;

public class ARCardTracker : MonoBehaviour
{
    public string qrID;

    private GameObject spawnedObject;
    private ObserverBehaviour observer;

    private bool hasSpawned = false;

    private void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
    }

    private void OnEnable()
    {
        observer.OnTargetStatusChanged += OnStatusChanged;
    }

    private void OnDisable()
    {
        observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        if (status.Status == Status.TRACKED ||
            status.Status == Status.EXTENDED_TRACKED)
        {
            Debug.Log("Imagen detectada ✅" + qrID);
            OnCardDetected();
        }
        else
        {
            Debug.Log("Imagen perdida ❌");
            OnCardLost();
        }
    }

    private void OnCardDetected()
    {
        if (!hasSpawned)
        {
            ARManager.Instance.SpawnCard(qrID, transform);
            hasSpawned = true;
        }
    }

    private void OnCardLost()
    {
        hasSpawned = false;

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}