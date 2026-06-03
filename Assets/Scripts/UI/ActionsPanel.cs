using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using System.Collections;

public class ActionsPanel : MonoBehaviour
{
    public static ActionsPanel Instance { get; private set; }

    [Header("Botones Principales")]
    [SerializeField] private Button buttonFight;
    [FormerlySerializedAs("buttonDrawSwap")]
    [SerializeField] private Button buttonFloopMain;
    [SerializeField] private TMP_Text textActionCount;

    [Header("Panel — Carta en MANO")]
    [SerializeField] private GameObject panelCardOptions;
    [SerializeField] private TMP_Text textSelectedCard;
    [SerializeField] private Button buttonPlaySpell;
    [SerializeField] private Button buttonCancel;

    [Header("Instrucciones")]
    [SerializeField] private TMP_Text textInstruction;

    private CardInstance pendingCard;
    private bool expectingLaneSelection;
    private int floopTargetLane = -1;
    private Coroutine notifRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        buttonFight.onClick.AddListener(OnFightPressed);
        buttonFloopMain.onClick.AddListener(OnFloopMainPressed);
        buttonPlaySpell.onClick.AddListener(OnPlaySpellPressed);
        buttonCancel.onClick.AddListener(OnCancelPressed);

        panelCardOptions.SetActive(false);

        TMP_Text btnText = buttonFloopMain.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = "Floop";

        SetInstruction("Selecciona un carril para colocar la carta.");

