using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text textName;
    [SerializeField] private TMP_Text textCost;
    [SerializeField] private TMP_Text textATK;
    [SerializeField] private TMP_Text textDEF;
    [SerializeField] private TMP_Text textAbility;
    [SerializeField] private TMP_Text textType;
    [SerializeField] private Image cardBackground;
    [SerializeField] private Button buttonSelect;


    public CardInstance CardInstance { get; private set; }

    private System.Action<CardUI> onClickCallback;

    //Colores de tipos de cartas 
    private static readonly Color32 ColorCreature = new Color32(70, 130, 180, 255); // azul
    private static readonly Color32 ColorBuilding = new Color32(139, 90, 43, 255); // marrón
    private static readonly Color32 ColorSpell = new Color32(138, 43, 226, 255); // violeta

    public void Setup(CardInstance instance, System.Action<CardUI> onClick)
    {
        CardInstance = instance;
        onClickCallback = onClick;

        // Llena los textos con los datos de la carta
        CardData data = instance.Data;
        textName.text = data.cardName;
        textCost.text = $"Costo: {data.actionCost}";
        textAbility.text = data.abilityDescription;
        textType.text = data.cardType.ToString();

        // Las criaturas y edificios muestran ATK/DEF; los hechizos no
        bool hasCombatStats = data.cardType != CardType.Spell;
        textATK.text = hasCombatStats ? $"ATK: {data.attack}" : "";
        textDEF.text = hasCombatStats ? $"DEF: {data.defense}" : "";

        // Color según tipo
        cardBackground.color = data.cardType switch
        {
            CardType.Creature => ColorCreature,
            CardType.Building => ColorBuilding,
            CardType.Spell => ColorSpell,
            _ => Color.white
        };

        buttonSelect.onClick.AddListener(() => onClickCallback(this));

        bool canAfford = GameManager.Instance.ActivePlayer
                                    .CanAfford(data.actionCost);
        buttonSelect.interactable = canAfford;
    }

    private void OnDestroy()
    {
        buttonSelect.onClick.RemoveAllListeners();
    }

}
