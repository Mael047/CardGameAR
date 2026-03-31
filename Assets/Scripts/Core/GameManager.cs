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

    public PlayerState ActivePlayer => Players[ActivePlayerIndex];
    public PlayerState OpponentPlayer => Players[1 - ActivePlayerIndex];

    private bool isFirstTurn = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeGame();
    }

    //Inicializa el juego, creando los jugadores y configurando el estado inicial

    private void InitializeGame()
    {
        TurnNumber = 1;
        isFirstTurn = true;

        Players = new PlayerState[2]
        {
            new PlayerState("Player 1", deckPlayer1),
            new PlayerState("Player 2", deckPlayer2)
        };

        ActivePlayerIndex = Random.Range(0, 2);
        Debug.Log($"Starting player: {ActivePlayerIndex + 1}");

        Players[0].DrawInitialHand();
        Players[1].DrawInitialHand();

        ChangeState(GameState.TurnStart);
    }


    private void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Estado: {newState} | Turno {TurnNumber} | " +
                  $"Jugador {ActivePlayerIndex + 1}");

        GameEvents.OnGameStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.TurnStart: HandleTurnStart(); break;
            case GameState.Actions: HandleActions(); break;
            case GameState.Fight: HandleFight(); break;
            case GameState.EndTurn: HandleEndTurn(); break;
            case GameState.GameOver: HandleGameOver(); break;
        }
    }


    private void HandleTurnStart()
    {
        ReadyAllCards();

        CardInstance drawn = ActivePlayer.DrawCard();
        if (drawn != null)
            GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);

        ActivePlayer.RestoreActions();

        StartCoroutine(TransitionAfterDelay(GameState.Actions, 0.5f));

    }

    private void ReadyAllCards()
    {
        for (int i = 0; i < 3; i++)
        {
            ActivePlayer.CreatureLanes[i]?.ReadyUp();
        }
        Debug.Log($"All cards for Player {ActivePlayerIndex + 1} are ready.");
    }

    private void HandleActions()
    {
        Debug.Log($"Jugador {ActivePlayerIndex + 1}: fase de acciones. " +
                 $"Acciones: {ActivePlayer.ActionsRemaining}");
        // La UI debe mostrar las opciones disponibles al jugador.
        // El GameManager solo espera ser llamado.
    }


    //Metodos de la UI 

    public bool TryPlayCreature(CardInstance card, int laneIndex)
    {
        if(CurrentState != GameState.Actions)
        {
            Debug.LogWarning("No se pueden jugar cartas fuera de la fase de acciones.");
            return false;
        }
        if(!ActivePlayer.Hand.Contains(card))
        {
            Debug.LogWarning("La carta no está en la mano del jugador.");
            return false;
        }
        if(card.Data.cardType != CardType.Creature)
        {
            Debug.LogWarning("Solo se pueden jugar cartas de tipo criatura en esta función.");
            return false;
        }
        if(!ActivePlayer.MeetsLandscapeRequirement(card.Data))
        {
            Debug.LogWarning("No se cumplen los requisitos de paisaje para jugar esta carta.");
            return false;
        }
        if(!ActivePlayer.CanAfford(card.Data.actionCost))
        {
            Debug.LogWarning("No se tienen suficientes recursos para jugar esta carta.");
            return false;
        }


        CardInstance existing = ActivePlayer.CreatureLanes[laneIndex];
        if(existing != null && existing.CurrentState == CardState.Flooped)
        {
            Debug.LogWarning("No se puede jugar una carta en un carril ocupado por una criatura flooped.");
            return false;
        }

        //Si la accion es valida
        ActivePlayer.SpendActions(card.Data.actionCost);
        ActivePlayer.PlaceCreature(card, laneIndex);

        ApplyOnEnterPassives(card, laneIndex);

        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        return true;

    }

    //Edificio
    public bool TryPlayBuilding(CardInstance card, int laneIndex)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Building) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;
        if (!ActivePlayer.CanAfford(card.Data.actionCost)) return false;

        ActivePlayer.SpendActions(card.Data.actionCost);
        ActivePlayer.PlaceBuilding(card, laneIndex);

        // Aplica inmediatamente el efecto pasivo del edificio
        ApplyBuildingPassive(card, laneIndex);

        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        return true;
    }

    // Intenta jugar un hechizo desde la mano.
    public bool TryPlaySpell(CardInstance card)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Spell) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;

        // Cartas Rainbow de costo 0 son gratuitas
        bool isFree = card.Data.landscapeRequired == LandscapeType.Rainbow
                   && card.Data.actionCost == 0;

        if (!isFree && !ActivePlayer.CanAfford(card.Data.actionCost)) return false;
        if (!isFree) ActivePlayer.SpendActions(card.Data.actionCost);

        // Resuelve el efecto del hechizo
        ResolveSpellEffect(card);
        ActivePlayer.DiscardSpell(card);
        return true;
    }

    // Intenta activar el Floop de una criatura en un carril.
    public bool TryFloop(int laneIndex)
    {
        if (CurrentState != GameState.Actions)
        {
            Debug.LogWarning("Solo puedes Flopear en la fase de acciones.");
            return false;
        }

        // Regla especial: el primer jugador no puede Flopear en su primer turno
        if (isFirstTurn && ActivePlayerIndex == 0)
        {
            Debug.LogWarning("El primer jugador no puede Flopear en su primer turno.");
            return false;
        }

        CardInstance creature = ActivePlayer.CreatureLanes[laneIndex];
        if (creature == null || !creature.CanFloop) return false;
        if (!ActivePlayer.CanAfford(creature.Data.abilityActionCost)) return false;

        ActivePlayer.SpendActions(creature.Data.abilityActionCost);
        creature.ActivateFloop();

        // Resuelve el efecto del Floop según qué carta es
        ResolveFloopEffect(creature, laneIndex);

        GameEvents.OnFloopActivated?.Invoke(ActivePlayerIndex, laneIndex);
        return true;
    }

    public void ProceedToFight()
    {
        if (CurrentState != GameState.Actions) return;
        ChangeState(GameState.Fight);
    }

    private void HandleFight()
    {
        if(isFirstTurn && ActivePlayerIndex == 0)
        {
            Debug.Log("Primer turno del primer jugador: no hay fase de combate.");
            ChangeState(GameState.EndTurn);
            return;
        }

        StartCoroutine(ResolveFightPhase());
    }

    private IEnumerator ResolveFightPhase()
    {
        int opponentIndex = 1 - ActivePlayerIndex;

        for (int lane = 0; lane < 3; lane++)
        {
            CardInstance attacker = ActivePlayer.CreatureLanes[lane];

            if (attacker == null || !attacker.CanAttack) continue;

            CardInstance defender = OpponentPlayer.CreatureLanes[lane];

            if(defender != null)
            {
                ResolveCombat(attacker, defender, lane, opponentIndex);
            }
            else
            {
                // Ataca al jugador directamente
                OpponentPlayer.TakeDamage(attacker.EffectiveAttack);
                GameEvents.OnDirectDamage?.Invoke(opponentIndex, attacker.EffectiveAttack);
                GameEvents.OnHPChanged?.Invoke(opponentIndex, OpponentPlayer.CurrentHP);

                Debug.Log($"Carril {lane}: daño directo de {attacker.EffectiveAttack} " +
                          $"al jugador {opponentIndex + 1}.");
            }

            attacker.MarkAsExhausted();

            yield return new WaitForSeconds(0.3f);

            if(CheckGameOver()) yield break;
        }
        ChangeState(GameState.EndTurn);
    }


    private void ResolveCombat(CardInstance attacker, CardInstance defenser, int lane, int opponentIndex)
    {
        int attackerDmg = attacker.EffectiveAttack;
        int defenserDmg = defenser.EffectiveAttack;

        bool attackerDestroyed = attacker.TakeDamage(defenserDmg);
        bool defenserDestroyed = defenser.TakeDamage(attackerDmg);  

        GameEvents.OnCreatureAttacked?.Invoke(ActivePlayerIndex, lane, attackerDmg);

        if(defenserDestroyed)
        {
            OpponentPlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(opponentIndex, lane);
        }
        if(attackerDestroyed)
        {
            ActivePlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(ActivePlayerIndex, lane);
        }

    }

    private void HandleEndTurn()
    {
        Debug.Log($"Jugador {ActivePlayerIndex + 1} ha terminado su turno.");

        if (isFirstTurn && ActivePlayerIndex == 0) 
            isFirstTurn = false;

        ActivePlayerIndex = 1 - ActivePlayerIndex;
        TurnNumber++;
        GameEvents.OnTurnChanged?.Invoke(ActivePlayerIndex);

        StartCoroutine(TransitionAfterDelay(GameState.TurnStart, 1f));
    }

    private bool CheckGameOver()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!Players[i].IsAlive)
            {
                ChangeState(GameState.GameOver);
                return true;
            }
        }
        return false;
    }   

    private void HandleGameOver()
    {
        int winner = Players[0].IsAlive ? 0:1;
        Debug.Log($"¡Jugador {winner + 1} gana el juego!");
        GameEvents.OnGameOver?.Invoke(winner);
    }


    // efectos pasivos cuando una criatura entra al campo 
    private void ApplyOnEnterPassives(CardInstance card, int laneIndex)
    {
        switch(card.Data.cardName)
        {
            case "Corn Stalker":
                //al entrar al campo, roba 1 carta 
                ActivePlayer.DrawCard();
                break;
                // aca se agregan mas habilidades de entrada
        }
    }

    private void ApplyBuildingPassive(CardInstance building, int laneIndex)
    {
        switch (building.Data.cardName)
        {
            case "Swamp Hut":
                // Pasiva: la criatura en este carril gana +1 DEF
                CardInstance creature = ActivePlayer.CreatureLanes[laneIndex];
                creature?.AddDefenseBonus(1);
                break;
                // Candy Lab se evalúa al inicio del turno, no al colocarse
        }
    }

    private void ResolveFloopEffect(CardInstance card, int laneIndex)
    {
        int opponentIndex = 1 -ActivePlayerIndex;

        switch(card.Data.cardName)
        {
            case "Fórmula Bot":
                // Floop : descarta una carta aleatoria de la mano enemiga
                if (OpponentPlayer.Hand.Count > 0)
                {
                    int randomIndex = Random.Range(0, OpponentPlayer.Hand.Count);
                    CardInstance discarded = OpponentPlayer.Hand[randomIndex];
                    OpponentPlayer.Hand.RemoveAt(randomIndex);
                    OpponentPlayer.Discard.Add(discarded);
                    Debug.Log($"Fórmula Bot: {discarded.Data.cardName} descartada.");
                }
                break;

            case "Skeletal Hand":
                // Floop: descarta las 2 cartas superiores del mazo enemigo
                for (int i = 0; i < 2; i++)
                    if (OpponentPlayer.HasCards)
                    {
                        CardInstance milled = OpponentPlayer.Deck.Pop();
                        OpponentPlayer.Discard.Add(milled);
                        Debug.Log($"Skeletal Hand: {milled.Data.cardName} " +
                                  $"eliminada del mazo enemigo.");
                    }
                break;

            case "Plains Runner":
                // Floop: se mueve a cualquier carril vacío
                // La UI debe pedirle al jugador que elija el carril destino.
                // Por ahora busca el primer carril vacío automáticamente.
                for (int i = 0; i < 3; i++)
                {
                    if (ActivePlayer.CreatureLanes[i] == null && i != laneIndex)
                    {
                        ActivePlayer.CreatureLanes[laneIndex] = null;
                        ActivePlayer.CreatureLanes[i] = card;
                        card.PlaceInLane(i);
                        Debug.Log($"Plains Runner se movió al carril {i}.");
                        break;
                    }
                }
                break;

            case "Swamp Lurker":
                // Floop: inmune a hechizos enemigos hasta el próximo turno.
                // Requiere una bandera en CardInstance — se añade en el siguiente paso.
                Debug.Log("Swamp Lurker: inmune a hechizos este turno.");
                break;
        }
    }

    private void ResolveSpellEffect(CardInstance spell)
    {
        int opponentIndex = 1 - ActivePlayerIndex;

        switch(spell.Data.cardName)
        {
            case "Science Blast":
                // Inflige 2 de daño directo a cualquier criatura o edificio enemigo.
                // La UI debe pedirle al jugador que elija el objetivo.
                // Por ahora aplica al primer objetivo disponible.
                for (int i = 0; i < 3; i++)
                {
                    CardInstance target = OpponentPlayer.CreatureLanes[i];
                    if (target != null)
                    {
                        bool destroyed = target.TakeDamage(2);
                        if (destroyed)
                        {
                            OpponentPlayer.DestroyCreature(i);
                            GameEvents.OnCardDestroyed?.Invoke(opponentIndex, i);
                        }
                        break;
                    }
                }
                break;

            case "Oh My Glob!":
                // Cancela el Floop de una criatura enemiga este turno.
                // Fuerza a la criatura Flooped a volver a Ready y atacar.
                for (int i = 0; i < 3; i++)
                {
                    CardInstance target = OpponentPlayer.CreatureLanes[i];
                    if (target != null && target.CurrentState == CardState.Flooped)
                    {
                        target.ReadyUp();
                        Debug.Log($"Oh My Glob! cancela el Floop de " +
                                  $"{target.Data.cardName}.");
                        break;
                    }
                }
                break;
        }
    }


    //Habilidades pasivas se evalúan al inicio de cada turno, no al colocar la carta.
    private void EvaluateContinuousPassives()
    {
        for (int i = 0; i < 3; i++)
        {
            CardInstance creature = ActivePlayer.CreatureLanes[i];
            if (creature == null) continue;

            switch (creature.Data.cardName)
            {
                case "Candy Warrior":
                    // +1 ATK si hay un edificio en el mismo carril
                    if (ActivePlayer.BuildingLanes[i] != null)
                        creature.AddAttackBonus(1);
                    break;

                case "Dogboy":
                    // +2 ATK si el oponente tiene más HP que tú
                    if (OpponentPlayer.CurrentHP > ActivePlayer.CurrentHP)
                        creature.AddAttackBonus(2);
                    break;

                case "Candy Lab":
                    // Edificio: al inicio del turno, si el carril opuesto
                    // no tiene criatura enemiga en Ready, roba 1 carta
                    CardInstance building = ActivePlayer.BuildingLanes[i];
                    if (building != null && building.Data.cardName == "Candy Lab")
                    {
                        CardInstance enemy = OpponentPlayer.CreatureLanes[i];
                        bool laneOpen = enemy == null ||
                                        enemy.CurrentState != CardState.Ready;
                        if (laneOpen)
                        {
                            ActivePlayer.DrawCard();
                            Debug.Log("Candy Lab: robas 1 carta.");
                        }
                    }
                    break;

                case "Sugar Golem":
                    // Pasiva: reduce todo el daño recibido en 1.
                    // Esto se maneja en TakeDamage() de CardInstance.
                    // Aquí solo lo marcamos visualmente si quisiéramos. :D
                    break;
            }
        }
    }

    private IEnumerator TransitionAfterDelay(GameState nextState, float delay)
    {
        yield return new WaitForSeconds(delay);
        ChangeState(nextState);
    }

}

