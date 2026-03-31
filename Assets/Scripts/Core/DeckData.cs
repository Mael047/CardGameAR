using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "NewDeck", menuName = "CardWars/Deck Data")]
public class DeckData : ScriptableObject
{
    [Header("Deck")]
    public string deckName;
    public Sprite deckPortrait; //Imagen representativa del mazo

    [Header("Cards")]
    public List<CardData> cards = new List<CardData>(); //Lista de cartas que componen el mazo

    [Header("Paisajes")]
    public List<LandscapeType> landscapes = new List<LandscapeType>(); //Lista de paisajes que el mazo puede usar
}
