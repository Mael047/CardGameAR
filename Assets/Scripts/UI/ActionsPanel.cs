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

    [Header("Panel — Carta en MANO")]
    [SerializeField] private GameObject panelCardOptions;
    [SerializeField] private TMP_Text textSelectedCard;
    [SerializeField] private Button buttonPlaySpell;  // nuevo: lanzar hechizo
    [SerializeField] private Button buttonCancel;

    [Header("Panel — Carta en CAMPO")]
    // Panel separado para cuando el jugador selecciona una criatura ya colocada
    [SerializeField] private GameObject panelFieldCardOptions;
    [SerializeField] private TMP_Text textFieldCard;
    [SerializeField] private Button buttonFloop;
    [SerializeField] private Button buttonFieldCancel;

    [Header("Instrucciones")]
    [SerializeField] private TMP_Text textInstruction;

    [Header("Referencias")]
    [SerializeField] private HandPanel handPanel;

    // ── Estado interno ────────────────────────────────────────────────────
    private CardInstance pendingCard;        // carta de la mano esperando carril
    private CardInstance selectedFieldCard;  // criatura en el campo seleccionada
    private int selectedFieldLane = -1;
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
        buttonPlaySpell.onClick.AddListener(OnPlaySpellPressed);
        buttonCancel.onClick.AddListener(OnCancelPressed);
        buttonFloop.onClick.AddListener(OnFloopPressed);
        buttonFieldCancel.onClick.AddListener(OnFieldCancelPressed);

        panelCardOptions.SetActive(false);
        panelFieldCardOptions.SetActive(false);
        SetInstruction("Selecciona una carta de tu mano o una criatura del campo.");
    }

    // ── Carta de la MANO seleccionada ─────────────────────────────────────
    public void ShowCardOptions(CardInstance card)
    {
        HideFieldCardOptions();
        pendingCard = card;
        panelCardOptions.SetActive(true);

        textSelectedCard.text = $"{card.Data.cardName}\n" +
                                $"Costo: {card.Data.actionCost}  |  Tipo: {card.Data.cardType}\n" +
                                $"{card.Data.abilityDescription}";

        bool isSpell = card.Data.cardType == CardType.Spell;
        buttonPlaySpell.gameObject.SetActive(isSpell);

        if (isSpell)
            SetInstruction($"Presiona 'Lanzar' para usar {card.Data.cardName}.");
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

    // ── Criatura del CAMPO seleccionada ───────────────────────────────────
    public void ShowFieldCardOptions(CardInstance card, int laneIndex)
    {
        if (GameManager.Instance.CurrentState != GameState.Actions) return;

        HideCardOptions();
        handPanel?.Deselect();

        selectedFieldCard = card;
        selectedFieldLane = laneIndex;
        panelFieldCardOptions.SetActive(true);

        textFieldCard.text = $"{card.Data.cardName}\n" +
                             $"ATK:{card.EffectiveAttack}  DEF:{card.EffectiveDefense}  " +
                             $"Daño:{card.AccumulatedDamage}\n" +
                             $"Estado: {card.CurrentState}";

        bool canFloop = card.CanFloop &&
                        GameManager.Instance.ActivePlayer
                                   .CanAfford(card.Data.abilityActionCost);
        buttonFloop.interactable = canFloop;

        if (!card.Data.abilityType.Equals(AbilityType.Floop))
            SetInstruction($"{card.Data.cardName} no tiene habilidad Floop.");
        else if (canFloop)
            SetInstruction($"Floop disponible (costo: {card.Data.abilityActionCost}). " +
                           $"La criatura entrará en modo defensa.");
        else
            SetInstruction($"No puedes Flopear ahora " +
                           $"(acciones: {GameManager.Instance.ActivePlayer.ActionsRemaining}).");
    }

    public void HideFieldCardOptions()
    {
        selectedFieldCard = null;
        selectedFieldLane = -1;
        panelFieldCardOptions.SetActive(false);
    }

    // ── Carril presionado en el campo ─────────────────────────────────────
    public void OnLaneSelected(int playerIndex, int laneIndex)
    {
        if (playerIndex != GameManager.Instance.ActivePlayerIndex) return;

        CardInstance existingCreature =
            GameManager.Instance.Players[playerIndex].CreatureLanes[laneIndex];

        // Si hay criatura y NO estoy colocando una carta → abrir panel de campo
        if (existingCreature != null && pendingCard == null)
        {
            ShowFieldCardOptions(existingCreature, laneIndex);
            return;
        }

        // Si estoy colocando una carta desde la mano → colocarla
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
            SetInstruction("Carta jugada.");
        }
        else
        {
            // Mensaje de error específico según la causa
            PlayerState p = GameManager.Instance.ActivePlayer;
            if (!p.CanAfford(pendingCard.Data.actionCost))
                SetInstruction("No tienes acciones suficientes.");
            else if (!p.MeetsLandscapeRequirement(pendingCard.Data))
                SetInstruction($"Necesitas paisaje '{pendingCard.Data.landscapeRequired}' " +
                               $"x{pendingCard.Data.landscapeAmount} para jugar esta carta.");
            else
                SetInstruction("Jugada inválida.");
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────

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

    private void OnFloopPressed()
    {
        if (selectedFieldCard == null || selectedFieldLane < 0) return;

        bool success = GameManager.Instance.TryFloop(selectedFieldLane);
        if (success)
        {
            // Refresca el texto del panel con el nuevo estado
            textFieldCard.text = $"{selectedFieldCard.Data.cardName}\n" +
                                 $"ATK:{selectedFieldCard.EffectiveAttack}  " +
                                 $"DEF:{selectedFieldCard.EffectiveDefense}\n" +
                                 $"Estado: {selectedFieldCard.CurrentState}";
            buttonFloop.interactable = false;
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
        HideFieldCardOptions();
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
        CardInstance drawn = player.DrawCard();
        if (drawn != null)
            GameEvents.OnCardDrawn?.Invoke(GameManager.Instance.ActivePlayerIndex, drawn);
        UpdateActionCount(player.ActionsRemaining);
        SetInstruction("Carta cambiada.");
    }

    private void OnCancelPressed()
    {
        HideCardOptions();
        handPanel?.Deselect();
    }

    private void OnFieldCancelPressed()
    {
        HideFieldCardOptions();
    }

    // ── Utilidades ────────────────────────────────────────────────────────
    public void SetInteractable(bool interactable)
    {
        buttonFight.interactable = interactable;
        buttonDrawSwap.interactable = interactable;
        if (!interactable) { HideCardOptions(); HideFieldCardOptions(); }
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
        buttonPlaySpell.onClick.RemoveAllListeners();
        buttonCancel.onClick.RemoveAllListeners();
        buttonFloop.onClick.RemoveAllListeners();
        buttonFieldCancel.onClick.RemoveAllListeners();
    }
}