using UnityEngine;
using TMPro;
using Vuforia;
using System.Collections;

public class ARCameraSwitcher : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.C;
    [SerializeField] private TMP_Text statusLabel;

    private WebCamDevice[] devices;
    private int currentIndex = -1;
    private bool isSwitching = false;

    private void Start()
    {
        RefreshDevices();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !isSwitching)
            CycleCamera();
    }

    public void RefreshDevices()
    {
        devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            SetStatus("No hay cámaras");
            return;
        }

        string saved = PlayerPrefs.GetString("SelectedCameraName", "");
        currentIndex = -1;
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].name == saved) { currentIndex = i; break; }
        }
        if (currentIndex < 0) currentIndex = 0;

        SetStatus($"Cámara: {devices[currentIndex].name}");
    }

    public void CycleCamera()
    {
#if UNITY_EDITOR
        if (isSwitching || devices == null || devices.Length == 0)
        {
            RefreshDevices();
            return;
        }

        currentIndex = (currentIndex + 1) % devices.Length;
        string camName = devices[currentIndex].name;
        PlayerPrefs.SetString("SelectedCameraName", camName);
        PlayerPrefs.Save();

        StartCoroutine(SwitchCamera(camName));
#else
        SetStatus("Cambio de cámara solo disponible en Editor");
#endif
    }

#if UNITY_EDITOR
    private IEnumerator SwitchCamera(string camName)
    {
        isSwitching = true;
        SetStatus($"Cambiando a {camName}...");

        VuforiaConfiguration.Instance.WebCam.DeviceNameSetInEditor = camName;

        if (VuforiaApplication.Instance.IsInitialized)
            VuforiaApplication.Instance.Deinit();

        yield return null;

        VuforiaApplication.Instance.Initialize();

        float timeout = 5f;
        while (!VuforiaApplication.Instance.IsInitialized && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        isSwitching = false;
        string icon = VuforiaApplication.Instance.IsInitialized ? "✓" : "✗";
        SetStatus($"{icon} {camName}");
    }
#endif

    private void SetStatus(string msg)
    {
        Debug.Log($"ARCameraSwitcher: {msg}");
        if (statusLabel != null)
            statusLabel.text = msg;
    }
}
