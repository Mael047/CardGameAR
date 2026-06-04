using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections;
using System.Linq;
using Vuforia;

public class CameraSelectorPanel : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown cameraDropdown;
    [SerializeField] private RawImage cameraPreview;
    [SerializeField] private Button buttonVerify;
    [SerializeField] private Button buttonApply;
    [SerializeField] private Button buttonClose;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private UnityEvent onClose;

    private WebCamDevice[] webcamDevices;
    private WebCamTexture activeWebCam;
    private Coroutine previewRoutine;
    private string selectedCameraName = "";

    private void Awake()
    {
        buttonVerify.onClick.AddListener(TogglePreview);
        buttonApply.onClick.AddListener(ApplySelection);
        buttonClose.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        StopPreview();
    }

    public void Open()
    {
        panelRoot.SetActive(true);
        RefreshCameraList();
    }

    public void Close()
    {
        StopPreview();
        panelRoot.SetActive(false);
    }

    private void RefreshCameraList()
    {
        cameraDropdown.ClearOptions();
        webcamDevices = WebCamTexture.devices;

        if (webcamDevices.Length == 0)
        {
            cameraDropdown.options.Add(new TMP_Dropdown.OptionData("— No se detectaron cámaras —"));
            cameraDropdown.interactable = false;
            buttonVerify.interactable = false;
            buttonApply.interactable = false;
            SetStatus("No se encontró ninguna cámara.", Color.red);
            return;
        }

        cameraDropdown.interactable = true;
        buttonVerify.interactable = true;

        var options = webcamDevices
            .Select((d, i) => $"[{i}] {d.name}")
            .ToList();
        cameraDropdown.AddOptions(options);

        // Restaurar selección previa
        string saved = PlayerPrefs.GetString("SelectedCameraName", "");
        int savedIndex = -1;
        for (int i = 0; i < webcamDevices.Length; i++)
        {
            if (webcamDevices[i].name == saved)
            {
                savedIndex = i;
                break;
            }
        }
        cameraDropdown.value = savedIndex >= 0 ? savedIndex : 0;
        cameraDropdown.onValueChanged.RemoveAllListeners();
        cameraDropdown.onValueChanged.AddListener(_ => StopPreview());

        SetStatus($"{webcamDevices.Length} cámara(s) detectada(s). Selecciona y presiona Verificar.",
            Color.white);
    }

    private void TogglePreview()
    {
        if (activeWebCam != null && activeWebCam.isPlaying)
        {
            StopPreview();
            return;
        }

        int index = cameraDropdown.value;
        if (index < 0 || index >= webcamDevices.Length) return;

        StopPreview();

        try
        {
            string deviceName = webcamDevices[index].name;
            activeWebCam = new WebCamTexture(deviceName);
            cameraPreview.texture = activeWebCam;
            activeWebCam.Play();
            SetStatus($"Probando: {deviceName}...", Color.white);
        }
        catch (System.Exception e)
        {
            SetStatus($"Error al abrir cámara: {e.Message}", Color.red);
        }
    }

    public void StopAnyPreview()
    {
        StopPreview();
    }

    private void StopPreview()
    {
        if (activeWebCam != null)
        {
            if (activeWebCam.isPlaying)
                activeWebCam.Stop();
            DestroyImmediate(activeWebCam);
            activeWebCam = null;
        }
        cameraPreview.texture = null;
    }

    private void ApplySelection()
    {
        int index = cameraDropdown.value;
        if (index < 0 || index >= webcamDevices.Length)
        {
            SetStatus("Selecciona una cámara válida.", Color.yellow);
            return;
        }

        selectedCameraName = webcamDevices[index].name;
        PlayerPrefs.SetString("SelectedCameraName", selectedCameraName);
        PlayerPrefs.Save();

        VuforiaConfiguration.Instance.WebCam.DeviceNameSetInEditor = selectedCameraName;

        StopPreview();
        SetStatus($"Cámara guardada: {selectedCameraName}", Color.green);
    }

    private void SetStatus(string msg, Color color)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            statusText.color = color;
        }
    }
}