        GameEvents.OnGameStateChanged += OnGameStateChanged;
        GameEvents.OnTurnChanged += OnTurnChanged;
        GameEvents.OnGameOver += OnGameOver;
    }

    private void OnDestroy()
    {
        buttonFight.onClick.RemoveAllListeners();
        buttonFloopMain.onClick.RemoveAllListeners();
        buttonPlaySpell.onClick.RemoveAllListeners();
        buttonCancel.onClick.RemoveAllListeners();

        GameEvents.OnGameStateChanged -= OnGameStateChanged;
        GameEvents.OnTurnChanged -= OnTurnChanged;
        GameEvents.OnGameOver -= OnGameOver;
    }

    private void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.TurnStart:
                ShowNotification("¡Nuevo turno! Acciones restauradas.");
                break;
            case GameState.Fight:
                ShowNotification("⚔ ¡Fase de combate!");
                break;
            case GameState.GameOver:
                break;
        }
    }

    private void OnTurnChanged(int activePlayerIdx)
    {
        ShowNotification($"Turno del Jugador {activePlayerIdx + 1}");
    }

    private void OnGameOver(int winnerIdx)
    {
        ShowNotification($"¡Jugador {winnerIdx + 1} gana la partida!");
    }

    public void ShowNotification(string message, float duration = 3f)
    {
        if (textInstruction == null) return;
        if (notifRoutine != null) StopCoroutine(notifRoutine);
        textInstruction.text = message;
        notifRoutine = StartCoroutine(ClearNotificationAfter(duration));
    }

    private IEnumerator ClearNotificationAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        notifRoutine = null;
        SetInstruction("Selecciona una carta de tu mano o una criatura del campo.");
    }

    // ── Carta de la MANO seleccionada ─────────────────────────────────────
    public void ShowCardOptions(CardInstance card)
    {
        floopTargetLane = -1;
        buttonFloopMain.interactable = false;

        pendingCard = card;
        panelCardOptions.SetActive(true);

        textSelectedCard.text = $"{card.Data.cardName}\n" +
                                $"Costo: {card.Data.actionCost}  |  Tipo: {card.Data.cardType}\n" +
                                $"{card.Data.abilityDescription}";

        bool isSpell = card.Data.cardType == CardType.Spell;
        buttonPlaySpell.gameObject.SetActive(isSpell);

        PlayerState player = GameManager.Instance.ActivePlayer;
        bool canAfford = player.CanAfford(card.Data.actionCost);
        bool meetsLandscape = player.MeetsLandscapeRequirement(card.Data);

        if (!canAfford)
            SetInstruction($"No tienes energía suficiente. Costo: {card.Data.actionCost}, tienes: {player.ActionsRemaining}.");
        else if (!meetsLandscape)
            SetInstruction($"Necesitas paisaje '{card.Data.landscapeRequired}'.");
        else if (isSpell)
            SetInstruction($"Selecciona un carril y escanea la carta para lanzar {card.Data.cardName}.");
        else
            SetInstruction($"Selecciona un carril para colocar {card.Data.cardName}.");

        expectingLaneSelection = !isSpell;
    }

    public void HideCardOptions()
    {
        pendingCard = null;
        expectingLaneSelection = false;
        panelCardOptions.SetActive(false);
        SetInstruction("Selecciona una carta de tu mano o una criatura del campo.");
    }

    // ── Carril presionado en el campo ─────────────────────────────────────
    public void OnLaneSelected(int playerIndex, int laneIndex)
    {
        PlayerState activePlayer = GameManager.Instance.ActivePlayer;
        int activeIdx = GameManager.Instance.ActivePlayerIndex;

        // Siempre prepara el carril para AR
        if (playerIndex == activeIdx)
            ARPlacementManager.Instance.SetWaitingLane(laneIndex);

        // Si esperamos selección de carril para colocar carta
        if (expectingLaneSelection && pendingCard != null)
        {
            bool success = false;

            if (pendingCard.Data.cardType == CardType.Creature)
                success = GameManager.Instance.TryPlayCreature(pendingCard, laneIndex);
            else if (pendingCard.Data.cardType == CardType.Building)
                success = GameManager.Instance.TryPlayBuilding(pendingCard, laneIndex);

            if (success)
            {
                HideCardOptions();
                UpdateActionCount(activePlayer.ActionsRemaining);
                SetInstruction("Carta jugada.");
            }
            else if (!activePlayer.CanAfford(pendingCard.Data.actionCost))
            {
                SetInstruction($"No tienes energía. Costo: {pendingCard.Data.actionCost}, " +
                               $"tienes: {activePlayer.ActionsRemaining}.");
            }
            else if (!activePlayer.MeetsLandscapeRequirement(pendingCard.Data))
            {
                SetInstruction($"Necesitas paisaje '{pendingCard.Data.landscapeRequired}'.");
            }
            return;
        }

        // Si hay una carta pendiente (spell), no revisar Floop
        if (pendingCard != null) return;

        // Si no estamos colocando carta, verificar si hay criatura Flopeable
        if (playerIndex == activeIdx)
        {
            CardInstance creature = activePlayer.CreatureLanes[laneIndex];
            if (creature == null)
            {
                floopTargetLane = -1;
                buttonFloopMain.interactable = false;
                SetInstruction($"No hay criatura en el Carril {laneIndex + 1}.");
            }
            else if (creature.CanFloop && activePlayer.CanAfford(creature.Data.abilityActionCost))
            {
                floopTargetLane = laneIndex;
                buttonFloopMain.interactable = true;
                SetInstruction($"Floop disponible en Carril {laneIndex + 1} " +
                               $"(costo: {creature.Data.abilityActionCost}).");
            }
            else if (!creature.CanFloop)
            {
                floopTargetLane = -1;
                buttonFloopMain.interactable = false;
                SetInstruction($"{creature.Data.cardName} no puede hacer Floop.");
            }
            else
            {
                floopTargetLane = -1;
                buttonFloopMain.interactable = false;
                SetInstruction($"No tienes energía para hacer Floop. " +
                               $"Costo: {creature.Data.abilityActionCost}, tienes: {activePlayer.ActionsRemaining}.");
            }
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private void OnPlaySpellPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (pendingCard == null || pendingCard.Data.cardType != CardType.Spell) return;

        PlayerState p = GameManager.Instance.ActivePlayer;

        if (!p.CanAfford(pendingCard.Data.actionCost))
        {
            SetInstruction($"No tienes energía para lanzar este hechizo. " +
                           $"Costo: {pendingCard.Data.actionCost}, tienes: {p.ActionsRemaining}.");
            return;
        }

        if (!p.MeetsLandscapeRequirement(pendingCard.Data))
        {
            SetInstruction($"Necesitas paisaje '{pendingCard.Data.landscapeRequired}' " +
                           $"para lanzar este hechizo.");
            return;
        }

        bool success = GameManager.Instance.TryPlaySpell(pendingCard);
        if (success)
        {
            HideCardOptions();
            UpdateActionCount(p.ActionsRemaining);
            SetInstruction("¡Hechizo lanzado!");
        }
        else
        {
            SetInstruction("No puedes lanzar este hechizo ahora.");
        }
    }

    private void OnFloopMainPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (floopTargetLane < 0)
        {
            SetInstruction("Selecciona una criatura en el campo para hacer Floop.");
            return;
        }

        PlayerState p = GameManager.Instance.ActivePlayer;
        CardInstance creature = p.CreatureLanes[floopTargetLane];
        if (creature == null)
        {
            SetInstruction("Ya no hay criatura en ese carril.");
            floopTargetLane = -1;
            return;
        }

        if (!p.CanAfford(creature.Data.abilityActionCost))
        {
            SetInstruction($"No tienes energía para hacer Floop. " +
                           $"Costo: {creature.Data.abilityActionCost}, tienes: {p.ActionsRemaining}.");
            buttonFloopMain.interactable = false;
            floopTargetLane = -1;
            return;
        }

        bool success = GameManager.Instance.TryFloop(floopTargetLane);
        if (success)
        {
            buttonFloopMain.interactable = false;
            floopTargetLane = -1;
            UpdateActionCount(p.ActionsRemaining);
            SetInstruction($"¡{creature.Data.cardName} activó su Floop!");
        }
        else
        {
            SetInstruction("No se puede hacer Floop en este momento.");
        }
    }

    private void OnFightPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance.ProceedToFight();
        HideCardOptions();
    }

    private void OnCancelPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        HideCardOptions();
    }

    // ── Utilidades ────────────────────────────────────────────────────────
    public void SetInteractable(bool interactable)
    {
        buttonFight.interactable = interactable;
        if (!interactable)
        {
            buttonFloopMain.interactable = false;
            floopTargetLane = -1;
            HideCardOptions();
        }
    }

    public void UpdateActionCount(int actions)
    {
        textActionCount.text = $"Acciones: {actions}";
        buttonFight.interactable = actions >= 0;
    }

    private void SetInstruction(string message)
    {
        if (textInstruction != null)
            textInstruction.text = message;
    }
}