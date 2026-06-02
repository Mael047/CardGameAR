using UnityEngine;
using Vuforia;
using System.Collections.Generic;

public class ARBoardManager : MonoBehaviour
{
    [System.Serializable]
    public class LaneConfig
    {
        public Vector3 creatureOffset = Vector3.zero;
        public Vector3 buildingOffset = new Vector3(0.06f, 0, 0);
        public Vector3 cardRotation = new Vector3(0, 180, 0);
        public Vector3 landscapeOffset = Vector3.zero;
    }

    [System.Serializable]
    public struct LandscapeLanePrefabEntry
    {
        public LandscapeType type;
        public GameObject prefab;
    }

    public static ARBoardManager Instance { get; private set; }

    [Header("Anchors de carriles — asignar en Inspector")]
    public Transform[] player1Lanes = new Transform[3];
    public Transform[] player2Lanes = new Transform[3];

    [Header("Ajustes por carril — Player 1 (cambia mientras ejecutas)")]
    public LaneConfig[] player1LaneSettings = new LaneConfig[3];
    [Header("Ajustes por carril — Player 2 (cambia mientras ejecutas)")]
    public LaneConfig[] player2LaneSettings = new LaneConfig[3];

    [Header("Escala global")]
    [Tooltip("Multiplicador sobre la escala base del prefab")]
    public Vector3 cardScale = new Vector3(1, 1, 1);

    [Header("Prefabs 3D de paisajes para los carriles")]
    public LandscapeLanePrefabEntry[] landscapeLanePrefabs = new LandscapeLanePrefabEntry[5];

    [Header("Colores de paisajes para los planos de los carriles")]
    public Color colorNicelands = new Color(0.2f, 0.7f, 0.2f, 0.4f);
    public Color colorCornfield = new Color(0.9f, 0.8f, 0.1f, 0.4f);
    public Color colorUselessSwamp = new Color(0.3f, 0.2f, 0.1f, 0.4f);
    public Color colorSpookyCemetery = new Color(0.4f, 0.3f, 0.5f, 0.4f);
    public Color colorRainbow = new Color(0.8f, 0.6f, 1f, 0.4f);

    private GameObject[] p1SpawnedCreatures = new GameObject[3];
    private GameObject[] p1SpawnedBuildings = new GameObject[3];
    private GameObject[] p2SpawnedCreatures = new GameObject[3];
    private GameObject[] p2SpawnedBuildings = new GameObject[3];

    private GameObject[] p1LaneLandscapes = new GameObject[3];
    private GameObject[] p2LaneLandscapes = new GameObject[3];

    private Dictionary<GameObject, Vector3> basePositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Vector3> baseScales = new Dictionary<GameObject, Vector3>();

    private ObserverBehaviour observer;
    private bool isTracked = false;

    private LaneConfig[] prevP1LaneSettings;
    private LaneConfig[] prevP2LaneSettings;
    private Vector3 prevCardScale;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        observer = GetComponent<ObserverBehaviour>();

