using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        DeckData d1 = deckPlayer1;
        DeckData d2 = deckPlayer2;

        if (d1 == null || d2 == null)
        {
            var decks = Resources.FindObjectsOfTypeAll<DeckData>();
            if (decks != null && decks.Length >= 2)
            {
                d1 = decks[0];
                d2 = decks[1];
                Debug.Log($"GameManager: decks cargados desde Resources ({decks.Length} encontrados)");
            }
            else
            {
                Debug.LogError($"GameManager: no hay DeckData disponibles (serializados: {deckPlayer1!=null}/{deckPlayer2!=null}, Resources: {decks?.Length})");
            }
        }

        Players = new PlayerState[2]
        {
            new PlayerState("Player 1", d1),
            new PlayerState("Player 2", d2)
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
        if (!LaneMatchesLandscape(laneIndex, card.Data))
        {
            Debug.LogWarning($"El carril {laneIndex + 1} no tiene paisaje {card.Data.landscapeRequired}.");
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
        ActionsPanel.Instance?.ShowNotification($"¡{card.Data.cardName} colocada en Carril {laneIndex + 1}!");
        return true;
    }

    public bool TryPlayBuilding(CardInstance card, int laneIndex)
    {
        if (CurrentState != GameState.Actions) return false;
        if (!ActivePlayer.Hand.Contains(card)) return false;
        if (card.Data.cardType != CardType.Building) return false;
        if (!ActivePlayer.MeetsLandscapeRequirement(card.Data)) return false;
        if (!LaneMatchesLandscape(laneIndex, card.Data)) return false;
        if (!ActivePlayer.CanAfford(card.Data.actionCost)) return false;

        ActivePlayer.SpendActions(card.Data.actionCost);
        ActivePlayer.PlaceBuilding(card, laneIndex);
        ApplyBuildingPassive(card, laneIndex);
        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        ActionsPanel.Instance?.ShowNotification($"¡{card.Data.cardName} construida en Carril {laneIndex + 1}!");
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

        GameEvents.OnCardPlayed?.Invoke(ActivePlayerIndex, laneIndex, card);
        ActionsPanel.Instance?.ShowNotification($"¡{card.Data.cardName} lanzado!");
        return true;
    }

    public bool LaneMatchesLandscape(int laneIndex, CardData cardData)
    {
        if (cardData.landscapeRequired == LandscapeType.Rainbow) return true;
        return ActivePlayer.Landscapes[laneIndex] == cardData.landscapeRequired;
    }

    public bool TryFloop(int laneIndex)
    {
        if (CurrentState != GameState.Actions) { Debug.LogWarning("No es fase de acciones."); return false; }
        if (isFirstTurn) { Debug.LogWarning("Primer turno: no se puede hacer Floop."); return false; }

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
        if (isFirstTurn)
        {
            Debug.Log("Primer turno: no hay fase de combate.");
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
                yield return StartCoroutine(ResolveCombat(attacker, defender, lane, opponentIndex));
            else
            {
                if (ARBoardManager.Instance != null)
                {
                    ARBoardManager.Instance.PlayAttackAnimation(ActivePlayerIndex, lane);
                    yield return new WaitForSeconds(ARBoardManager.Instance.GetAttackLength(ActivePlayerIndex, lane));
                }

                int dmg = attacker.EffectiveAttack;
                OpponentPlayer.TakeDamage(dmg);
                GameEvents.OnDirectDamage?.Invoke(opponentIndex, dmg);
                GameEvents.OnHPChanged?.Invoke(opponentIndex, OpponentPlayer.CurrentHP);
                ActionsPanel.Instance?.ShowNotification($"¡{attacker.Data.cardName} ataca por {dmg} de daño directo!");
            }

            attacker.MarkAsExhausted();
            yield return new WaitForSeconds(0.3f);
            if (CheckGameOver()) yield break;
        }

        ChangeState(GameState.EndTurn);
    }

    private IEnumerator ResolveCombat(CardInstance attacker, CardInstance defender,
                                      int lane, int opponentIndex)
    {
        ActionsPanel.Instance?.ShowNotification(
            $"¡{attacker.Data.cardName} ataca a {defender.Data.cardName}!");

        // 1. Ataque del atacante
        if (ARBoardManager.Instance != null)
        {
            ARBoardManager.Instance.PlayAttackAnimation(ActivePlayerIndex, lane);
            yield return new WaitForSeconds(ARBoardManager.Instance.GetAttackLength(ActivePlayerIndex, lane));
        }

        // 2. Contraataque del defensor
        if (ARBoardManager.Instance != null)
        {
            ARBoardManager.Instance.PlayAttackAnimation(opponentIndex, lane);
            yield return new WaitForSeconds(ARBoardManager.Instance.GetAttackLength(opponentIndex, lane));
        }

        // 3. Daño simultáneo
        int atkDmg = attacker.EffectiveAttack;
        int defDmg = defender.EffectiveAttack;
        bool attackerDestroyed = attacker.TakeDamage(defDmg);
        bool defenderDestroyed = defender.TakeDamage(atkDmg);

        GameEvents.OnDamageTaken?.Invoke(ActivePlayerIndex, lane, attacker);
        GameEvents.OnDamageTaken?.Invoke(opponentIndex, lane, defender);
        GameEvents.OnCreatureAttacked?.Invoke(ActivePlayerIndex, lane, atkDmg);

        // 4. Daño al defensor
        if (ARBoardManager.Instance != null)
        {
            ARBoardManager.Instance.PlayDamageAnimation(opponentIndex, lane);
            yield return new WaitForSeconds(ARBoardManager.Instance.GetDamageLength(opponentIndex, lane));
        }

        // 5. Daño reflejo al atacante
        if (ARBoardManager.Instance != null)
        {
            ARBoardManager.Instance.PlayDamageAnimation(ActivePlayerIndex, lane);
            yield return new WaitForSeconds(ARBoardManager.Instance.GetDamageLength(ActivePlayerIndex, lane));
        }

        // 6. Muerte con espera dinámica
        if (defenderDestroyed)
        {
            ActionsPanel.Instance?.ShowNotification(
                $"¡{defender.Data.cardName} destruido en combate!", 2f);
            if (ARBoardManager.Instance != null)
            {
                ARBoardManager.Instance.PlayDeathAnimation(opponentIndex, lane);
                yield return new WaitForSeconds(ARBoardManager.Instance.GetDeathLength(opponentIndex, lane));
            }
            OpponentPlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(opponentIndex, lane);
        }

        if (attackerDestroyed)
        {
            ActionsPanel.Instance?.ShowNotification(
                $"¡{attacker.Data.cardName} destruido en combate!", 2f);
            if (ARBoardManager.Instance != null)
            {
                ARBoardManager.Instance.PlayDeathAnimation(ActivePlayerIndex, lane);
                yield return new WaitForSeconds(ARBoardManager.Instance.GetDeathLength(ActivePlayerIndex, lane));
            }
            ActivePlayer.DestroyCreature(lane);
            GameEvents.OnCardDestroyed?.Invoke(ActivePlayerIndex, lane);
        }
    }

    // ── EndTurn ───────────────────────────────────────────────────────────
    private void HandleEndTurn()
    {
        isFirstTurn = false;
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
        if (ActivePlayer.BuildingLanes[laneIndex]?.Data.cardName == "Tiny Crypt")
            card.AddDefenseBonus(1);
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
        SpellVFX vfx = SpellVFX.Instance;
        ParticleSystem cardParticle = spell.Data.spellEffect;
        switch (spell.Data.cardName)
        {
            case "Science Blast":
                if (laneIndex >= 0)
                {
                    vfx?.PlayAtLane(opp, laneIndex, cardParticle, vfx.scienceBlastColor);
                    CardInstance target = OpponentPlayer.CreatureLanes[laneIndex];
                    if (target != null)
                    {
                        if (target.spellImmune) break;
                        ARBoardManager.Instance?.PlayDamageAnimation(opp, laneIndex);
                        bool destroyed = target.TakeDamage(2);
                        GameEvents.OnDamageTaken?.Invoke(opp, laneIndex, target);
                        ActionsPanel.Instance?.ShowNotification(
                            $"¡Science Blast! {target.Data.cardName} recibe 2 de daño.");
                        if (destroyed)
                        {
                            ARBoardManager.Instance?.PlayDeathAnimation(opp, laneIndex);
                            OpponentPlayer.DestroyCreature(laneIndex);
                            GameEvents.OnCardDestroyed?.Invoke(opp, laneIndex);
                        }
                        else
                        {
                            ARBoardManager.Instance?.PlayIdleAnimation(opp, laneIndex);
                        }
                    }
                    else
                    {
                        CardInstance targetBuilding = OpponentPlayer.BuildingLanes[laneIndex];
                        if (targetBuilding != null)
                        {
                            ARBoardManager.Instance?.PlayDamageAnimation(opp, laneIndex);
                            targetBuilding.TakeDamage(2);
                            GameEvents.OnDamageTaken?.Invoke(opp, laneIndex, targetBuilding);
                            ActionsPanel.Instance?.ShowNotification(
                                $"¡Science Blast! {targetBuilding.Data.cardName} recibe 2 de daño.");
                        }
                        else
                        {
                            OpponentPlayer.TakeDamage(2);
                            GameEvents.OnDirectDamage?.Invoke(opp, 2);
                            GameEvents.OnHPChanged?.Invoke(opp, OpponentPlayer.CurrentHP);
                            ActionsPanel.Instance?.ShowNotification(
                                "¡Science Blast! 2 de daño directo al oponente.");
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

                        int ownerIdx = isOpp ? opp : ActivePlayerIndex;
                        vfx?.PlayAtLane(ownerIdx, laneIndex, cardParticle, vfx.candyPushColor);

                        if (destLane >= 0)
                        {
                            owner.CreatureLanes[laneIndex] = null;
                            owner.CreatureLanes[destLane] = target;
                            target.PlaceInLane(destLane);
                            GameEvents.OnCardPlayed?.Invoke(ownerIdx, destLane, target);
                            ActionsPanel.Instance?.ShowNotification(
                                $"¡Candy Push! {target.Data.cardName} movido a Carril {destLane + 1}.");
                        }
                        else
                        {
                            ActionsPanel.Instance?.ShowNotification(
                                "Candy Push: no hay espacio adyacente para mover.");
                        }
                    }
                }
                break;

            case "Boo To You":
                if (laneIndex >= 0)
                {
                    vfx?.PlayAtLane(opp, laneIndex, cardParticle, vfx.booToYouColor);
                    CardInstance target = OpponentPlayer.CreatureLanes[laneIndex];
                    if (target != null && target.CurrentState == CardState.Flooped)
                    {
                        target.ReadyUp();
                        ActionsPanel.Instance?.ShowNotification(
                            $"¡Boo To You! Floop cancelado de {target.Data.cardName}.");
                        Debug.Log("Boo To You: canceló el Floop enemigo.");
                    }
                    else
                    {
                        ActionsPanel.Instance?.ShowNotification(
                            "Boo To You: el objetivo no está en Floop.", 2f);
                    }
                }
                break;

            case "Smell":
                for (int i = 0; i < 3; i++)
                {
                    CardInstance c = OpponentPlayer.CreatureLanes[i];
                    if (c != null) c.AddAttackBonus(-2);
                }
                vfx?.PlayAtLane(opp, 1, cardParticle, vfx.smellColor);
                ActionsPanel.Instance?.ShowNotification(
                    "¡Smell! -2 ATK a todas las criaturas enemigas este turno.");
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
            if (building != null)
            {
                if (building.Data.cardName == "Candy Lab")
                {
                    CardInstance enemy = OpponentPlayer.CreatureLanes[i];
                    if (enemy == null || enemy.CurrentState != CardState.Ready)
                    {
                        CardInstance drawn = ActivePlayer.DrawCard();
                        if (drawn != null) GameEvents.OnCardDrawn?.Invoke(ActivePlayerIndex, drawn);
                    }
                }

                if (building.Data.cardName == "Tiny Crypt" && creature != null)
                    creature.AddDefenseBonus(1);
            }
        }
    }

    private IEnumerator TransitionAfterDelay(GameState nextState, float delay)
    {
        yield return new WaitForSeconds(delay);
        ChangeState(nextState);
    }
}