using Mirror;
using UnityEngine;
using UnityEngine.UI;        // ← For InputField, Button
using TMPro;                 // ← ADD THIS for TextMeshPro!

public class OnlineMenu : MonoBehaviour
{
    [Header("UI References")]
    public InputField ipInput;      // ← Legacy InputField OR use TMP_InputField
    public TextMeshProUGUI statusText;    // ← CHANGED from Text to TextMeshProUGUI
    public TextMeshProUGUI publicIpText;  // ← CHANGED from Text to TextMeshProUGUI
    public Button hostButton;
    public Button joinButton;

    private NetworkManager networkManager;

    void Start()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();

        if (ipInput != null)
            ipInput.text = "localhost";

        if (publicIpText != null)
            StartCoroutine(GetPublicIP());
    }

    System.Collections.IEnumerator GetPublicIP()
    {
        if (publicIpText == null) yield break;

        publicIpText.text = "Getting IP...";

        var www = UnityEngine.Networking.UnityWebRequest.Get("https://api.ipify.org");
        yield return www.SendWebRequest();

        if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            publicIpText.text = $"Your Public IP: {www.downloadHandler.text}";
        }
        else
        {
            publicIpText.text = "IP: Check failed (use whatismyip.com)";
        }
    }

    public void HostGame()
    {
        Debug.Log("🔹 Hosting game...");

        if (statusText != null)
            statusText.text = "Hosting... Share your Public IP with friends!";

        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;

        networkManager.StartHost();
    }

    public void JoinGame()
    {
        string ip = ipInput.text.Trim();

        if (string.IsNullOrEmpty(ip))
        {
            if (statusText != null)
                statusText.text = "Enter host's Public IP!";
            return;
        }

        Debug.Log($"🔹 Joining: {ip}");

        if (statusText != null)
            statusText.text = $"Joining: {ip}...";

        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;

        networkManager.networkAddress = ip;
        networkManager.StartClient();
    }
}