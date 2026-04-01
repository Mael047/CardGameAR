using UnityEngine;

public class CardInstance
{
    public CardData Data { get; private set; }

    public int AccumulatedDamage { get; private set; }
    public CardState CurrentState { get; private set; }
    public int LaneIndex { get; private set; }

    public int EffectiveAttack => Data.attack + attackBonus;
    public int EffectiveDefense => Data.defense + defenseBonus;
    public bool IsAlive => AccumulatedDamage < EffectiveDefense;
    public bool CanAttack => CurrentState == CardState.Ready && IsAlive;
    public bool CanFloop => CurrentState == CardState.Ready
                                 && IsAlive
                                 && Data.abilityType == AbilityType.Floop;

    private int attackBonus;
    private int defenseBonus;

    public CardInstance(CardData data)
    {
        Data = data;
        AccumulatedDamage = 0;
        CurrentState = CardState.Ready;
        LaneIndex = -1;
        attackBonus = 0;
        defenseBonus = 0;
    }

    public void PlaceInLane(int laneIndex)
    {
        LaneIndex = laneIndex;
        CurrentState = CardState.Ready;
    }

    public bool TakeDamage(int amount)
    {
        int finalDamage = Mathf.Max(0, amount);
        AccumulatedDamage += finalDamage;
        Debug.Log($"{Data.cardName} recibió {finalDamage} de daño. " +
                  $"Total: {AccumulatedDamage}/{EffectiveDefense}");
        return !IsAlive;
    }

    public bool ActivateFloop()
    {
        if (!CanFloop)
        {
            Debug.LogWarning($"{Data.cardName} no puede activar Floop.");
            return false;
        }
        CurrentState = CardState.Flooped;
        Debug.Log($"{Data.cardName} activó Floop.");
        return true;
    }

    public void MarkAsExhausted()
    {
        CurrentState = CardState.Exhausted;
    }

    public void ReadyUp()
    {
        CurrentState = CardState.Ready;
        attackBonus = 0;
        defenseBonus = 0;
        Debug.Log($"{Data.cardName} lista.");
    }

    public void AddAttackBonus(int bonus) => attackBonus += bonus;
    public void AddDefenseBonus(int bonus) => defenseBonus += bonus;

    public void RemoveFromField()
    {
        LaneIndex = -1;
        AccumulatedDamage = 0;
        CurrentState = CardState.Ready;
        attackBonus = 0;
        defenseBonus = 0;
    }
}