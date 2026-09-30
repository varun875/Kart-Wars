using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugToggleManager : MonoBehaviour
{
    [Header("FPS Toggle")]
    public Toggle fpsToggle;
    public TextMeshProUGUI fpsDisplayText;

    [Header("Ping Toggle")]
    public Toggle pingToggle;
    public TextMeshProUGUI pingDisplayText;

    private float deltaTime;

    void Start()
    {
        // Load saved preferences (optional)
        fpsToggle.isOn = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        pingToggle.isOn = PlayerPrefs.GetInt("ShowPing", 0) == 1;

        // Wire up listeners
        fpsToggle.onValueChanged.AddListener(OnFPSToggle);
        pingToggle.onValueChanged.AddListener(OnPingToggle);

        // Apply initial state
        OnFPSToggle(fpsToggle.isOn);
        OnPingToggle(pingToggle.isOn);
    }

    void OnFPSToggle(bool isOn)
    {
        if (fpsDisplayText != null)
            fpsDisplayText.gameObject.SetActive(isOn);

        PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0);
    }

    void OnPingToggle(bool isOn)
    {
        if (pingDisplayText != null)
            pingDisplayText.gameObject.SetActive(isOn);

        PlayerPrefs.SetInt("ShowPing", isOn ? 1 : 0);
    }

    void Update()
    {
        // FPS counter
        if (fpsToggle.isOn && fpsDisplayText != null)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            int fps = Mathf.RoundToInt(1.0f / deltaTime);
            fpsDisplayText.text = $"FPS: {fps}";
        }

        // Ping counter (Mirror networking)
        if (pingToggle.isOn && pingDisplayText != null)
        {
            // Uncomment whichever network library you use:

            // Mirror:
            // int ping = Mathf.RoundToInt((float)Mirror.NetworkTime.rtt * 1000);

            // NGO (Netcode for GameObjects):
            // int ping = (int)(Unity.Netcode.NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(0));

            pingDisplayText.text = $"Ping: 00ms"; // replace with above
        }
    }
}