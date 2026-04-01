using UnityEngine;
using TMPro;

public class GameInfoPanel : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private TMP_Text textHP_P1;
    [SerializeField] private TMP_Text textHP_P2;

    [Header("Turno y Estado")]
    [SerializeField] private TMP_Text textTurnNumber;
    [SerializeField] private TMP_Text textActivePlayer;
    [SerializeField] private TMP_Text textGameState;

    [Header("Acciones")]
    [SerializeField] private TMP_Text textActions;

    public void UpdateHp(int playerIndex, int newHP)
    {
        TMP_Text target = playerIndex == 0 ? textHP_P1 : textHP_P2;
        target.text = $"HP: {newHP}/{PlayerState.MAX_HP}";

        if (newHP > 10) target.color = Color.green;
        else if (newHP > 5) target.color = Color.yellow;
        else target.color = Color.red;
    }

    public void UpdateTurn(int activePlayerIndex)
    {
        if (GameManager.Instance == null) return;

        if (textTurnNumber != null)
            textTurnNumber.text = $"Turno {GameManager.Instance.TurnNumber}";

        if (textActivePlayer != null)
            textActivePlayer.text = $"Turno de: " +
                $"{GameManager.Instance.Players[activePlayerIndex].PlayerName}";
    }

    public void UpdateState(GameState state)
    {
        if (GameManager.Instance == null) return;

  
        UpdateTurn(GameManager.Instance.ActivePlayerIndex);

        // También inicializa los HP al primer estado
        UpdateHp(0, GameManager.Instance.Players[0].CurrentHP);
        UpdateHp(1, GameManager.Instance.Players[1].CurrentHP);

        if (textGameState != null)
            textGameState.text = state switch
            {
                GameState.TurnStart => "Iniciando turno...",
                GameState.Actions => "Fase de Acciones",
                GameState.Fight => "Fase de Pelea",
                GameState.EndTurn => "Fin de turno...",
                GameState.GameOver => "¡Juego terminado!",
                _ => state.ToString()
            };

        UpdateActionCount(GameManager.Instance.ActivePlayer.ActionsRemaining);
    }

    public void UpdateActionCount(int actions)
    {
        if (textActions != null)
            textActions.text = $"Acciones restantes: {actions}";
    }
}