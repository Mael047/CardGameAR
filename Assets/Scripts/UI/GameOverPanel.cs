using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text textWinner;
    [SerializeField] private Button buttonRestart;

    private void Start()
    {
        buttonRestart.onClick.AddListener(RestartGame);
        gameObject.SetActive(false);
    }


    public void Show(int winnerIndex)
    {
        gameObject.SetActive(true);
        string winnerName = GameManager.Instance.Players[winnerIndex].PlayerName;
        textWinner.text = winnerName;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        buttonRestart.onClick.RemoveAllListeners();
    }
}
