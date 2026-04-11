using UnityEngine;
using Vuforia;

public class ARBoardManager : MonoBehaviour
{
    public static ARBoardManager Instance { get; private set; }

    [Header("Anchors de carriles — asignar en Inspector")]
    public Transform[] player1Lanes = new Transform[3];
    public Transform[] player2Lanes = new Transform[3];

    private GameObject[] p1SpawnedCreatures = new GameObject[3];
    private GameObject[] p1SpawnedBuildings = new GameObject[3];
    private GameObject[] p2SpawnedCreatures = new GameObject[3];
    private GameObject[] p2SpawnedBuildings = new GameObject[3];

    private ObserverBehaviour observer;
    private bool isTracked = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        observer = GetComponent<ObserverBehaviour>();
    }

    private void OnEnable()
    {
        if (observer != null) observer.OnTargetStatusChanged += OnStatusChanged;

        // IMPORTANTE: Asegúrate de que estos eventos coincidan con los de tu GameManager
        GameEvents.OnCardPlayed += OnCardChanged;
        GameEvents.OnCardDestroyed += OnCardRemoved;
        GameEvents.OnTurnChanged += OnTurnChanged;

        // Sugerencia: Escuchar también cuando se activa un Floop para actualizar visuales
        GameEvents.OnFloopActivated += (p, l) => RefreshBoard();
    }

    private void OnDisable()
    {
        if (observer != null) observer.OnTargetStatusChanged -= OnStatusChanged;
        GameEvents.OnCardPlayed -= OnCardChanged;
        GameEvents.OnCardDestroyed -= OnCardRemoved;
        GameEvents.OnTurnChanged -= OnTurnChanged;
    }

    private void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        // Consideramos rastreado si está TRACKED o EXTENDED_TRACKED (fundamental para persistencia)
        bool tracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (tracked && !isTracked)
        {
            isTracked = true;
            RefreshBoard(); // Refresca al recuperar visión
        }
        else if (!tracked && isTracked)
        {
            isTracked = false;
        }
    }

    public void RefreshBoard()
    {
        if (GameManager.Instance == null) return;

        var players = GameManager.Instance.Players;
        // Ahora pasamos ambos arrays de objetos spawneados
        UpdatePlayerLanes(players[0], player1Lanes, p1SpawnedCreatures, p1SpawnedBuildings);
        UpdatePlayerLanes(players[1], player2Lanes, p2SpawnedCreatures, p2SpawnedBuildings);
    }

    private void UpdatePlayerLanes(PlayerState player, Transform[] anchors, GameObject[] spawnedCreatures, GameObject[] spawnedBuildings)
    {
        for (int i = 0; i < 3; i++)
        {
            // Procesar Criatura
            HandleCardVisual(player.CreatureLanes[i], anchors[i], ref spawnedCreatures[i], new Vector3(0, 0.05f, 0));

            // Procesar Edificio (con un pequeño offset lateral o de altura para que no se solapen)
            HandleCardVisual(player.BuildingLanes[i], anchors[i], ref spawnedBuildings[i], new Vector3(0.1f, 0.02f, 0));
        }
    }

    private void HandleCardVisual(CardInstance card, Transform anchor, ref GameObject spawnedObj, Vector3 offset)
    {
        if (card == null)
        {
            if (spawnedObj != null) { Destroy(spawnedObj); spawnedObj = null; }
            return;
        }

        if (spawnedObj == null)
        {
            GameObject prefab = card.Data.creaturePrefab;
            if (prefab != null)
            {
                spawnedObj = Instantiate(prefab, anchor);
                spawnedObj.transform.localPosition = offset;
                spawnedObj.transform.localRotation = Quaternion.identity;
            }
        }

        spawnedObj?.GetComponent<ARCardVisual>()?.UpdateVisual(card);
    }
    private void OnCardChanged(int playerIndex, int laneIndex, CardInstance card) => RefreshBoard();
    private void OnCardRemoved(int playerIndex, int laneIndex) => RefreshBoard();
    private void OnTurnChanged(int activePlayerIndex) => RefreshBoard();
}