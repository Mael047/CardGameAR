using UnityEngine;
using Vuforia;

public class ARCardTracker : MonoBehaviour
{
    [Header("Identidad")]
    public string qrID;

    [Header("Detección de Floop por orientación")]
    [Range(30f, 80f)]
    public float floopAngleThreshold = 60f;

    // ── Referencias internas ──────────────────────────────────────────────
    private ObserverBehaviour observer;

    private GameObject spawnedVisual;

    private ARCardVisual cardVisual;

    private CardInstance trackedCardInstance;

    private bool floopTriggeredThisDetection = false;

    // ── Estado de tracking ────────────────────────────────────────────────
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
    }

    private void OnDisable()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    // ── Update: corre cada frame mientras la carta es visible ─────────────
    private void Update()
    {
        if (!IsTracked) return;

        // Detecta la orientación de la carta para Floop
        CheckFloopOrientation();

        // Actualiza el feedback visual si hay una instancia trackeada
        if (cardVisual != null && trackedCardInstance != null)
            cardVisual.UpdateVisual(trackedCardInstance);
    }

    // ── Callback de Vuforia ───────────────────────────────────────────────
    private void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool nowTracked = status.Status == Status.TRACKED ||
                          status.Status == Status.EXTENDED_TRACKED;

        if (nowTracked && !IsTracked)
        {
            IsTracked = true;
            floopTriggeredThisDetection = false;
            OnCardDetected();
        }
        else if (!nowTracked && IsTracked)
        {
            IsTracked = false;
            OnCardLost();
        }
    }

    // ── Carta detectada ───────────────────────────────────────────────────
    private void OnCardDetected()
    {
        Debug.Log($"Carta detectada: {qrID}");
        trackedCardInstance = ARManager.Instance.FindCardInstance(qrID);

        ARManager.Instance.RegisterTracker(qrID, this);

        // Spawna el visual solo si no hay uno ya (evita duplicados)
        if (spawnedVisual == null)
            SpawnVisual();
    }

    // ── Carta perdida ─────────────────────────────────────────────────────
    private void OnCardLost()
    {
        Debug.Log($"Carta perdida: {qrID}");
        if (spawnedVisual != null)
        {
            Destroy(spawnedVisual);
            spawnedVisual = null;
            cardVisual = null;
        }

        trackedCardInstance = null;
        floopTriggeredThisDetection = false;
        ARManager.Instance.UnregisterTracker(qrID);
    }

    // ── Spawna el prefab visual ───────────────────────────────────────────
    private void SpawnVisual()
    {
        CardData data = ARManager.Instance.FindCardData(qrID);

        if (data == null)
        {
            Debug.LogWarning($"ARCardTracker: no se encontró CardData para QR '{qrID}'.");
            return;
        }

        if (data.creaturePrefab == null)
        {
            Debug.LogWarning($"ARCardTracker: {data.cardName} no tiene prefab asignado.");
            return;
        }

        spawnedVisual = Instantiate(data.creaturePrefab, transform);
        spawnedVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        spawnedVisual.transform.localRotation = Quaternion.identity;

        // Busca el componente de feedback visual en el prefab
        cardVisual = spawnedVisual.GetComponent<ARCardVisual>();
        if (cardVisual == null)
            Debug.Log($"ARCardTracker: {data.cardName} no tiene ARCardVisual " +
                      "(opcional — sin feedback visual de estado).");

        Debug.Log($"Spawneado: {data.cardName}");
    }

    // ── Detección de orientación para Floop ───────────────────────────────
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
                // Encuentra en qué carril está esta carta
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
    public void RefreshVisual()
    {
        if (cardVisual == null || trackedCardInstance == null) return;
        cardVisual.UpdateVisual(trackedCardInstance);
    }
}