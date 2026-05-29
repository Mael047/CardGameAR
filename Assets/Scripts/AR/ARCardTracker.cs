using UnityEngine;
using Vuforia;

public class ARCardTracker : MonoBehaviour
{
    [Header("Identidad")]
    public string qrID;

    private bool hasBeenPlayed = false;

    [Header("Detección de Floop por orientación")]
    [Range(30f, 80f)]
    public float floopAngleThreshold = 60f;

    private ObserverBehaviour observer;

    private CardInstance trackedCardInstance;

    private bool floopTriggeredThisDetection = false;

    public bool IsTracked { get; private set; } = false;

    private void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();

        if (observer == null)
            Debug.LogError($"ARCardTracker [{name}]: no tiene ObserverBehaviour. " +
                           "Asegúrate de que este script está en un ImageTarget de Vuforia.");
    }

    private void OnEnable()
    {
        if (observer != null)
            observer.OnTargetStatusChanged += OnStatusChanged;
        GameEvents.OnTurnChanged += OnTurnChanged;
    }

    private void OnDisable()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
        GameEvents.OnTurnChanged -= OnTurnChanged;
    }

    private void Update()
    {
        if (!IsTracked) return;
        CheckFloopOrientation();
    }

    private void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool isTracking = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
        IsTracked = isTracking;

        if (isTracking)
        {
            trackedCardInstance = ARManager.Instance.FindCardInstance(qrID);
            ARManager.Instance.RegisterTracker(qrID, this);

            if (!hasBeenPlayed)
            {
                Vector3 worldPos = transform.position;
                ARPlacementManager.Instance.TryPlaceCard(qrID, worldPos);
                hasBeenPlayed = true;
            }
        }
        else
        {
            trackedCardInstance = null;
            floopTriggeredThisDetection = false;
            ARManager.Instance.UnregisterTracker(qrID);
        }
    }

    private void OnTurnChanged(int activePlayerIndex)
    {
        hasBeenPlayed = false;
        Debug.Log($"Tracker [{qrID}] reseteado para el nuevo turno.");
    }

    private void CheckFloopOrientation()
    {
        if (trackedCardInstance == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Actions) return;

        float angle = Vector3.Angle(transform.up, Vector3.up);

        bool isRotatedForFloop = angle > floopAngleThreshold;

        if (isRotatedForFloop && !floopTriggeredThisDetection)
        {
            if (trackedCardInstance.CanFloop)
            {
                int laneIndex = trackedCardInstance.LaneIndex;
                if (laneIndex >= 0)
                {
                    bool success = GameManager.Instance.TryFloop(laneIndex);
                    if (success)
                    {
                        floopTriggeredThisDetection = true;
                        Debug.Log($"Floop físico detectado: {qrID} girado {angle:F1}°");
                    }
                }
            }
        }

        if (!isRotatedForFloop && floopTriggeredThisDetection)
            floopTriggeredThisDetection = false;
    }
}