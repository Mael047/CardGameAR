using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public struct LandscapeSpriteEntry
{
    public LandscapeType type;
    public Sprite sprite;
}

public class SetupPanel : MonoBehaviour
{
    public static SetupPanel Instance { get; private set; }

    [Header("UI del panel")]
    [SerializeField] private TMP_Text textInstruction;
    [SerializeField] private TMP_Text textCurrentPlayer;
    [SerializeField] private Button buttonConfirmSetup;

    [Header("Carriles — UI visual")]
    [SerializeField] private Button[] laneButtons;
    [SerializeField] private TMP_Text[] laneTexts;
    [SerializeField] private Image[] laneBackgrounds;

    [Header("Sprites de paisajes")]
    [SerializeField] private LandscapeSpriteEntry[] landscapeSprites;

    [Header("Botones de paisajes disponibles")]
    [SerializeField] private Transform landscapeButtonContainer;
    [SerializeField] private Button landscapeButtonPrefab;

    private static Dictionary<LandscapeType, Sprite> spriteMap;

    public static Sprite GetLandscapeSprite(LandscapeType landscape)
    {
        if (spriteMap != null && spriteMap.TryGetValue(landscape, out Sprite s))
            return s;
        return null;
    }

    private void BuildSpriteMap()
    {
        spriteMap = new Dictionary<LandscapeType, Sprite>();
        if (landscapeSprites == null) return;
        foreach (var entry in landscapeSprites)
            if (!spriteMap.ContainsKey(entry.type))
                spriteMap[entry.type] = entry.sprite;
    }

    // ── Estado interno ────────────────────────────────────────────────────
    // El jugador que está configurando actualmente (0 o 1)
    private int setupPlayerIndex = 0;

    // Los paisajes asignados a cada carril para el jugador actual
    // null = carril no asignado todavía
    private LandscapeType?[] assignedLandscapes = new LandscapeType?[3];

    // El paisaje que el jugador tiene seleccionado para colocar
    private LandscapeType? selectedLandscape = null;

    // Cuántos de cada paisaje le quedan por colocar al jugador actual
    private Dictionary<LandscapeType, int> availableLandscapes
        = new Dictionary<LandscapeType, int>();

