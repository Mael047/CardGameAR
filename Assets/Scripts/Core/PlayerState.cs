using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class PlayerState
{
    public string PlayerName { get; private set; }
    public DeckData DeckData { get; private set; }

    //Vida
    public int CurrentHP { get; private set; }
    public const int MAX_HP = 15;

    //Acciones del turno 
    public int ActionsRemaining { get; private set; }
    public const int ACTIONS_PER_TURN = 2;

    //Cartas 
    public Stack<CardInstance> Deck { get; private set; }
    public List<CardInstance> Hand { get; private set; }
    public List<CardInstance> Discard { get; private set; }

    // Campo de batalla 
    //Criatura del carril 
    public CardInstance[] CreatureLanes { get; private set; }
    //edificio del carril
    public CardInstance[] BuildingLanes { get; private set; }
    //paisaje del carril
    public LandscapeType[] Landscapes { get; private set; }


    public bool IsAlive => CurrentHP > 0;
    public bool HasCards =>Deck.Count > 0;
    public int CardsInHand => Hand.Count;

    public PlayerState(string name, DeckData deckdata)
    {
        PlayerName = name;
        DeckData = deckdata;
        CurrentHP = MAX_HP;

        //Inicializar mazo
        Deck = new Stack<CardInstance>();
        Hand = new List<CardInstance>();
        Discard = new List<CardInstance>();
        CreatureLanes = new CardInstance[3]; // 3 carriles de criaturas
        BuildingLanes = new CardInstance[3];
        Landscapes = new LandscapeType[3];

        for (int i = 0; i < 3; i++)
        {
            Landscapes[i] = deckdata.landscapes[i];
        }

        BuildDeck();
    }

    private void BuildDeck()
    {
        List<CardInstance> tempList = new List<CardInstance>();

        foreach(CardData cardData in DeckData.cards)
            tempList.Add(new CardInstance(cardData));

        for(int i = tempList.Count -1; i> 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardInstance temp = tempList[i];
            tempList[i] = tempList[j];
            tempList[j] = temp;
        }

        foreach(CardInstance card in tempList)
            Deck.Push(card);

        Debug.Log($"Deck for {PlayerName} built with {Deck.Count} cards.");
    }


    public CardInstance DrawCard()
    {
        if(!HasCards)
        {
            Debug.LogWarning($"{PlayerName} has no cards left to draw!");
            return null;
        }

        CardInstance drawn = Deck.Pop();
        Hand.Add(drawn);
        Debug.Log($"{PlayerName} draws {drawn.Data.cardName}. Cards left in deck: {Hand.Count}");
        return drawn;
    }

    public void DrawInitialHand()
    {
        for(int i = 0; i < 5; i++)
        {
            DrawCard();
        }
    }

    public bool CanAfford(int cost)
    {
        return ActionsRemaining >= cost;
    }

    public bool SpendActions(int amount)
    {
        if(!CanAfford(amount))
        {
            Debug.LogWarning($"{PlayerName} cannot spend {amount} actions. Only {ActionsRemaining} remaining.");
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
        Debug.Log($"{PlayerName} takes {amount} damage. Current HP: {CurrentHP}");
    }

    // Gestion del campo de juego 

    public bool MeetsLandscapeRequirement(CardData card)
    {
        if(card.landscapeRequired == LandscapeType.Rainbow)
            return true;

        int count = 0;
        foreach(LandscapeType landscape in Landscapes)
            if(landscape == card.landscapeRequired)
                count++;

        return count >= card.landscapeAmount;

    }

    //colocacion de cartas en un carril 

    public CardInstance PlaceCreature(CardInstance creature, int laneIndex)
    {
        CardInstance replaced = CreatureLanes[laneIndex];

        if(replaced != null)
        {
            replaced.RemoveFromField();
            Discard.Add(replaced);
            Hand.Remove(creature);
            Debug.Log($"{PlayerName} places {creature.Data.cardName} in creature lane {laneIndex}, replacing {replaced.Data.cardName}.");
        }

        CreatureLanes[laneIndex] = creature;
        creature.PlaceInLane(laneIndex);
        Debug.Log($"{PlayerName} places {creature.Data.cardName} in creature lane {laneIndex}.");

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
        if(creature != null)
        {
            creature.RemoveFromField();
            Discard.Add(creature);
            CreatureLanes[laneIndex] = null;
            Debug.Log($"{PlayerName}'s creature in lane {laneIndex} is destroyed.");
        }   
    }

    public void DiscardSpeel(CardInstance spell)
    {
        Hand.Remove(spell);
        Discard.Add(spell);
        Debug.Log($"{PlayerName} discards {spell.Data.cardName} after casting.");
    }

}
