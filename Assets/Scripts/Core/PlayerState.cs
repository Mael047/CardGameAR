using UnityEngine;
using System.Collections.Generic;
// REMOVIDO: using UnityEditor — causa errores en builds de producción,
// solo debe usarse en scripts de herramientas del editor

public class PlayerState
{
    public string PlayerName { get; private set; }
    public DeckData DeckData { get; private set; }

    public int CurrentHP { get; private set; }
    public const int MAX_HP = 15;

    public int ActionsRemaining { get; private set; }
    public const int ACTIONS_PER_TURN = 2;

    public Stack<CardInstance> Deck { get; private set; }
    public List<CardInstance> Hand { get; private set; }
    public List<CardInstance> Discard { get; private set; }

    public CardInstance[] CreatureLanes { get; private set; }
    public CardInstance[] BuildingLanes { get; private set; }
    public LandscapeType[] Landscapes { get; private set; }

    public bool IsAlive => CurrentHP > 0;
    public bool HasCards => Deck.Count > 0;
    public int CardsInHand => Hand.Count;

    public PlayerState(string name, DeckData deckdata)
    {
        PlayerName = name;
        DeckData = deckdata;
        CurrentHP = MAX_HP;

        Deck = new Stack<CardInstance>();
        Hand = new List<CardInstance>();
        Discard = new List<CardInstance>();
        CreatureLanes = new CardInstance[3];
        BuildingLanes = new CardInstance[3];
        Landscapes = new LandscapeType[3];

        for (int i = 0; i < 3; i++)
            Landscapes[i] = deckdata.landscapes[i];

        BuildDeck();
    }

    private void BuildDeck()
    {
        List<CardInstance> tempList = new List<CardInstance>();

        foreach (CardData cardData in DeckData.cards)
            tempList.Add(new CardInstance(cardData));

        for (int i = tempList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardInstance t = tempList[i];
            tempList[i] = tempList[j];
            tempList[j] = t;
        }

        foreach (CardInstance card in tempList)
            Deck.Push(card);

        Debug.Log($"{PlayerName}: mazo construido con {Deck.Count} cartas.");
    }

    public CardInstance DrawCard()
    {
        if (!HasCards)
        {
            Debug.LogWarning($"{PlayerName}: mazo vacío.");
            return null;
        }

        CardInstance drawn = Deck.Pop();
        Hand.Add(drawn);
        // BUG CORREGIDO: el log original decía Hand.Count en lugar de Deck.Count
        // mostrando el tamaño de la mano como "cartas en el mazo", confuso para debug
        Debug.Log($"{PlayerName} draws {drawn.Data.cardName}. Cards left in deck: {Deck.Count}");
        return drawn;
    }

    public void DrawInitialHand()
    {
        for (int i = 0; i < 5; i++)
            DrawCard();
    }

    public bool CanAfford(int cost)
    {
        return ActionsRemaining >= cost;
    }

    public bool SpendActions(int amount)
    {
        if (!CanAfford(amount))
        {
            Debug.LogWarning($"{PlayerName}: no tiene {amount} acciones (tiene {ActionsRemaining}).");
            return false;
        }
        ActionsRemaining -= amount;
        return true;
    }

    public void RestoreActions()
    {
        ActionsRemaining = ACTIONS_PER_TURN;
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        Debug.Log($"{PlayerName} recibió {amount} de daño. HP: {CurrentHP}/{MAX_HP}");
    }

    public bool MeetsLandscapeRequirement(CardData card)
    {
        if (card.landscapeRequired == LandscapeType.Rainbow)
            return true;

        int count = 0;
        foreach (LandscapeType landscape in Landscapes)
            if (landscape == card.landscapeRequired)
                count++;

        return count >= card.landscapeAmount;
    }

    public CardInstance PlaceCreature(CardInstance creature, int laneIndex)
    {
        CardInstance replaced = CreatureLanes[laneIndex];

        if (replaced != null)
        {
            replaced.RemoveFromField();
            Discard.Add(replaced);
            Debug.Log($"{PlayerName}: {replaced.Data.cardName} reemplazada en carril {laneIndex}.");
        }

        // BUG CORREGIDO: el original llamaba Hand.Remove(creature) solo dentro del if
        // de reemplazo, pero la carta siempre debe salir de la mano al jugarse
        Hand.Remove(creature);
        CreatureLanes[laneIndex] = creature;
        creature.PlaceInLane(laneIndex);
        Debug.Log($"{PlayerName}: {creature.Data.cardName} colocada en carril {laneIndex}.");

        return replaced;
    }

    public CardInstance PlaceBuilding(CardInstance building, int laneIndex)
    {
        CardInstance replaced = BuildingLanes[laneIndex];

        if (replaced != null)
        {
            replaced.RemoveFromField();
            Discard.Add(replaced);
            Debug.Log($"{PlayerName}: {replaced.Data.cardName} (edificio) reemplazado.");
        }

        BuildingLanes[laneIndex] = building;
        building.PlaceInLane(laneIndex);
        Hand.Remove(building);
        return replaced;
    }

    public void DestroyCreature(int laneIndex)
    {
        CardInstance creature = CreatureLanes[laneIndex];
        if (creature == null) return;

        creature.RemoveFromField();
        Discard.Add(creature);
        CreatureLanes[laneIndex] = null;
        Debug.Log($"{PlayerName}: criatura en carril {laneIndex} destruida.");
    }

    public void DiscardSpell(CardInstance spell)
    {
        Hand.Remove(spell);
        Discard.Add(spell);
        Debug.Log($"{PlayerName}: {spell.Data.cardName} descartado.");
    }
}