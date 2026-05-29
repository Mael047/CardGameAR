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
    private bool isSetup = false;

    public void Setup(int playerIndex, int laneIndex,
                      System.Action<int, int> onSelect,
                      bool buttonEnabled = true)
    {
        PlayerIndex = playerIndex;
        LaneIndex = laneIndex;
        onLaneSelected = onSelect;
        isSetup = true;

        if (buttonSelectLane != null)
        {
            buttonSelectLane.onClick.RemoveAllListeners();

            if (buttonEnabled)
                buttonSelectLane.onClick.AddListener(
                    () => onLaneSelected(PlayerIndex, LaneIndex));

            buttonSelectLane.gameObject.SetActive(buttonEnabled);
        }
        else
        {
            Debug.LogError($"LaneUI [{name}]: buttonSelectLane no asignado.");
        }

        SetTextActive(true);
        Refresh();
    }

    private void SetTextActive(bool active)
    {
        if (textCreatureName != null) textCreatureName.gameObject.SetActive(active);
        if (textCreatureStats != null) textCreatureStats.gameObject.SetActive(active);
        if (textCreatureState != null) textCreatureState.gameObject.SetActive(active);
        if (textBuildingName != null) textBuildingName.gameObject.SetActive(active);
    }

    public void Refresh()
    {
        if (!isSetup || GameManager.Instance == null) return;
        if (PlayerIndex >= GameManager.Instance.Players.Length) return;

        PlayerState player = GameManager.Instance.Players[PlayerIndex];
        CardInstance creature = player.CreatureLanes[LaneIndex];
        CardInstance building = player.BuildingLanes[LaneIndex];

        // Fondo: sprite del paisaje del carril
        if (laneBackground != null)
        {
            Sprite landscapeSprite = player.Landscapes != null && LaneIndex < player.Landscapes.Length
                ? SetupPanel.GetLandscapeSprite(player.Landscapes[LaneIndex])
                : null;

            if (landscapeSprite != null)
            {
                laneBackground.sprite = landscapeSprite;
                laneBackground.color = Color.white;
            }
            else
            {
                laneBackground.sprite = null;
                laneBackground.color = Color.clear;
            }
        }

        if (creature != null)
        {
            if (textCreatureName != null)
                textCreatureName.text = creature.Data.cardName;

            if (textCreatureStats != null)
                textCreatureStats.text = $"ATK:{creature.EffectiveAttack}  " +
                                         $"DEF:{creature.EffectiveDefense}  " +
                                         $"DMG:{creature.AccumulatedDamage}";

            if (textCreatureState != null)
                textCreatureState.text = creature.CurrentState switch
                {
                    CardState.Ready => "Ready",
                    CardState.Flooped => "Floop",
                    CardState.Exhausted => "Exhausted",
                    _ => ""
                };
        }
        else
        {
            if (textCreatureName != null) textCreatureName.text = " vacío ";
            if (textCreatureStats != null) textCreatureStats.text = "";
            if (textCreatureState != null) textCreatureState.text = "";
        }

        if (textBuildingName != null)
            textBuildingName.text = building != null
                ? $"{building.Data.cardName}" : "";
    }

    private void OnDestroy()
    {
        buttonSelectLane?.onClick.RemoveAllListeners();
    }
}