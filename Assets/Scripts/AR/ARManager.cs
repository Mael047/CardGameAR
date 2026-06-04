using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Vuforia;

[DefaultExecutionOrder(-500)]
public class ARManager : MonoBehaviour
{
    public static ARManager Instance { get; private set; }

    [Header("Base de datos de cartas")]
    [SerializeField] private CardData[] allCards;

    private Dictionary<string, ARCardTracker> activeTrackers
        = new Dictionary<string, ARCardTracker>();

    private Dictionary<string, CardData> cardDataCache
        = new Dictionary<string, CardData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildCardDataCache();

#if UNITY_EDITOR
        string savedCam = PlayerPrefs.GetString("SelectedCameraName", "");
        if (!string.IsNullOrEmpty(savedCam))
        {
            string disponibles = "";
            foreach (var d in WebCamTexture.devices)
                disponibles += $"  '{d.name}'\n";

            bool camExiste = false;
            foreach (var d in WebCamTexture.devices)
            {
                if (d.name == savedCam) { camExiste = true; break; }
            }

            if (camExiste)
            {
                VuforiaConfiguration.Instance.WebCam.DeviceNameSetInEditor = savedCam;
                Debug.Log($"ARManager: cámara '{savedCam}' encontrada, aplicada a Vuforia\nCámaras disponibles:\n{disponibles}");
            }
            else
            {
                Debug.LogWarning($"ARManager: cámara '{savedCam}' NO encontrada entre las disponibles:\n{disponibles}Se usará la camera default de Vuforia");
            }
        }
        else
        {
            Debug.Log("ARManager: no hay cámara guardada en PlayerPrefs, se usará la default de Vuforia");
        }
#endif

