using UnityEngine;
using TMPro;


public class ARCardVisual : MonoBehaviour
{
    [Header("Indicadores de estado")]
    // Cada uno es un GameObject hijo — se activa/desactiva según el estado
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject floopIndicator;
    [SerializeField] private GameObject exhaustedIndicator;

    [Header("Textos de stats (Canvas World Space)")]
    [SerializeField] private TMP_Text textATK;
    [SerializeField] private TMP_Text textHP;

    // ── Llamado por ARCardTracker cada frame y al cambiar estado ──────────
    public void UpdateVisual(CardInstance instance)
    {
        if (instance == null) return;

        // Activa solo el indicador del estado actual
        if (readyIndicator != null) readyIndicator.SetActive(
            instance.CurrentState == CardState.Ready);
        if (floopIndicator != null) floopIndicator.SetActive(
            instance.CurrentState == CardState.Flooped);
        if (exhaustedIndicator != null) exhaustedIndicator.SetActive(
            instance.CurrentState == CardState.Exhausted);

        // Textos de stats
        if (textATK != null)
            textATK.text = $"ATK {instance.EffectiveAttack}";

        if (textHP != null)
        {
            textHP.text = $"{instance.AccumulatedDamage}/{instance.EffectiveDefense}";
            float pct = instance.EffectiveDefense > 0
                ? (float)instance.AccumulatedDamage / instance.EffectiveDefense : 0f;
            textHP.color = pct < 0.4f ? Color.green
                         : pct < 0.7f ? Color.yellow
                         : Color.red;
        }
    }
}