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
    // Retorna true si la carta se colocó exitosamente
    public bool TryPlaceCard(string qrID, Vector3 worldPosition)
    {
        if (waitingLaneIndex == -1)
        {
            Debug.LogWarning("AR: Se detectó una carta, pero no hay un carril seleccionado en la UI.");
            return false;
        }

        CardInstance cardInHand = FindCardInHand(qrID);
        if (cardInHand == null)
        {
            Debug.LogWarning($"AR: La carta con ID {qrID} no está en tu mano.");
            return false;
        }

        bool success = false;

        switch (cardInHand.Data.cardType)
        {
            case CardType.Creature:
                success = GameManager.Instance.TryPlayCreature(cardInHand, waitingLaneIndex);
                break;

            case CardType.Building:
                success = GameManager.Instance.TryPlayBuilding(cardInHand, waitingLaneIndex);
                break;

            case CardType.Spell:
                success = GameManager.Instance.TryPlaySpell(cardInHand, waitingLaneIndex);
                break;

            default:
                Debug.LogWarning($"AR: Tipo de carta desconocido: {cardInHand.Data.cardType}");
                break;
        }

        if (success)
        {
            Debug.Log($"AR: {cardInHand.Data.cardName} jugada con éxito en el carril {waitingLaneIndex}!");
            waitingLaneIndex = -1;
        }
        else
        {
            Debug.LogWarning($"AR: No se pudo jugar {cardInHand.Data.cardName} en carril {waitingLaneIndex}.");
            ActionsPanel.Instance?.ShowNotification(
                $"No se puede colocar {cardInHand.Data.cardName} en el carril {waitingLaneIndex + 1}.");
        }

        return success;
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