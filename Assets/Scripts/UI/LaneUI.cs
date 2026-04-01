using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LaneUI : MonoBehaviour
{
    [SerializeField] private TMP_Text textCreatureName;
    [SerializeField] private TMP_Text textCreatureStats;
    [SerializeField] private TMP_Text textCreatureState;
    [SerializeField] private TMP_Text textBuildingName;
    [SerializeField] private Image laneBackground;
    [SerializeField] private Button buttonSelectLane;

    public int PlayerIndex { get; private set; }
    public int LaneIndex { get; private set; }

    private System.Action<int, int> onLaneSelected;
    private bool isSetup = false;  // Bandera para saber si ya fue configurado

    public void Setup(int playerIndex, int laneIndex,
                      System.Action<int, int> onSelect)
    {
        PlayerIndex = playerIndex;
        LaneIndex = laneIndex;
        onLaneSelected = onSelect;
        isSetup = true;

        if (buttonSelectLane != null)
        {
            buttonSelectLane.onClick.RemoveAllListeners();
            buttonSelectLane.onClick.AddListener(
                () => onLaneSelected(PlayerIndex, LaneIndex));
        }
        else
        {
            Debug.LogError($"LaneUI [{name}]: buttonSelectLane no asignado en Inspector.");
        }

        Refresh();
    }

    public void Refresh()
    {
        // Si no está configurado o el GameManager no existe aún, no hace nada
        if (!isSetup || GameManager.Instance == null) return;

        // Verifica que el índice sea válido antes de acceder al array
        if (PlayerIndex >= GameManager.Instance.Players.Length)
        {
            Debug.LogError($"LaneUI: PlayerIndex {PlayerIndex} inválido.");
            return;
        }

        PlayerState player = GameManager.Instance.Players[PlayerIndex];
        CardInstance creature = player.CreatureLanes[LaneIndex];
        CardInstance building = player.BuildingLanes[LaneIndex];

        // Usa el operador ?. en cada texto por si no están asignados en el Inspector
        if (creature != null)
        {
            if (textCreatureName != null)
                textCreatureName.text = creature.Data.cardName;

            if (textCreatureStats != null)
                textCreatureStats.text = $"ATK:{creature.EffectiveAttack} " +
                                         $"DEF:{creature.EffectiveDefense} " +
                                         $"DMG:{creature.AccumulatedDamage}";

            if (textCreatureState != null)
                textCreatureState.text = creature.CurrentState switch
                {
                    CardState.Ready => "Ready",
                    CardState.Flooped => "Flooped",
                    CardState.Exhausted => "Exhausted",
                    _ => ""
                };

            if (laneBackground != null)
                laneBackground.color = creature.CurrentState switch
                {
                    CardState.Ready => new Color(0.2f, 0.8f, 0.2f, 0.3f),
                    CardState.Flooped => new Color(0.5f, 0.2f, 0.8f, 0.3f),
                    CardState.Exhausted => new Color(0.5f, 0.5f, 0.5f, 0.3f),
                    _ => Color.clear
                };
        }
        else
        {
            if (textCreatureName != null) textCreatureName.text = "— vacío —";
            if (textCreatureStats != null) textCreatureStats.text = "";
            if (textCreatureState != null) textCreatureState.text = "";
            if (laneBackground != null) laneBackground.color = Color.clear;
        }

        if (textBuildingName != null)
            textBuildingName.text = building != null ? $"🏛 {building.Data.cardName}" : "";
    }

    private void OnDestroy()
    {
        buttonSelectLane?.onClick.RemoveAllListeners();
    }
}