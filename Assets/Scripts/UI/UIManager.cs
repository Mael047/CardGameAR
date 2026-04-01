using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Paneles Principales")]
    [SerializeField] private GameInfoPanel gameInfoPanel;
    [SerializeField] private HandPanel handPanel;
    [SerializeField] private FieldPanel fieldPanel;
    [SerializeField] private ActionsPanel actionsPanel;
    [SerializeField] private GameOverPanel gameOverPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleStateChanged;
        GameEvents.OnHPChanged += HandleHPChanged;
        GameEvents.OnTurnChanged += HandleTurnChanged;
        GameEvents.OnCardDrawn += HandleCardDrawn;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnCardDestroyed += HandleCardDestroyed;
        GameEvents.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleStateChanged;
        GameEvents.OnHPChanged -= HandleHPChanged;
        GameEvents.OnTurnChanged -= HandleTurnChanged;
        GameEvents.OnCardDrawn -= HandleCardDrawn;
        GameEvents.OnCardPlayed -= HandleCardPlayed;
        GameEvents.OnCardDestroyed -= HandleCardDestroyed;
        GameEvents.OnGameOver -= HandleGameOver;
    }

    private void HandleStateChanged(GameState newState)
    {
        actionsPanel.SetInteractable(newState == GameState.Actions);
        gameOverPanel.gameObject.SetActive(newState == GameState.GameOver);
        gameInfoPanel.UpdateState(newState);

        // BUG CORREGIDO: la mano y el campo también deben refrescarse
        // cuando el estado cambia (ej: al inicio del turno después de robar)
        if (newState == GameState.Actions)
        {
            handPanel.Refresh();
            fieldPanel.Refresh();
            actionsPanel.UpdateActionCount(
                GameManager.Instance.ActivePlayer.ActionsRemaining);
        }

        bool isPlaying = newState != GameState.Setup;
        handPanel.gameObject.SetActive(isPlaying);
        fieldPanel.gameObject.SetActive(isPlaying);
        actionsPanel.gameObject.SetActive(isPlaying);
        gameInfoPanel.gameObject.SetActive(isPlaying);
    }

    private void HandleHPChanged(int playerIndex, int newHP)
    {
        gameInfoPanel.UpdateHp(playerIndex, newHP);
    }

    private void HandleTurnChanged(int activePlayerIndex)
    {
        gameInfoPanel.UpdateTurn(activePlayerIndex);
        handPanel.Refresh();
        fieldPanel.Refresh();
    }

    private void HandleCardDrawn(int playerIndex, CardInstance card)
    {
        // Solo refresca si es el jugador activo
        if (playerIndex == GameManager.Instance.ActivePlayerIndex)
            handPanel.Refresh();
    }

    private void HandleCardPlayed(int playerIndex, int laneIndex, CardInstance card)
    {
        fieldPanel.UpdateLane(playerIndex, laneIndex);
        handPanel.Refresh();
        actionsPanel.UpdateActionCount(
            GameManager.Instance.ActivePlayer.ActionsRemaining);
    }

    private void HandleCardDestroyed(int playerIndex, int laneIndex)
    {
        fieldPanel.UpdateLane(playerIndex, laneIndex);
    }

    private void HandleGameOver(int winnerIndex)
    {
        gameOverPanel.Show(winnerIndex);
    }
}