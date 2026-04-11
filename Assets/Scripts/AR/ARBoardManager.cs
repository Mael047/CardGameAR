using UnityEngine;
using Vuforia;

public class ARBoardManager : MonoBehaviour
{
    public static ARBoardManager Instance { get; private set; }

    [Header("Anchors de carriles — asignar en Inspector")]
    public Transform[] player1Lanes = new Transform[3];
    public Transform[] player2Lanes = new Transform[3];

    private GameObject[] p1Spawned = new GameObject[3];
    private GameObject[] p2Spawned = new GameObject[3];

    private ObserverBehaviour observer;
    private bool isTracked = false;
    private bool boardLocked = false;

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

        // ¿Estás seguro de que esta lista tiene los 2 jugadores?
        var players = GameManager.Instance.Players;

        // Si players[1] es el Jugador 2, ¿se está enviando player2Lanes?
        UpdatePlayerLanes(players[0], player1Lanes, p1Spawned);
        UpdatePlayerLanes(players[1], player2Lanes, p2Spawned);
    }

    private void UpdatePlayerLanes(PlayerState player, Transform[] anchors, GameObject[] spawned)
    {
        for (int i = 0; i < 3; i++)
        {
            CardInstance card = player.CreatureLanes[i];

            if (card == null)
            {
                if (spawned[i] != null) { Destroy(spawned[i]); spawned[i] = null; }
                continue;
            }

            if (spawned[i] != null)
            {
                // Solo actualizamos el visual (HP, estado Ready/Floop)
                spawned[i].GetComponent<ARCardVisual>()?.UpdateVisual(card);
                continue;
            }

            // CREACIÓN PERSISTENTE:
            GameObject prefab = card.Data.creaturePrefab;
            if (prefab != null)
            {
                // Se instancia como hijo del carril. 
                // Como el carril es hijo del Board (Vuforia), se moverá con el tablero.
                spawned[i] = Instantiate(prefab, anchors[i]);
                spawned[i].transform.localPosition = new Vector3(0f, 0.02f, 0f); // Un poco elevado
                spawned[i].transform.localRotation = Quaternion.identity;

                spawned[i].GetComponent<ARCardVisual>()?.UpdateVisual(card);
            }
        }
    }

    private void OnCardChanged(int playerIndex, int laneIndex, CardInstance card) => RefreshBoard();
    private void OnCardRemoved(int playerIndex, int laneIndex) => RefreshBoard();
    private void OnTurnChanged(int activePlayerIndex) => RefreshBoard();
}