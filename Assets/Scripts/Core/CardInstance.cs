using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class CardInstance
{
    //Referencia al asset original de la carta
    public CardData Data { get; private set; }

    //Estado en combate
    public int AccumulatedDamage {  get; private set; }

    //Estado actual de la carta 
    public CardState CurrentState { get; private set; }

    //En que carril esta la carta 
    public int LaneIndex { get; private set; }


    //posibles acciones

    public int EffectiveAttack => Data.attack + attackBonus;

    public int EffectiveDefense => Data.defense + defenseBonus;

    public bool IsAlive => AccumulatedDamage < EffectiveDefense;

    public bool CanAttack => CurrentState == CardState.Ready && IsAlive;

    public bool CanFloop => CurrentState == CardState.Ready && IsAlive && Data.abilityType == AbilityType.Floop;


    //bonificacions temporales

    private int attackBonus;
    private int defenseBonus;

    //constructor
    public CardInstance(CardData data)
    {
        Data = data;
        AccumulatedDamage = 0;
        CurrentState = CardState.Ready;
        LaneIndex = -1;  //No asignada a ningun carril
        attackBonus = 0;
        defenseBonus = 0;
    }

    //Metodos del juego 

    public void PlaceInLane(int laneIndex)
    {
        LaneIndex = laneIndex;
        CurrentState = CardState.Ready;
    }

    public bool TakeDamage(int amount)
    {
        int finalDamage = Mathf.Max(0, amount);
        AccumulatedDamage += finalDamage;

        Debug.Log($"{Data.cardName} took {finalDamage} damage. Total damage: {AccumulatedDamage}/{EffectiveDefense}"); 
        return !IsAlive;
    }

    public bool ActivateFloop()
    {
        if (!CanFloop)
        {
            Debug.LogWarning($"{Data.cardName} cannot activate Floop ability");
            return false;
        }
            
        CurrentState = CardState.Flooped;
        Debug.Log($"{Data.cardName} activated Floop ability!");
        return true;
    }

    public void MarkAsExhausted()
    {
        CurrentState = CardState.Exhausted;
    }

    //Resetear el estado de inicio
    public void ReadyUp()
    {
        CurrentState = CardState.Ready;
        attackBonus = 0;
        defenseBonus = 0;
        Debug.Log($"{Data.cardName} is ready for action again!");
    }

    //Bonifiicaciones
    public void AddAttackBonus(int bonus)
    {
        attackBonus += bonus;
    }
    
    public void AddDefenseBonus(int bonus)
    {
        defenseBonus += bonus;
    }

    //Limpiar la carta que sale o se destruye 
    public void RemoveFromField()
    {
        LaneIndex = -1;
        AccumulatedDamage = 0;
        CurrentState = CardState.Ready;
        attackBonus = 0;
        defenseBonus= 0;
    }

}

