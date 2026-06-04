using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    [SerializeField] private CameraSelectorPanel cameraPanel;

    public void StartGame()
    {
        AudioManager.Instance?.PlayButtonClick();
        SceneManager.LoadScene("AR");
    }

    public void OpenCameraPanel()
    {
        AudioManager.Instance?.PlayButtonClick();
        cameraPanel.Open();
    }

    public void CloseCameraPanel()
    {
        cameraPanel.Close();
    }

    public void ExitGame()
    {
        AudioManager.Instance?.PlayButtonClick();
        Application.Quit();
    }
}
