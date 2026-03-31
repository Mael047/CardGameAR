using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "CardWars/Card Data")]

public class CardData : ScriptableObject
{
    [Header("Card Info")]

    public string cardName;
    public string qrID;        //ID de la carta, para AR
    public Sprite cardArtwork; //imagen de la carta 

    [Header("Tipo y costo")]
    public CardType cardType;
    public int actionCost;
    public LandscapeType landscapeRequired;
    public int landscapeAmount;

    [Header("Stats")]
    [Min(0)] public int attack;
    [Min(0)] public int defense;

    [Header("Habilidades")]
    public AbilityType abilityType;
    public int abilityActionCost;

    [TextArea(2,4)]
    public string abilityDescription;

    [Header("AR")]
    public GameObject creaturePrefab;
    public AnimationClip floopAnimation;
    public ParticleSystem spellEffect;
}
