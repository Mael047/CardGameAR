using UnityEngine;

public class FieldPanel : MonoBehaviour
{
    [SerializeField] private LaneUI[] lanesPlayer1 = new LaneUI[3];
    [SerializeField] private LaneUI[] lanesPlayer2 = new LaneUI[3];

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleGameReady;
        GameEvents.OnTurnChanged += HandleTurnChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleGameReady;
        GameEvents.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleGameReady(GameState state)
    {
        GameEvents.OnGameStateChanged -= HandleGameReady;
        GameEvents.OnGameStateChanged += HandleStateChanged;
        SetupLanes();
    }

    private void HandleStateChanged(GameState state) => Refresh();

    private void HandleTurnChanged(int activePlayerIndex) => SetupLanes();

    private void SetupLanes()
    {
        if (lanesPlayer1 == null || lanesPlayer2 == null)
        {
            Debug.LogError("FieldPanel: arrays de lanes no asignados.");
            return;
        }

        int active = GameManager.Instance.ActivePlayerIndex;
        int inactive = 1 - active;

        LaneUI[] activeLanes = active == 0 ? lanesPlayer1 : lanesPlayer2;
        LaneUI[] inactiveLanes = active == 0 ? lanesPlayer2 : lanesPlayer1;

        // Carriles del jugador activo: botón habilitado
        for (int i = 0; i < 3; i++)
            activeLanes[i]?.Setup(active, i, OnLaneSelected, buttonEnabled: true);

        // Carriles del oponente: botón deshabilitado, solo muestra info
        for (int i = 0; i < 3; i++)
            inactiveLanes[i]?.Setup(inactive, i, OnLaneSelected, buttonEnabled: false);

        Debug.Log($"FieldPanel: botones activos para Jugador {active + 1}");
    }

    public void Refresh()
    {
        foreach (LaneUI lane in lanesPlayer1) lane?.Refresh();
        foreach (LaneUI lane in lanesPlayer2) lane?.Refresh();
    }

    public void UpdateLane(int playerIndex, int laneIndex)
    {
        LaneUI[] lanes = playerIndex == 0 ? lanesPlayer1 : lanesPlayer2;
        if (laneIndex >= 0 && laneIndex < lanes.Length)
            lanes[laneIndex]?.Refresh();
    }

    private void OnLaneSelected(int playerIndex, int laneIndex)
    {
        ActionsPanel.Instance.OnLaneSelected(playerIndex, laneIndex);
    }
}