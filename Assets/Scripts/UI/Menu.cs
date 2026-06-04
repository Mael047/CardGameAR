using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

public class Menu : MonoBehaviour
{
    [SerializeField] private CameraSelectorPanel cameraPanel;

    public void StartGame()
    {
        AudioManager.Instance?.PlayButtonClick();
        cameraPanel?.StopAnyPreview();

        string savedCam = PlayerPrefs.GetString("SelectedCameraName", "");
        if (!string.IsNullOrEmpty(savedCam))
        {
            bool exists = false;
            foreach (var d in WebCamTexture.devices)
                if (d.name == savedCam) { exists = true; break; }
            if (exists)
            {
                VuforiaConfiguration.Instance.WebCam.DeviceNameSetInEditor = savedCam;
                Debug.Log($"Menu: cámara '{savedCam}' configurada en Vuforia antes de entrar a AR");
            }
        }

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
