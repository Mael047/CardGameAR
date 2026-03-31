public enum CardType
{
    Creature,
    Vuilding,
    Spell
}

public enum  LandscapeType
{
    Nicelands,
    Cornfield,
    UselessSwamp,
    BluePlains,
    Rainbow
}

public enum AbilityType
{
    None,
    Passive,
    Floop
}

public enum CardState
{
    Ready,     // Vertical - Puede atacar y usar floop
    Flooped,    // HOrizontal , no puede atacar 
    Exhausted   // horizontal hacia el oponente, ya ataco.
}

public enum GameState
{
    Setup,      //COnfiguracion inicial 
    TurnStart,  //Paso 1 y 2
    Actions,    // Paso 3 y 4
    Fight,      // Paso 5
    EndTurn,    // Paso 6
    GameOver // Final del juego 
}