    // Botones de paisaje instanciados (para poder destruirlos y recrearlos)
    private List<Button> spawnedLandscapeButtons = new List<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildSpriteMap();
    }

    private void Start()
    {
        buttonConfirmSetup.onClick.AddListener(OnConfirmPressed);
        buttonConfirmSetup.interactable = false;

        for (int i = 0; i < laneButtons.Length; i++)
        {
            int capturedIndex = i;
            laneButtons[i].onClick.AddListener(() => OnLaneButtonPressed(capturedIndex));
        }

        GameEvents.OnGameStateChanged += HandleStateChanged;

        // Si GameManager ya está en Setup (ejecutó su Start antes que nosotros)
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Setup)
        {
            gameObject.SetActive(true);
            StartSetupForPlayer(0);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnGameStateChanged -= HandleStateChanged;
        buttonConfirmSetup.onClick.RemoveAllListeners();
        foreach (Button b in laneButtons) b.onClick.RemoveAllListeners();
    }

    private void HandleStateChanged(GameState state)
    {
        // Solo visible durante el Setup
        gameObject.SetActive(state == GameState.Setup);

        if (state == GameState.Setup)
            StartSetupForPlayer(0); // Siempre empieza el jugador 0
    }

    // ── Inicializa la configuración para un jugador ───────────────────────
    private void StartSetupForPlayer(int playerIndex)
    {
        setupPlayerIndex = playerIndex;
        selectedLandscape = null;
        assignedLandscapes = new LandscapeType?[3];

        // Carga los paisajes disponibles desde el DeckData del jugador
        availableLandscapes.Clear();
        DeckData deck = GameManager.Instance.Players[playerIndex].DeckData;

        foreach (LandscapeType landscape in deck.landscapes)
        {
            if (!availableLandscapes.ContainsKey(landscape))
                availableLandscapes[landscape] = 0;
            availableLandscapes[landscape]++;
        }

        // Actualiza la UI
        textCurrentPlayer.text = $"Jugador {playerIndex + 1}: coloca tus paisajes";
        textInstruction.text = "Selecciona un paisaje y luego el carril donde colocarlo.";
        buttonConfirmSetup.interactable = false;

        RefreshLaneButtons();
        RefreshLandscapeButtons();
    }

    private void RefreshLaneButtons()
    {
        for (int i = 0; i < 3; i++)
        {
            if (assignedLandscapes[i].HasValue)
            {
                LandscapeType landscape = assignedLandscapes[i].Value;
                laneTexts[i].text = landscape.ToString();
                laneButtons[i].interactable = false;
                if (laneBackgrounds != null && i < laneBackgrounds.Length && laneBackgrounds[i] != null)
                {
                    laneBackgrounds[i].sprite = GetLandscapeSprite(landscape);
                    laneBackgrounds[i].color = Color.white;
                }
            }
            else
            {
                laneTexts[i].text = "— vacío —";
                laneButtons[i].interactable = selectedLandscape.HasValue;
                if (laneBackgrounds != null && i < laneBackgrounds.Length && laneBackgrounds[i] != null)
                {
                    laneBackgrounds[i].sprite = null;
                    laneBackgrounds[i].color = Color.clear;
                }
            }
        }
    }

    private void RefreshLandscapeButtons()
    {
        foreach (Button b in spawnedLandscapeButtons)
            if (b != null) Destroy(b.gameObject);
        spawnedLandscapeButtons.Clear();

        if (landscapeButtonPrefab == null)
        {
            Debug.LogError("SetupPanel: landscapeButtonPrefab no asignado en el Inspector.");
            return;
        }
        if (landscapeButtonContainer == null)
        {
            Debug.LogError("SetupPanel: landscapeButtonContainer no asignado en el Inspector.");
            return;
        }

        foreach (var kvp in availableLandscapes)
        {
            if (kvp.Value <= 0) continue;

            LandscapeType capturedType = kvp.Key;
            int count = kvp.Value;

            Button newBtn = Instantiate(landscapeButtonPrefab, landscapeButtonContainer);
            TMP_Text btnText = newBtn.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
                btnText.text = $"{capturedType} (x{count})";
            else
                Debug.LogWarning($"SetupPanel: el prefab no tiene TMP_Text child para {capturedType}");

            newBtn.onClick.AddListener(() => OnLandscapeSelected(capturedType));
            spawnedLandscapeButtons.Add(newBtn);
        }

    }

    // ── El jugador selecciona un tipo de paisaje ──────────────────────────
    private void OnLandscapeSelected(LandscapeType landscape)
    {
        AudioManager.Instance?.PlayButtonClick();
        selectedLandscape = landscape;
        textInstruction.text = $"Paisaje '{landscape}' seleccionado. Ahora elige un carril.";

        // Habilita los carriles vacíos
        RefreshLaneButtons();
    }

    // ── El jugador presiona un carril para colocar el paisaje ─────────────
    private void OnLaneButtonPressed(int laneIndex)
    {
        AudioManager.Instance?.PlayButtonClick();
        if (!selectedLandscape.HasValue) return;
        if (assignedLandscapes[laneIndex].HasValue) return; // Ya ocupado

        // Asigna el paisaje al carril
        assignedLandscapes[laneIndex] = selectedLandscape.Value;

        // Descuenta del inventario disponible
        availableLandscapes[selectedLandscape.Value]--;
        selectedLandscape = null;

        textInstruction.text = "Paisaje colocado. Selecciona el siguiente.";

        RefreshLandscapeButtons();
        RefreshLaneButtons();

        // Si los 3 carriles están llenos, habilita Confirmar
        bool allFilled = assignedLandscapes[0].HasValue
                      && assignedLandscapes[1].HasValue
                      && assignedLandscapes[2].HasValue;

        buttonConfirmSetup.interactable = allFilled;

        if (allFilled)
            textInstruction.text = "¡Listo! Presiona Confirmar para continuar.";
    }

    // ── El jugador confirma su configuración ──────────────────────────────
    private void OnConfirmPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        // Aplica los paisajes elegidos al estado del jugador
        PlayerState player = GameManager.Instance.Players[setupPlayerIndex];
        for (int i = 0; i < 3; i++)
            player.SetLandscape(i, assignedLandscapes[i].Value);

        if (setupPlayerIndex == 0)
        {
            // Jugador 1 terminó — turno del jugador 2
            textInstruction.text = "Jugador 1 listo. Ahora el Jugador 2 coloca sus paisajes.";
            StartSetupForPlayer(1);
        }
        else
        {
            // Ambos jugadores configurados — empieza el juego
            textInstruction.text = "¡Ambos jugadores listos! Comenzando partida...";
            gameObject.SetActive(false);
            GameManager.Instance.StartGame();
        }
    }
}