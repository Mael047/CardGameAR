using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

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

    // ── Estado interno ────────────────────────────────────────────────────
    private CardInstance pendingCard;
    private bool expectingLaneSelection;
    private int floopTargetLane = -1;

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

        if (isSpell)
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
            return;
        }

        // Si hay una carta pendiente (spell), no revisar Floop
        if (pendingCard != null) return;

        // Si no estamos colocando carta, verificar si hay criatura Flopeable
        if (playerIndex == activeIdx)
        {
            CardInstance creature = activePlayer.CreatureLanes[laneIndex];
            if (creature != null && creature.CanFloop &&
                activePlayer.CanAfford(creature.Data.abilityActionCost))
            {
                floopTargetLane = laneIndex;
                buttonFloopMain.interactable = true;
                SetInstruction($"Floop disponible en Carril {laneIndex + 1} " +
                               $"(costo: {creature.Data.abilityActionCost}).");
            }
            else
            {
                floopTargetLane = -1;
                buttonFloopMain.interactable = false;
                SetInstruction($"Escanea tu carta para el Carril {laneIndex + 1}");
            }
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private void OnPlaySpellPressed()
    {
        if (pendingCard == null || pendingCard.Data.cardType != CardType.Spell) return;

        bool success = GameManager.Instance.TryPlaySpell(pendingCard);
        if (success)
        {
            HideCardOptions();
            UpdateActionCount(GameManager.Instance.ActivePlayer.ActionsRemaining);
            SetInstruction("¡Hechizo lanzado!");
        }
        else
        {
            PlayerState p = GameManager.Instance.ActivePlayer;
            if (!p.MeetsLandscapeRequirement(pendingCard.Data))
                SetInstruction($"Necesitas paisaje '{pendingCard.Data.landscapeRequired}' " +
                               $"para lanzar este hechizo.");
            else
                SetInstruction("No puedes lanzar este hechizo ahora.");
        }
    }

    private void OnFloopMainPressed()
    {
        if (floopTargetLane < 0) return;

        bool success = GameManager.Instance.TryFloop(floopTargetLane);
        if (success)
        {
            buttonFloopMain.interactable = false;
            floopTargetLane = -1;
            UpdateActionCount(GameManager.Instance.ActivePlayer.ActionsRemaining);
            SetInstruction("¡Floop activado! La criatura está en modo defensa.");
        }
        else
        {
            SetInstruction("No se puede Flopear ahora.");
        }
    }

    private void OnFightPressed()
    {
        GameManager.Instance.ProceedToFight();
        HideCardOptions();
    }

    private void OnCancelPressed()
    {
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

    private void OnDestroy()
    {
        buttonFight.onClick.RemoveAllListeners();
        buttonFloopMain.onClick.RemoveAllListeners();
        buttonPlaySpell.onClick.RemoveAllListeners();
        buttonCancel.onClick.RemoveAllListeners();
    }
}