        Debug.Log($"ARManager: ¿Vuforia ya inicializado? {VuforiaApplication.Instance.IsInitialized}");
    }

    private void Start()
    {
        StartCoroutine(EnsureCorrectCamera());
    }

    private IEnumerator EnsureCorrectCamera()
    {
#if UNITY_EDITOR
        string savedCam = PlayerPrefs.GetString("SelectedCameraName", "");
        if (string.IsNullOrEmpty(savedCam)) yield break;

        float timeout = 10f;
        while (!VuforiaApplication.Instance.IsInitialized && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!VuforiaApplication.Instance.IsInitialized)
        {
            Debug.LogError("ARManager: Vuforia no inicializó");
            yield break;
        }

        VuforiaConfiguration.Instance.WebCam.DeviceNameSetInEditor = savedCam;
        Debug.Log($"ARManager: reiniciando Vuforia con cámara '{savedCam}'");
        VuforiaApplication.Instance.Deinit();

        float wait = 2f;
        while (VuforiaApplication.Instance.IsInitialized && wait > 0)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        VuforiaApplication.Instance.Initialize();

        timeout = 5f;
        while (!VuforiaApplication.Instance.IsInitialized && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (VuforiaApplication.Instance.IsInitialized)
            Debug.Log($"ARManager: Vuforia reiniciado con cámara '{savedCam}'");
        else
            Debug.LogError("ARManager: Vuforia no pudo reiniciarse");
#else
        yield break;
#endif
    }

    private void OnEnable()
    {
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnCardDestroyed += HandleCardDestroyed;
        GameEvents.OnFloopActivated += HandleFloopActivated;
        GameEvents.OnTurnChanged += HandleTurnChanged;
        GameEvents.OnHPChanged += HandleHPChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnCardPlayed -= HandleCardPlayed;
        GameEvents.OnCardDestroyed -= HandleCardDestroyed;
        GameEvents.OnFloopActivated -= HandleFloopActivated;
        GameEvents.OnTurnChanged -= HandleTurnChanged;
        GameEvents.OnHPChanged -= HandleHPChanged;
    }

    private void BuildCardDataCache()
    {
        cardDataCache.Clear();

        if (allCards != null)
        {
            foreach (CardData card in allCards)
            {
                if (card == null) continue;
                TryRegisterCard(card);
            }
            if (cardDataCache.Count > 0)
            {
                Debug.Log($"ARManager: caché construida con {cardDataCache.Count} cartas desde Inspector");
                return;
            }
        }

        // Fallback para builds: buscar CardData cargados por dependencias (DeckData -> CardData)
        var found = Resources.FindObjectsOfTypeAll<CardData>();
        if (found != null && found.Length > 0)
        {
            foreach (CardData card in found)
            {
                if (card == null) continue;
                TryRegisterCard(card);
            }
            Debug.Log($"ARManager: caché construida con {cardDataCache.Count} cartas vía FindObjectsOfTypeAll");
            return;
        }

        Debug.LogWarning("ARManager: no se encontraron CardData. " +
                         "Asegúrate de que los assets estén en Resources o asignados en el Inspector.");
    }

    private void TryRegisterCard(CardData card)
    {
        if (string.IsNullOrEmpty(card.qrID))
        {
            Debug.LogWarning($"ARManager: {card.cardName} no tiene QR ID asignado.");
            return;
        }
        if (cardDataCache.ContainsKey(card.qrID))
        {
            Debug.LogWarning($"ARManager: QR ID duplicado '{card.qrID}' en {card.cardName}. Se ignorará.");
            return;
        }
        cardDataCache[card.qrID] = card;
        Debug.Log($"ARManager: registrada carta '{card.cardName}' con QR '{card.qrID}'");
    }


    public void RegisterTracker(string qrID, ARCardTracker tracker)
    {
        activeTrackers[qrID] = tracker;
        Debug.Log($"ARManager: tracker registrado para '{qrID}'. " +
                  $"Total activos: {activeTrackers.Count}");
    }

    public void UnregisterTracker(string qrID)
    {
        if (activeTrackers.ContainsKey(qrID))
        {
            activeTrackers.Remove(qrID);
            Debug.Log($"ARManager: tracker eliminado para '{qrID}'.");
        }
    }

    public CardData FindCardData(string qrID)
    {
        if (cardDataCache.TryGetValue(qrID, out CardData data))
            return data;

        Debug.LogWarning($"ARManager: no se encontró CardData para QR '{qrID}'.");
        return null;
    }

    public CardInstance FindCardInstance(string qrID)
    {
        if (GameManager.Instance == null) return null;

        foreach (PlayerState player in GameManager.Instance.Players)
        {
            // Busca en la mano
            foreach (CardInstance card in player.Hand)
                if (card.Data.qrID == qrID) return card;

            // Busca en carriles de criaturas
            foreach (CardInstance card in player.CreatureLanes)
                if (card != null && card.Data.qrID == qrID) return card;

            // Busca en carriles de edificios
            foreach (CardInstance card in player.BuildingLanes)
                if (card != null && card.Data.qrID == qrID) return card;
        }

        return null;
    }


    private void HandleCardPlayed(int playerIndex, int laneIndex, CardInstance card)
    {
        // Refresca el visual de la carta que fue jugada
        RefreshTrackerForCard(card);
    }

    private void HandleCardDestroyed(int playerIndex, int laneIndex)
    {
        // Refresca todos los trackers activos porque el campo cambió
        RefreshAllTrackers();
    }

    private void HandleFloopActivated(int playerIndex, int laneIndex)
    {
        // Busca la carta en el carril y refresca su visual
        if (GameManager.Instance == null) return;
        CardInstance card = GameManager.Instance.Players[playerIndex].CreatureLanes[laneIndex];
        if (card != null) RefreshTrackerForCard(card);
    }

    private void HandleTurnChanged(int activePlayerIndex)
    {
        // Al cambiar turno, refresca todos los visuals (estados Ready/Exhausted cambian)
        RefreshAllTrackers();
    }

    private void HandleHPChanged(int playerIndex, int newHP)
    {
        // El HP cambió — por ahora solo log; en el futuro puede mostrar efecto visual
        Debug.Log($"ARManager: Jugador {playerIndex + 1} HP → {newHP}");
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    // Refresca el visual AR de una carta específica
    private void RefreshTrackerForCard(CardInstance card)
    {
        if (card == null || card.Data == null) return;

        string qrID = card.Data.qrID;
        if (activeTrackers.TryGetValue(qrID, out ARCardTracker tracker))
        {
            // BUSCAMOS EL COMPONENTE VISUAL QUE SÍ TIENE LA FUNCIÓN
            var visual = tracker.GetComponent<ARCardVisual>();
            if (visual != null)
            {
                visual.UpdateVisual(card); // Nota: En tu ARCardVisual se llama UpdateVisual
            }
        }
    }

    // Refresca todos los trackers activos de una vez
    private void RefreshAllTrackers()
    {
        foreach (var kvp in activeTrackers)
        {
            ARCardTracker tracker = kvp.Value;
            if (tracker == null) continue;

            // Buscamos la lógica de la carta
            CardInstance instance = FindCardInstance(tracker.qrID);

            // Buscamos el componente visual en el objeto detectado
            ARCardVisual visual = tracker.GetComponent<ARCardVisual>();

            if (instance != null && visual != null)
            {
                visual.UpdateVisual(instance);
            }
        }
    }

    // Devuelve cuántos trackers están activos (útil para debug)
    public int ActiveTrackerCount => activeTrackers.Count;

}