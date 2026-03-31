using UnityEngine;
using System;

public static class GameEvents
{
    // HP de un jugador cambió (0 = Player1, 1 = Player2, nuevoHP)
    public static Action<int, int> OnHPChanged;

    // El estado del juego cambió (nuevo estado)
    public static Action<GameState> OnGameStateChanged;

    // Una carta fue jugada al campo (jugador, carril, instancia)
    public static Action<int, int, CardInstance> OnCardPlayed;

    // Una carta fue destruida (jugador, carril)
    public static Action<int, int> OnCardDestroyed;

    // Una criatura activó Floop (jugador, carril)
    public static Action<int, int> OnFloopActivated;

    // Una criatura atacó (jugadorAtacante, carrilAtacante, dañoInfligido)
    public static Action<int, int, int> OnCreatureAttacked;

    // Daño directo a jugador (jugadorReceptor, daño)
    public static Action<int, int> OnDirectDamage;

    // El turno cambió (índice del jugador activo: 0 o 1)
    public static Action<int> OnTurnChanged;

    // El juego terminó (índice del jugador ganador)
    public static Action<int> OnGameOver;

    // Una carta fue robada (jugador, carta)
    public static Action<int, CardInstance> OnCardDrawn;

}
