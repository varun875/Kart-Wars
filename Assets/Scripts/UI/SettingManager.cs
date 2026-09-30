using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("References")]
    public Toggle showFPSToggle;
    public Toggle showLatencyToggle;
    public FPSDisplay fpsDisplay;

    void Start()
    {
        bool savedFPS = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        bool savedLatency = PlayerPrefs.GetInt("ShowLatency", 0) == 1;

        showFPSToggle.isOn = savedFPS;
        showLatencyToggle.isOn = savedLatency;

        fpsDisplay.SetFPSVisible(savedFPS);
        fpsDisplay.SetLatencyVisible(savedLatency);

        showFPSToggle.onValueChanged.AddListener(OnFPSToggled);
        showLatencyToggle.onValueChanged.AddListener(OnLatencyToggled);
    }

    void OnFPSToggled(bool isOn)
    {
        fpsDisplay.SetFPSVisible(isOn);
        PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void OnLatencyToggled(bool isOn)
    {
        fpsDisplay.SetLatencyVisible(isOn);
        PlayerPrefs.SetInt("ShowLatency", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        showFPSToggle.onValueChanged.RemoveListener(OnFPSToggled);
        showLatencyToggle.onValueChanged.RemoveListener(OnLatencyToggled);
    }
}