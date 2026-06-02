using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

        // Todas las cartas del mazo están disponibles desde el inicio
        Players[0].MoveAllToHand();
        Players[1].MoveAllToHand();

        // Primero va la fase de Setup para colocar paisajes
        ChangeState(GameState.Setup);
    }

    // Llamado por SetupPanel cuando ambos jugadores terminaron de colocar paisajes
    public void StartGame()
    {
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

    private void HandleTurnStart()
    {
        ReadyAllCards();
        EvaluateContinuousPassives();
        ActivePlayer.RestoreActions();
        StartCoroutine(TransitionAfterDelay(GameState.Actions, 0.5f));
    }

    private void ReadyAllCards()
    {
        for (int i = 0; i < 3; i++)
            ActivePlayer.CreatureLanes[i]?.ReadyUp();
        Debug.Log($"Jugador {ActivePlayerIndex + 1}: cartas listas.");
    }

    private void HandleActions()
    {
        Debug.Log($"Jugador {ActivePlayerIndex + 1}: acciones. " +
                  $"Disponibles: {ActivePlayer.ActionsRemaining}");
    }

    // ── Métodos llamados por la UI ─────────────────────────────────────────

    public bool TryPlayCreature(CardInstance card, int laneIndex)
    {
        if (CurrentState != GameState.Actions) { Debug.LogWarning("No es fase de acciones."); return false; }
        if (!ActivePlayer.Hand.Contains(card)) { Debug.LogWarning("Carta no en mano."); return false; }
        if (card.Data.cardType != CardType.Creature) { Debug.LogWarning("No es criatura."); return false; }
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data))
        {
            Debug.LogWarning($"Necesita paisaje {card.Data.landscapeRequired} x{card.Data.landscapeAmount}.");
            return false;
        }
        if (!ActivePlayer.CanAfford(card.Data.actionCost)) { Debug.LogWarning("Sin acciones."); return false; }

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

    public bool TryPlaySpell(CardInstance card, int laneIndex = -1)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Spell) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;

        bool isFree = card.Data.landscapeRequired == LandscapeType.Rainbow
                   && card.Data.actionCost == 0;

        if (!isFree && !ActivePlayer.CanAfford(card.Data.actionCost)) return false;
        if (!isFree) ActivePlayer.SpendActions(card.Data.actionCost);

        ResolveSpellEffect(card, laneIndex);
        ActivePlayer.DiscardSpell(card);

        // Notifica la UI para refrescar la mano
        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        return true;
    }

    public bool TryFloop(int laneIndex)
    {
        if (CurrentState != GameState.Actions) { Debug.LogWarning("No es fase de acciones."); return false; }
        if (isFirstTurn && ActivePlayerIndex == 0) { Debug.LogWarning("Primer turno: no Floop."); return false; }

        CardInstance creature = ActivePlayer.CreatureLanes[laneIndex];
        if (creature == null || !creature.CanFloop) { Debug.LogWarning("No puede Flopear."); return false; }
        if (!ActivePlayer.CanAfford(creature.Data.abilityActionCost)) { Debug.LogWarning("Sin acciones para Floop."); return false; }

        ActivePlayer.SpendActions(creature.Data.abilityActionCost);
        creature.ActivateFloop();
        ResolveFloopEffect(creature, laneIndex);
        GameEvents.OnFloopActivated?.Invoke(ActivePlayerIndex, laneIndex);

        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, creature);
        return true;
    }

    public void ProceedToFight()
    {
        if (CurrentState != GameState.Actions) return;
        ChangeState(GameState.Fight);
    }

    // ── Fight ─────────────────────────────────────────────────────────────
    private void HandleFight()
    {
        if (isFirstTurn && ActivePlayerIndex == 0)
        {
            Debug.Log("Primer turno: jugador 1 no pelea.");
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
                ResolveCombat(attacker, defender, lane, opponentIndex);
            else
            {
                OpponentPlayer.TakeDamage(attacker.EffectiveAttack);
                GameEvents.OnDirectDamage?.Invoke(opponentIndex, attacker.EffectiveAttack);
                GameEvents.OnHPChanged?.Invoke(opponentIndex, OpponentPlayer.CurrentHP);
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
        bool attackerDestroyed = attacker.TakeDamage(defender.EffectiveAttack);
        bool defenderDestroyed = defender.TakeDamage(attacker.EffectiveAttack);

        GameEvents.OnDamageTaken?.Invoke(ActivePlayerIndex, lane, attacker);
        GameEvents.OnDamageTaken?.Invoke(opponentIndex, lane, defender);
        GameEvents.OnCreatureAttacked?.Invoke(ActivePlayerIndex, lane, attacker.EffectiveAttack);

        if (defenderDestroyed) { OpponentPlayer.DestroyCreature(lane); GameEvents.OnCardDestroyed?.Invoke(opponentIndex, lane); }
        if (attackerDestroyed) { ActivePlayer.DestroyCreature(lane); GameEvents.OnCardDestroyed?.Invoke(ActivePlayerIndex, lane); }
    }

    // ── EndTurn ───────────────────────────────────────────────────────────
    private void HandleEndTurn()
    {
        if (isFirstTurn && ActivePlayerIndex == 0) isFirstTurn = false;
        ActivePlayerIndex = 1 - ActivePlayerIndex;
        TurnNumber++;
        GameEvents.OnTurnChanged?.Invoke(ActivePlayerIndex);
        StartCoroutine(TransitionAfterDelay(GameState.TurnStart, 1f));
    }

    private bool CheckGameOver()
    {
        for (int i = 0; i < 2; i++)
            if (!Players[i].IsAlive) { ChangeState(GameState.GameOver); return true; }
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
        // (reservado para pasivas de entrada)
    }

    private void ApplyBuildingPassive(CardInstance building, int laneIndex)
    {
        if (building.Data.cardName == "Tiny Crypt")
            ActivePlayer.CreatureLanes[laneIndex]?.AddDefenseBonus(1);
    }

    private void ResolveFloopEffect(CardInstance card, int laneIndex)
    {
        int opp = 1 - ActivePlayerIndex;
        switch (card.Data.cardName)
        {
            case "Punchy":
                CardInstance drawn = ActivePlayer.DrawCard();
                if (drawn != null)
                    GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
                break;

            case "Skeletal Hand":
                List<CardInstance> tempDeck = new List<CardInstance>();
                CardInstance discardedSpell = null;
                while (OpponentPlayer.Deck.Count > 0)
                {
                    CardInstance c = OpponentPlayer.Deck.Pop();
                    if (discardedSpell == null && c.Data.cardType == CardType.Spell)
                        discardedSpell = c;
                    else
                        tempDeck.Add(c);
                }
                for (int j = tempDeck.Count - 1; j >= 0; j--)
                    OpponentPlayer.Deck.Push(tempDeck[j]);
                if (discardedSpell != null)
                {
                    OpponentPlayer.Discard.Add(discardedSpell);
                    Debug.Log($"Skeletal Hand: descartó {discardedSpell.Data.cardName} del mazo enemigo.");
                }
                break;

            case "Swamp Lurker":
                card.spellImmune = true;
                break;

            case "Sugar Golem":
                card.AddDefenseBonus(1);
                break;
        }
    }

    private void ResolveSpellEffect(CardInstance spell, int laneIndex = -1)
    {
        int opp = 1 - ActivePlayerIndex;
        switch (spell.Data.cardName)
        {
            case "Science Blast":
                if (laneIndex >= 0)
                {
                    CardInstance target = OpponentPlayer.CreatureLanes[laneIndex];
                    if (target != null)
                    {
                        if (target.spellImmune) break;
                        bool destroyed = target.TakeDamage(2);
                        GameEvents.OnDamageTaken?.Invoke(opp, laneIndex, target);
                        if (destroyed)
                        {
                            OpponentPlayer.DestroyCreature(laneIndex);
                            GameEvents.OnCardDestroyed?.Invoke(opp, laneIndex);
                        }
                    }
                    else
                    {
                        CardInstance targetBuilding = OpponentPlayer.BuildingLanes[laneIndex];
                        if (targetBuilding != null)
                        {
                            targetBuilding.TakeDamage(2);
                            GameEvents.OnDamageTaken?.Invoke(opp, laneIndex, targetBuilding);
                        }
                        else
                        {
                            OpponentPlayer.TakeDamage(2);
                            GameEvents.OnDirectDamage?.Invoke(opp, 2);
                            GameEvents.OnHPChanged?.Invoke(opp, OpponentPlayer.CurrentHP);
                        }
                    }
                }
                break;

            case "Candy Push":
                if (laneIndex >= 0)
                {
                    CardInstance target = ActivePlayer.CreatureLanes[laneIndex];
                    bool isOpp = false;
                    if (target == null)
                    {
                        target = OpponentPlayer.CreatureLanes[laneIndex];
                        isOpp = true;
                    }
                    if (target != null && !target.spellImmune)
                    {
                        PlayerState owner = isOpp ? OpponentPlayer : ActivePlayer;
                        int left = laneIndex - 1;
                        int right = laneIndex + 1;
                        int destLane = -1;
                        if (left >= 0 && owner.CreatureLanes[left] == null)
                            destLane = left;
                        else if (right < 3 && owner.CreatureLanes[right] == null)
                            destLane = right;

                        if (destLane >= 0)
                        {
                            int ownerIdx = isOpp ? opp : ActivePlayerIndex;
                            owner.CreatureLanes[laneIndex] = null;
                            owner.CreatureLanes[destLane] = target;
                            target.PlaceInLane(destLane);
                            GameEvents.OnCardPlayed?.Invoke(ownerIdx, destLane, target);
                        }
                    }
                }
                break;

            case "Boo To You":
                if (laneIndex >= 0)
                {
                    CardInstance target = OpponentPlayer.CreatureLanes[laneIndex];
                    if (target != null && target.CurrentState == CardState.Flooped)
                    {
                        target.ReadyUp();
                        Debug.Log("Boo To You: canceló el Floop enemigo.");
                    }
                }
                break;

            case "Smell":
                for (int i = 0; i < 3; i++)
                {
                    CardInstance c = OpponentPlayer.CreatureLanes[i];
                    if (c != null) c.AddAttackBonus(-2);
                }
                break;
        }
    }

    private void EvaluateContinuousPassives()
    {
        for (int i = 0; i < 3; i++)
        {
            CardInstance creature = ActivePlayer.CreatureLanes[i];
            if (creature != null)
            {
                if (creature.Data.cardName == "Cat Warrior" && ActivePlayer.BuildingLanes[i] != null)
                    creature.AddAttackBonus(1);
                if (creature.Data.cardName == "Ghosty" && OpponentPlayer.CurrentHP > ActivePlayer.CurrentHP)
                    creature.AddAttackBonus(2);
            }

            CardInstance building = ActivePlayer.BuildingLanes[i];
            if (building != null && building.Data.cardName == "Candy Lab")
            {
                CardInstance enemy = OpponentPlayer.CreatureLanes[i];
                if (enemy == null || enemy.CurrentState != CardState.Ready)
                {
                    CardInstance drawn = ActivePlayer.DrawCard();
                    if (drawn != null) GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
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