        InitLaneSettings();
    }

    private void InitLaneSettings()
    {
        player1LaneSettings = InitLaneArray(player1LaneSettings);
        player2LaneSettings = InitLaneArray(player2LaneSettings);

        prevP1LaneSettings = CopyLaneArray(player1LaneSettings);
        prevP2LaneSettings = CopyLaneArray(player2LaneSettings);
        prevCardScale = cardScale;
    }

    private LaneConfig[] InitLaneArray(LaneConfig[] arr)
    {
        if (arr == null || arr.Length != 3)
            arr = new LaneConfig[3];
        for (int i = 0; i < 3; i++)
            if (arr[i] == null) arr[i] = new LaneConfig();
        return arr;
    }

    private LaneConfig[] CopyLaneArray(LaneConfig[] src)
    {
        LaneConfig[] dst = new LaneConfig[3];
        for (int i = 0; i < 3; i++)
        {
            dst[i] = new LaneConfig();
            CopyLaneConfig(src[i], dst[i]);
        }
        return dst;
    }

    private void CopyLaneConfig(LaneConfig from, LaneConfig to)
    {
        to.creatureOffset = from.creatureOffset;
        to.buildingOffset = from.buildingOffset;
        to.cardRotation = from.cardRotation;
        to.landscapeOffset = from.landscapeOffset;
    }

    private bool LaneConfigsEqual(LaneConfig a, LaneConfig b)
    {
        return a.creatureOffset == b.creatureOffset
            && a.buildingOffset == b.buildingOffset
            && a.cardRotation == b.cardRotation
            && a.landscapeOffset == b.landscapeOffset;
    }

    private Color GetLandscapeColor(LandscapeType landscape)
    {
        return landscape switch
        {
            LandscapeType.Nicelands => colorNicelands,
            LandscapeType.Cornfield => colorCornfield,
            LandscapeType.UselessSwamp => colorUselessSwamp,
            LandscapeType.SpookyCemetery => colorSpookyCemetery,
            LandscapeType.Rainbow => colorRainbow,
            _ => Color.clear
        };
    }

    private GameObject GetLandscapePrefab(LandscapeType landscape)
    {
        if (landscapeLanePrefabs == null) return null;
        foreach (var entry in landscapeLanePrefabs)
            if (entry.type == landscape)
                return entry.prefab;
        return null;
    }

    private void OnEnable()
    {
        if (observer != null) observer.OnTargetStatusChanged += OnStatusChanged;

        GameEvents.OnCardPlayed += OnCardChanged;
        GameEvents.OnCardDestroyed += OnCardRemoved;
        GameEvents.OnTurnChanged += OnTurnChanged;
        GameEvents.OnGameStateChanged += OnGameStateChanged;
        GameEvents.OnFloopActivated += (p, l) => RefreshBoard();
    }

    private void OnDisable()
    {
        if (observer != null) observer.OnTargetStatusChanged -= OnStatusChanged;
        GameEvents.OnCardPlayed -= OnCardChanged;
        GameEvents.OnCardDestroyed -= OnCardRemoved;
        GameEvents.OnTurnChanged -= OnTurnChanged;
        GameEvents.OnGameStateChanged -= OnGameStateChanged;

        DestroyLaneLandscapes();
    }

    private void DestroyLaneLandscapes()
    {
        for (int i = 0; i < 3; i++)
        {
            if (p1LaneLandscapes[i] != null) { Destroy(p1LaneLandscapes[i]); p1LaneLandscapes[i] = null; }
            if (p2LaneLandscapes[i] != null) { Destroy(p2LaneLandscapes[i]); p2LaneLandscapes[i] = null; }
        }
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.TurnStart)
            RefreshBoard();
    }

    private void Update()
    {
        bool dirty = cardScale != prevCardScale;
        if (!dirty) dirty = LaneArrayDirty(player1LaneSettings, prevP1LaneSettings);
        if (!dirty) dirty = LaneArrayDirty(player2LaneSettings, prevP2LaneSettings);

        if (dirty)
        {
            CopyLaneArrayInto(player1LaneSettings, prevP1LaneSettings);
            CopyLaneArrayInto(player2LaneSettings, prevP2LaneSettings);
            prevCardScale = cardScale;
            RefreshBoard();
        }
    }

    private bool LaneArrayDirty(LaneConfig[] current, LaneConfig[] prev)
    {
        if (prev == null) return true;
        for (int i = 0; i < 3; i++)
            if (!LaneConfigsEqual(current[i], prev[i]))
                return true;
        return false;
    }

    private void CopyLaneArrayInto(LaneConfig[] src, LaneConfig[] dst)
    {
        for (int i = 0; i < 3; i++)
            CopyLaneConfig(src[i], dst[i]);
    }

    private void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool tracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (tracked && !isTracked)
        {
            isTracked = true;
            RefreshBoard();
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
        UpdatePlayerLanes(players[0], player1Lanes, p1SpawnedCreatures, p1SpawnedBuildings, player1LaneSettings);
        UpdatePlayerLanes(players[1], player2Lanes, p2SpawnedCreatures, p2SpawnedBuildings, player2LaneSettings);
    }

    private void UpdatePlayerLanes(PlayerState player, Transform[] anchors, GameObject[] spawnedCreatures, GameObject[] spawnedBuildings, LaneConfig[] settings)
    {
        GameObject[] laneLandscapes = player == GameManager.Instance?.Players[0] ? p1LaneLandscapes : p2LaneLandscapes;

        for (int i = 0; i < 3; i++)
        {
            HandleCardVisual(player.CreatureLanes[i], anchors[i], ref spawnedCreatures[i], settings[i], false);
            HandleCardVisual(player.BuildingLanes[i], anchors[i], ref spawnedBuildings[i], settings[i], true);

            if (anchors[i] == null) continue;

            LandscapeType landscape = player.Landscapes != null && i < player.Landscapes.Length
                ? player.Landscapes[i]
                : LandscapeType.Nicelands;

            // Prefab 3D del paisaje
            GameObject landscapePrefab = GetLandscapePrefab(landscape);
            if (landscapePrefab != null)
            {
                if (laneLandscapes[i] == null || laneLandscapes[i].name != landscapePrefab.name)
                {
                    if (laneLandscapes[i] != null)
                        Destroy(laneLandscapes[i]);
                    laneLandscapes[i] = Instantiate(landscapePrefab, anchors[i]);
                    laneLandscapes[i].transform.localRotation = Quaternion.identity;
                }
                if (laneLandscapes[i] != null)
                    laneLandscapes[i].transform.localPosition = settings[i].landscapeOffset;
            }
            else
            {
                if (laneLandscapes[i] != null)
                {
                    Destroy(laneLandscapes[i]);
                    laneLandscapes[i] = null;
                }
            }

            // Color de respaldo en el plano
            MeshRenderer plane = anchors[i].GetComponentInChildren<MeshRenderer>();
            if (plane != null)
            {
                bool usePlane = landscapePrefab == null;
                plane.gameObject.SetActive(usePlane);
                if (usePlane)
                    plane.material.color = GetLandscapeColor(landscape);
            }
        }
    }

    private void HandleCardVisual(CardInstance card, Transform anchor, ref GameObject spawnedObj, LaneConfig cfg, bool isBuilding)
    {
        if (card == null)
        {
            if (spawnedObj != null)
            {
                basePositions.Remove(spawnedObj);
                baseScales.Remove(spawnedObj);
                Destroy(spawnedObj);
                spawnedObj = null;
            }
            return;
        }

        if (spawnedObj == null)
        {
            GameObject prefab = isBuilding && card.Data.buildingPrefab != null
                ? card.Data.buildingPrefab
                : card.Data.creaturePrefab;
            if (prefab != null)
            {
                spawnedObj = Instantiate(prefab, anchor);
                basePositions[spawnedObj] = spawnedObj.transform.localPosition;
                baseScales[spawnedObj] = spawnedObj.transform.localScale;
            }
        }

        if (spawnedObj != null)
        {
            Vector3 offset = isBuilding ? cfg.buildingOffset : cfg.creatureOffset;
            spawnedObj.transform.localPosition = basePositions[spawnedObj] + offset;
            spawnedObj.transform.localRotation = Quaternion.Euler(cfg.cardRotation);
            spawnedObj.transform.localScale = Vector3.Scale(baseScales[spawnedObj], cardScale);
            spawnedObj.GetComponent<ARCardVisual>()?.UpdateVisual(card);
        }
    }
    private void OnCardChanged(int playerIndex, int laneIndex, CardInstance card) => RefreshBoard();
    private void OnCardRemoved(int playerIndex, int laneIndex) => RefreshBoard();
    private void OnTurnChanged(int activePlayerIndex) => RefreshBoard();
}