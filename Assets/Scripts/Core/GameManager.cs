using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private DeckData deckPlayer1;
    [SerializeField] private DeckData deckPlayer2;

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

    }

    private void Start()
    {
        InitializeGame();
    }

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
        Debug.Log($"Empieza: Jugador {ActivePlayerIndex + 1}");
        Debug.Log("Vida del jugador " + (ActivePlayerIndex + 1) + ": " + ActivePlayer.CurrentHP);

        Players[0].DrawInitialHand();
        Players[1].DrawInitialHand();

        ChangeState(GameState.TurnStart);
    }

    private void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Estado: {newState} | Turno {TurnNumber} | Jugador {ActivePlayerIndex + 1}");

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

    // ── Paso 1 y 2: TurnStart ─────────────────────────────────────────────
    private void HandleTurnStart()
    {
        ReadyAllCards();
        EvaluateContinuousPassives();

        CardInstance drawn = ActivePlayer.DrawCard();
        if (drawn != null)
            GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);

        ActivePlayer.RestoreActions();

        StartCoroutine(TransitionAfterDelay(GameState.Actions, 0.5f));
    }

    private void ReadyAllCards()
    {
        for (int i = 0; i < 3; i++)
            ActivePlayer.CreatureLanes[i]?.ReadyUp();

        Debug.Log($"Todas las cartas del Jugador {ActivePlayerIndex + 1} están listas.");
    }

    private void HandleActions()
    {
        Debug.Log($"Jugador {ActivePlayerIndex + 1}: fase de acciones. " +
                  $"Acciones: {ActivePlayer.ActionsRemaining}");
    }

    // ── Métodos públicos llamados por la UI ───────────────────────────────

    public bool TryPlayCreature(CardInstance card, int laneIndex)
    {
        if (CurrentState != GameState.Actions)
        {
            Debug.LogWarning("No es la fase de acciones.");
            return false;
        }
        if (!ActivePlayer.Hand.Contains(card))
        {
            Debug.LogWarning("La carta no está en la mano.");
            return false;
        }
        if (card.Data.cardType != CardType.Creature)
        {
            Debug.LogWarning("No es una criatura.");
            return false;
        }
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data))
        {
            Debug.LogWarning("No cumple el requisito de paisaje.");
            return false;
        }
        if (!ActivePlayer.CanAfford(card.Data.actionCost))
        {
            Debug.LogWarning("No tiene acciones suficientes.");
            return false;
        }

        CardInstance existing = ActivePlayer.CreatureLanes[laneIndex];
        if (existing != null && existing.CurrentState == CardState.Flooped)
        {
            Debug.LogWarning("No puedes reemplazar una criatura Flooped.");
            return false;
        }

        ActivePlayer.SpendActions(card.Data.actionCost);
        ActivePlayer.PlaceCreature(card, laneIndex);
        ApplyOnEnterPassives(card, laneIndex);
        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        return true;
    }

    public bool TryPlayBuilding(CardInstance card, int laneIndex)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Building) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;
        if (!ActivePlayer.CanAfford(card.Data.actionCost)) return false;

        ActivePlayer.SpendActions(card.Data.actionCost);
        ActivePlayer.PlaceBuilding(card, laneIndex);
        ApplyBuildingPassive(card, laneIndex);
        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        return true;
    }

    public bool TryPlaySpell(CardInstance card)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Spell) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;

        bool isFree = card.Data.landscapeRequired == LandscapeType.Rainbow
                   && card.Data.actionCost == 0;

        if (!isFree && !ActivePlayer.CanAfford(card.Data.actionCost)) return false;
        if (!isFree) ActivePlayer.SpendActions(card.Data.actionCost);

        ResolveSpellEffect(card);
        ActivePlayer.DiscardSpell(card);
        return true;
    }

    public bool TryFloop(int laneIndex)
    {
        if (CurrentState != GameState.Actions)
        {
            Debug.LogWarning("Solo puedes Flopear en la fase de acciones.");
            return false;
        }
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
        ResolveFloopEffect(creature, laneIndex);
        GameEvents.OnFloopActivated?.Invoke(ActivePlayerIndex, laneIndex);
        return true;
    }

    public void ProceedToFight()
    {
        if (CurrentState != GameState.Actions) return;
        ChangeState(GameState.Fight);
    }

    // ── Paso 5: Fight ─────────────────────────────────────────────────────
    private void HandleFight()
    {
        if (isFirstTurn && ActivePlayerIndex == 0)
        {
            Debug.Log("Primer turno: el jugador 1 no pelea.");
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

            if (defender != null)
            {
                ResolveCombat(attacker, defender, lane, opponentIndex);
            }
            else
            {
                OpponentPlayer.TakeDamage(attacker.EffectiveAttack);
                GameEvents.OnDirectDamage?.Invoke(opponentIndex, attacker.EffectiveAttack);
                GameEvents.OnHPChanged?.Invoke(opponentIndex, OpponentPlayer.CurrentHP);
                Debug.Log($"Carril {lane}: daño directo de {attacker.EffectiveAttack} al jugador {opponentIndex + 1}.");
            }

            attacker.MarkAsExhausted();

            yield return new WaitForSeconds(0.3f);

            if (CheckGameOver()) yield break;
        }

        ChangeState(GameState.EndTurn);
    }

    private void ResolveCombat(CardInstance attacker, CardInstance defender,
                               int lane, int opponentIndex)
    {
        int attackerDmg = attacker.EffectiveAttack;
        int defenderDmg = defender.EffectiveAttack;

        bool attackerDestroyed = attacker.TakeDamage(defenderDmg);
        bool defenderDestroyed = defender.TakeDamage(attackerDmg);

        GameEvents.OnCreatureAttacked?.Invoke(ActivePlayerIndex, lane, attackerDmg);

        if (defenderDestroyed)
        {
            OpponentPlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(opponentIndex, lane);
        }
        if (attackerDestroyed)
        {
            ActivePlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(ActivePlayerIndex, lane);
        }
    }

    // ── Paso 6: EndTurn ───────────────────────────────────────────────────
    private void HandleEndTurn()
    {
        Debug.Log($"Turno {TurnNumber} terminado.");

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
        int winner = Players[0].IsAlive ? 0 : 1;
        Debug.Log($"¡Jugador {winner + 1} gana!");
        GameEvents.OnGameOver?.Invoke(winner);
    }

    // ── Habilidades ───────────────────────────────────────────────────────
    private void ApplyOnEnterPassives(CardInstance card, int laneIndex)
    {
        switch (card.Data.cardName)
        {
            case "Corn Stalker":
                CardInstance drawn = ActivePlayer.DrawCard();
                if (drawn != null)
                    GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
                break;
        }
    }

    private void ApplyBuildingPassive(CardInstance building, int laneIndex)
    {
        switch (building.Data.cardName)
        {
            case "Swamp Hut":
                CardInstance creature = ActivePlayer.CreatureLanes[laneIndex];
                creature?.AddDefenseBonus(1);
                break;
        }
    }

    private void ResolveFloopEffect(CardInstance card, int laneIndex)
    {
        int opponentIndex = 1 - ActivePlayerIndex;

        switch (card.Data.cardName)
        {
            case "Fórmula Bot":
                if (OpponentPlayer.Hand.Count > 0)
                {
                    int randomIndex = Random.Range(0, OpponentPlayer.Hand.Count);
                    CardInstance discard = OpponentPlayer.Hand[randomIndex];
                    OpponentPlayer.Hand.RemoveAt(randomIndex);
                    OpponentPlayer.Discard.Add(discard);
                    Debug.Log($"Fórmula Bot: {discard.Data.cardName} descartada.");
                }
                break;

            case "Skeletal Hand":
                for (int i = 0; i < 2; i++)
                    if (OpponentPlayer.HasCards)
                    {
                        CardInstance milled = OpponentPlayer.Deck.Pop();
                        OpponentPlayer.Discard.Add(milled);
                        Debug.Log($"Skeletal Hand: {milled.Data.cardName} eliminada.");
                    }
                break;

            case "Plains Runner":
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
                Debug.Log("Swamp Lurker: inmune a hechizos este turno.");
                break;
        }
    }

    private void ResolveSpellEffect(CardInstance spell)
    {
        int opponentIndex = 1 - ActivePlayerIndex;

        switch (spell.Data.cardName)
        {
            case "Science Blast":
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
                for (int i = 0; i < 3; i++)
                {
                    CardInstance target = OpponentPlayer.CreatureLanes[i];
                    if (target != null && target.CurrentState == CardState.Flooped)
                    {
                        target.ReadyUp();
                        Debug.Log($"Oh My Glob! cancela el Floop de {target.Data.cardName}.");
                        break;
                    }
                }
                break;
        }
    }

    private void EvaluateContinuousPassives()
    {
        for (int i = 0; i < 3; i++)
        {
            // Evalúa criaturas
            CardInstance creature = ActivePlayer.CreatureLanes[i];
            if (creature != null)
            {
                switch (creature.Data.cardName)
                {
                    case "Candy Warrior":
                        if (ActivePlayer.BuildingLanes[i] != null)
                            creature.AddAttackBonus(1);
                        break;

                    case "Dogboy":
                        if (OpponentPlayer.CurrentHP > ActivePlayer.CurrentHP)
                            creature.AddAttackBonus(2);
                        break;
                }
            }

            // Evalúa edificios (Candy Lab vive aquí, no en criaturas)
            CardInstance building = ActivePlayer.BuildingLanes[i];
            if (building != null && building.Data.cardName == "Candy Lab")
            {
                CardInstance enemy = OpponentPlayer.CreatureLanes[i];
                bool laneOpen = enemy == null || enemy.CurrentState != CardState.Ready;
                if (laneOpen)
                {
                    CardInstance drawn = ActivePlayer.DrawCard();
                    if (drawn != null)
                        GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
                    Debug.Log("Candy Lab: robas 1 carta.");
                }
            }
        }
    }

    private IEnumerator TransitionAfterDelay(GameState nextState, float delay)
    {
        yield return new WaitForSeconds(delay);
        ChangeState(nextState);
    }
}