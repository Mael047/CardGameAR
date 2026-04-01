using UnityEngine;

public class FieldPanel : MonoBehaviour
{
    [SerializeField] private LaneUI[] lanesPlayer1;
    [SerializeField] private LaneUI[] lanesPlayer2;

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleGameReady;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleGameReady;
    }
    private void HandleGameReady(GameState state)
    {
        GameEvents.OnGameStateChanged -= HandleGameReady;
        GameEvents.OnGameStateChanged += HandleStateChanged;

        SetupLanes();
    }

    private void HandleStateChanged(GameState state)
    {
        Refresh();
    }

    private void SetupLanes()
    {
        // Verifica que los arrays estén asignados antes de iterar
        if (lanesPlayer1 == null || lanesPlayer2 == null)
        {
            Debug.LogError("FieldPanel: faltan referencias a los LaneUI en el Inspector.");
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            if (lanesPlayer1[i] != null)
                lanesPlayer1[i].Setup(0, i, OnLaneSelected);
            else
                Debug.LogError($"FieldPanel: lanesPlayer1[{i}] no está asignado.");

            if (lanesPlayer2[i] != null)
                lanesPlayer2[i].Setup(1, i, OnLaneSelected);
            else
                Debug.LogError($"FieldPanel: lanesPlayer2[{i}] no está asignado.");
        }
    }

    public void Refresh()
    {
        foreach (LaneUI lane in lanesPlayer1) lane?.Refresh();
        foreach (LaneUI lane in lanesPlayer2) lane?.Refresh();
    }

    public void UpdateLane(int playerIndex, int laneIndex)
    {
        LaneUI[] lanes = playerIndex == 0 ? lanesPlayer1 : lanesPlayer2;
        lanes[laneIndex]?.Refresh();
    }

    private void OnLaneSelected(int playerIndex, int laneIndex)
    {
        ActionsPanel.Instance.OnLaneSelected(playerIndex, laneIndex);
    }
}