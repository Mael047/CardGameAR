using UnityEngine;
using System.Collections.Generic;

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

        if (allCards == null || allCards.Length == 0)
        {
            Debug.LogWarning("ARManager: el array 'All Cards' está vacío. " +
                             "Arrastra todos los CardData assets al Inspector.");
            return;
        }

        foreach (CardData card in allCards)
        {
            if (card == null) continue;

            if (string.IsNullOrEmpty(card.qrID))
            {
                Debug.LogWarning($"ARManager: {card.cardName} no tiene QR ID asignado.");
                continue;
            }

            if (cardDataCache.ContainsKey(card.qrID))
            {
                Debug.LogWarning($"ARManager: QR ID duplicado '{card.qrID}' " +
                                 $"en {card.cardName}. Se ignorará.");
                continue;
            }

            cardDataCache[card.qrID] = card;
        }

        Debug.Log($"ARManager: caché construida con {cardDataCache.Count} cartas.");
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

    // Busca si hay alguna carta siendo trackeada que aún esté en la mano del jugador activo
    public bool GetTrackedCardInHand(out string qrID, out Vector3 position)
    {
        qrID = null;
        position = Vector3.zero;

        PlayerState activePlayer = GameManager.Instance?.ActivePlayer;
        if (activePlayer == null) return false;

        foreach (var kvp in activeTrackers)
        {
            if (kvp.Value == null || !kvp.Value.IsTracked) continue;
            CardInstance instance = FindCardInstance(kvp.Key);
            if (instance != null && activePlayer.Hand.Contains(instance))
            {
                qrID = kvp.Key;
                position = kvp.Value.transform.position;
                return true;
            }
        }
        return false;
    }

}