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

    private void Awake()
    {
        // Auto-discover references if not explicitly assigned in Inspector
        if (fpsToggle == null)
        {
            var toggles = GetComponentsInChildren<Toggle>(true);
            foreach (var t in toggles)
            {
                if (t.gameObject.name.ToLowerInvariant().Contains("fps"))
                {
                    fpsToggle = t;
                    break;
                }
            }
        }

        if (pingToggle == null)
        {
            var toggles = GetComponentsInChildren<Toggle>(true);
            foreach (var t in toggles)
            {
                if (t.gameObject.name.ToLowerInvariant().Contains("ping"))
                {
                    pingToggle = t;
                    break;
                }
            }
        }

        if (fpsDisplayText == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in texts)
            {
                if (txt.gameObject.name.ToLowerInvariant().Contains("fps"))
                {
                    fpsDisplayText = txt;
                    break;
                }
            }
        }

        if (pingDisplayText == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in texts)
            {
                if (txt.gameObject.name.ToLowerInvariant().Contains("ping"))
                {
                    pingDisplayText = txt;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        if (fpsToggle != null)
        {
            fpsToggle.isOn = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
            fpsToggle.onValueChanged.AddListener(OnFPSToggle);
            OnFPSToggle(fpsToggle.isOn);
        }

        if (pingToggle != null)
        {
            pingToggle.isOn = PlayerPrefs.GetInt("ShowPing", 0) == 1;
            pingToggle.onValueChanged.AddListener(OnPingToggle);
            OnPingToggle(pingToggle.isOn);
        }
    }

    private void OnFPSToggle(bool isOn)
    {
        if (fpsDisplayText != null)
            fpsDisplayText.gameObject.SetActive(isOn);

        PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0);
    }

    private void OnPingToggle(bool isOn)
    {
        if (pingDisplayText != null)
            pingDisplayText.gameObject.SetActive(isOn);

        PlayerPrefs.SetInt("ShowPing", isOn ? 1 : 0);
    }

    private void Update()
    {
        // FPS counter
        if (fpsToggle != null && fpsToggle.isOn && fpsDisplayText != null)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            int fps = Mathf.RoundToInt(1.0f / deltaTime);
            fpsDisplayText.text = $"FPS: {fps}";
        }

        // Ping counter (NGO)
        if (pingToggle != null && pingToggle.isOn && pingDisplayText != null)
        {
            int ping = 0;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
            {
                var transport = Unity.Netcode.NetworkManager.Singleton.NetworkConfig?.NetworkTransport;
                if (transport != null)
                {
                    ping = (int)transport.GetCurrentRtt(Unity.Netcode.NetworkManager.ServerClientId);
                }
            }

            pingDisplayText.text = $"Ping: {ping}ms";
        }
    }
}