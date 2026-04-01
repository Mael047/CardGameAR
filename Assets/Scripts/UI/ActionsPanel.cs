using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionsPanel : MonoBehaviour
{
    public static ActionsPanel Instance { get; private set; }

    [Header("Botones Principales")]
    [SerializeField] private Button buttonFight;
    [SerializeField] private Button buttonDrawSwap;
    [SerializeField] private TMP_Text textActionCount;

    [Header("Panel de Opciones de la carta")]
    [SerializeField] private GameObject panelCardOptions;
    [SerializeField] private TMP_Text textSelectedCard;
    [SerializeField] private Button buttonFloop;
    [SerializeField] private Button buttonCancel;

    [Header("Instrucciones")]
    [SerializeField] private TMP_Text textInstruction;

    [Header("Referencias")]
    [SerializeField] private HandPanel handPanel;

    private CardInstance pendingCard;
    private bool expectingLaneSelection;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        buttonFight.onClick.AddListener(OnFightPressed);
        buttonDrawSwap.onClick.AddListener(OnDrawSwapPressed);
        buttonFloop.onClick.AddListener(OnFloopPressed);
        buttonCancel.onClick.AddListener(OnCancelPressed);

        panelCardOptions.SetActive(false);
        SetInstruction("Selecciona una carta de tu mano.");
    }

    public void ShowCardOptions(CardInstance card)
    {
        pendingCard = card;
        panelCardOptions.SetActive(true);

        textSelectedCard.text = $"{card.Data.cardName}\n" +
                                $"Costo: {card.Data.actionCost} | " +
                                $"Tipo: {card.Data.cardType}";

        bool cardIsOnField = card.LaneIndex >= 0;
        bool hasFloop = card.Data.abilityType == AbilityType.Floop;
        buttonFloop.gameObject.SetActive(cardIsOnField && hasFloop);

        if (card.Data.cardType == CardType.Spell)
            SetInstruction("Presiona 'Confirmar' para lanzar el hechizo.");
        else
            SetInstruction($"Selecciona un carril del campo para colocar {card.Data.cardName}.");

        expectingLaneSelection = card.Data.cardType != CardType.Spell;
    }

    public void HideCardOptions()
    {
        pendingCard = null;
        expectingLaneSelection = false;
        panelCardOptions.SetActive(false);
        SetInstruction("Selecciona una carta de tu mano.");
    }

    public void OnLaneSelected(int playerIndex, int laneIndex)
    {
        if (playerIndex != GameManager.Instance.ActivePlayerIndex) return;
        if (!expectingLaneSelection || pendingCard == null) return;

        bool success = false;

        switch (pendingCard.Data.cardType)
        {
            case CardType.Creature:
                success = GameManager.Instance.TryPlayCreature(pendingCard, laneIndex);
                break;
            case CardType.Building:
                success = GameManager.Instance.TryPlayBuilding(pendingCard, laneIndex);
                break;
        }

        if (success)
        {
            HideCardOptions();
            UpdateActionCount(GameManager.Instance.ActivePlayer.ActionsRemaining);
            SetInstruction("Carta jugada. Selecciona otra acción.");
        }
        else
        {
            SetInstruction("Jugada inválida. Intenta otro carril.");
        }
    }

    private void OnFightPressed()
    {
        GameManager.Instance.ProceedToFight();
        HideCardOptions();
    }

    private void OnDrawSwapPressed()
    {
        PlayerState player = GameManager.Instance.ActivePlayer;
        if (!player.CanAfford(1))
        {
            SetInstruction("No tienes acciones para cambiar carta.");
            return;
        }
        player.SpendActions(1);
        player.DrawCard();
        UpdateActionCount(player.ActionsRemaining);
        SetInstruction("Carta cambiada.");
    }

    private void OnFloopPressed()
    {
        if (pendingCard == null || pendingCard.LaneIndex < 0) return;

        bool success = GameManager.Instance.TryFloop(pendingCard.LaneIndex);
        if (success)
        {
            HideCardOptions();
            UpdateActionCount(GameManager.Instance.ActivePlayer.ActionsRemaining);
            SetInstruction("¡Floop activado!");
        }
        else
        {
            SetInstruction("No se puede Flopear ahora.");
        }
    }

    private void OnCancelPressed()
    {
        HideCardOptions();
        handPanel?.Deselect();
    }

    public void SetInteractable(bool interactable)
    {
        buttonFight.interactable = interactable;
        buttonDrawSwap.interactable = interactable;
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
        buttonDrawSwap.onClick.RemoveAllListeners();
        buttonFloop.onClick.RemoveAllListeners();
        buttonCancel.onClick.RemoveAllListeners();
    }
}