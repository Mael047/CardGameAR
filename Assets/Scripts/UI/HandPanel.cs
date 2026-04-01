using UnityEngine;
using System.Collections.Generic;

public class HandPanel : MonoBehaviour
{
    [Header("Prefabs y Contenedores")]
    [SerializeField] private CardUI cardPrefab;
    [SerializeField] private Transform cardsContainer;

    private CardUI selectedCard;

    public void Refresh()
    {
        // Validación: si faltan referencias, avisa en consola y no explota
        if (cardPrefab == null)
        {
            Debug.LogError("HandPanel: Card Prefab no asignado en el Inspector.");
            return;
        }
        if (cardsContainer == null)
        {
            Debug.LogError("HandPanel: Cards Container no asignado en el Inspector.");
            return;
        }
        if (GameManager.Instance == null) return;

        // Destruye todas las cartas visuales actuales
        foreach (Transform child in cardsContainer)
            Destroy(child.gameObject);

        selectedCard = null;

        // Recrea una CardUI por cada carta en la mano del jugador activo
        List<CardInstance> hand = GameManager.Instance.ActivePlayer.Hand;

        if (hand == null || hand.Count == 0)
        {
            Debug.Log("HandPanel: la mano está vacía.");
            return;
        }

        foreach (CardInstance cardInstance in hand)
        {
            CardUI newCardUI = Instantiate(cardPrefab, cardsContainer);
            newCardUI.Setup(cardInstance, OnCardSelected);
        }

        Debug.Log($"HandPanel: mostrando {hand.Count} cartas.");
    }

    private void OnCardSelected(CardUI cardUI)
    {
        if (selectedCard == cardUI)
        {
            Deselect();
            return;
        }

        selectedCard = cardUI;
        ActionsPanel.Instance.ShowCardOptions(cardUI.CardInstance);
        Debug.Log($"Carta seleccionada: {cardUI.CardInstance.Data.cardName}");
    }

    public void Deselect()
    {
        selectedCard = null;
        ActionsPanel.Instance.HideCardOptions();
    }

    public CardInstance GetSelectedCard() => selectedCard?.CardInstance;
}