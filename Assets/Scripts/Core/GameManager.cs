using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }


    [SerializeField] private DeckData deckPlayer1;
    [SerializeField] private DeckData deckPlayer2;

    //Estados del juego
    public GameState CurrentState { get; private set; }
    public PlayerState[] Players { get; private set; }
    public int ActivePlayerIndex { get; private set; }
    public int TurnNumber { get; private set; }
}
