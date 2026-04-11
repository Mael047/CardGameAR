using UnityEngine;

public class ARPlacementManager : MonoBehaviour
{
    public static ARPlacementManager Instance { get; private set; }

    [SerializeField] private ARBoardManager board;

    // Guardamos el carril que el jugador seleccionó en la UI
    private int waitingLaneIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Paso 1: La UI llama a esto cuando tocas un carril en pantalla
    public void SetWaitingLane(int laneIndex)
    {
        waitingLaneIndex = laneIndex;
        Debug.Log($"AR: Carril {laneIndex} seleccionado mediante UI. Esperando escaneo...");
    }

    // Paso 2: El ARCardTracker llama a esto cuando detecta la tarjeta
    public void TryPlaceCard(string qrID, Vector3 worldPosition)
    {
        // 1. Verificamos que se haya seleccionado un carril previamente en la UI
        if (waitingLaneIndex == -1)
        {
            Debug.LogWarning("AR: Se detectó una carta, pero no hay un carril seleccionado en la UI.");
            return;
        }

        // 2. Buscamos la instancia de la carta en la mano
        CardInstance cardInHand = FindCardInHand(qrID);
        if (cardInHand == null)
        {
            Debug.LogWarning($"AR: La carta con ID {qrID} no está en tu mano.");
            return;
        }

        // 3. Intentamos jugar la carta directamente en el carril de la UI
        // Ya NO usamos worldPosition ni GetClosestLane
        bool success = GameManager.Instance.TryPlayCreature(cardInHand, waitingLaneIndex);

        if (success)
        {
            Debug.Log($"AR: {cardInHand.Data.cardName} jugada con éxito en el carril {waitingLaneIndex}!");

            // IMPORTANTE: Reseteamos el carril de espera para la siguiente jugada
            waitingLaneIndex = -1;
        }
    }

    private CardInstance FindCardInHand(string qrID)
    {
        PlayerState activePlayer = GameManager.Instance.ActivePlayer;

        foreach (CardInstance card in activePlayer.Hand)
        {
            if (card.Data.qrID == qrID) return card;
        }
        return null;
    }
}