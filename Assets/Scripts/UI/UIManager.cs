using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Paneles Principales")]
    [SerializeField] private GameInfoPanel gameInfoPanel;
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

        if (newState == GameState.Actions)
        {
            fieldPanel.Refresh();
            actionsPanel.UpdateActionCount(
                GameManager.Instance.ActivePlayer.ActionsRemaining);
        }

        bool isPlaying = newState != GameState.Setup;
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
        fieldPanel.Refresh();
    }

    private void HandleCardDrawn(int playerIndex, CardInstance card) { }

    private void HandleCardPlayed(int playerIndex, int laneIndex, CardInstance card)
    {
        fieldPanel.UpdateLane(playerIndex, laneIndex);
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