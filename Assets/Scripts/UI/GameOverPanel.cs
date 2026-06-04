using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverPanel : MonoBehaviour
{
    [SerializeField] private Image displayImage;
    [SerializeField] private Sprite spritePlayer1Win;
    [SerializeField] private Sprite spritePlayer2Win;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private string menuSceneName = "Menu";

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Coroutine showRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Show(int winnerIndex)
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;

        if (displayImage != null)
            displayImage.sprite = winnerIndex == 0 ? spritePlayer1Win : spritePlayer2Win;

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

        AudioManager.Instance?.PlayButtonClick();
        SceneManager.LoadScene(menuSceneName);
    }

    private void OnDestroy()
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);
    }
}
