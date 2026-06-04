using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TurnChangePanel : MonoBehaviour
{
    [SerializeField] private Image displayImage;
    [SerializeField] private Sprite spritePlayer1Turn;
    [SerializeField] private Sprite spritePlayer2Turn;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float displayDuration = 1.2f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Coroutine showRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        GameEvents.OnTurnChanged += HandleTurnChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleTurnChanged(int activePlayerIndex)
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        if (displayImage != null)
            displayImage.sprite = activePlayerIndex == 0 ? spritePlayer1Turn : spritePlayer2Turn;

        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        showRoutine = StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);
    }
}
