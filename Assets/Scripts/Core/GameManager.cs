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

        Players[0].DrawInitialHand();
        Players[1].DrawInitialHand();

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

    // ── TurnStart ─────────────────────────────────────────────────────────
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

        // Notifica la UI para refrescar la mano
        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, -1, card);
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
        if (card.Data.cardName == "Corn Stalker")
        {
            CardInstance drawn = ActivePlayer.DrawCard();
            if (drawn != null) GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
        }
    }

    private void ApplyBuildingPassive(CardInstance building, int laneIndex)
    {
        if (building.Data.cardName == "Swamp Hut")
            ActivePlayer.CreatureLanes[laneIndex]?.AddDefenseBonus(1);
    }

    private void ResolveFloopEffect(CardInstance card, int laneIndex)
    {
        int opp = 1 - ActivePlayerIndex;
        switch (card.Data.cardName)
        {
            case "Fórmula Bot":
                if (OpponentPlayer.Hand.Count > 0)
                {
                    int idx = Random.Range(0, OpponentPlayer.Hand.Count);
                    CardInstance d = OpponentPlayer.Hand[idx];
                    OpponentPlayer.Hand.RemoveAt(idx);
                    OpponentPlayer.Discard.Add(d);
                }
                break;
            case "Skeletal Hand":
                for (int i = 0; i < 2 && OpponentPlayer.HasCards; i++)
                    OpponentPlayer.Discard.Add(OpponentPlayer.Deck.Pop());
                break;
            case "Plains Runner":
                for (int i = 0; i < 3; i++)
                    if (ActivePlayer.CreatureLanes[i] == null && i != laneIndex)
                    {
                        ActivePlayer.CreatureLanes[laneIndex] = null;
                        ActivePlayer.CreatureLanes[i] = card;
                        card.PlaceInLane(i);
                        break;
                    }
                break;
        }
    }

    private void ResolveSpellEffect(CardInstance spell)
    {
        int opp = 1 - ActivePlayerIndex;
        switch (spell.Data.cardName)
        {
            case "Science Blast":
                for (int i = 0; i < 3; i++)
                {
                    CardInstance t = OpponentPlayer.CreatureLanes[i];
                    if (t != null)
                    {
                        if (t.TakeDamage(2)) { OpponentPlayer.DestroyCreature(i); GameEvents.OnCardDestroyed?.Invoke(opp, i); }
                        break;
                    }
                }
                break;
            case "Oh My Glob!":
                for (int i = 0; i < 3; i++)
                {
                    CardInstance t = OpponentPlayer.CreatureLanes[i];
                    if (t != null && t.CurrentState == CardState.Flooped) { t.ReadyUp(); break; }
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
                if (creature.Data.cardName == "Candy Warrior" && ActivePlayer.BuildingLanes[i] != null)
                    creature.AddAttackBonus(1);
                if (creature.Data.cardName == "Dogboy" && OpponentPlayer.CurrentHP > ActivePlayer.CurrentHP)
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