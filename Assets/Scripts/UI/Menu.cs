using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{

    public void StartGame()
    {
        AudioManager.Instance?.PlayButtonClick();
        SceneManager.LoadScene("AR");
    }

    public void ExitGame()
    {
        AudioManager.Instance?.PlayButtonClick();
        Application.Quit();
    }
}
