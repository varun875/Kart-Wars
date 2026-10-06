using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class OnlineMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;

    private NetworkManager networkManager;

    void Start()
    {
        NetworkManagerHelper.EnsureTransport();
        networkManager = NetworkManager.Singleton != null ? NetworkManager.Singleton : FindAnyObjectByType<NetworkManager>();

        if (hostButton != null) hostButton.onClick.AddListener(HostGame);
        if (joinButton != null) joinButton.onClick.AddListener(JoinGame);
    }

    public void HostGame()
    {
        var navigator = FindAnyObjectByType<PanelNavigator>();
        if (navigator != null && navigator.hostPanel != null)
        {
            navigator.ShowHost();
            return;
        }

        Debug.Log("🔹 Hosting game...");

        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;

        if (networkManager == null)
            networkManager = NetworkManagerHelper.EnsureNetworkManager();

        if (networkManager != null) networkManager.StartHost();
    }

    public void JoinGame()
    {
        var navigator = FindAnyObjectByType<PanelNavigator>();
        if (navigator != null && navigator.joinPanel != null)
        {
            navigator.ShowJoin();
            return;
        }

        Debug.Log("🔹 Joining game...");

        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;

        if (networkManager == null)
            networkManager = NetworkManagerHelper.EnsureNetworkManager();

        if (networkManager != null) networkManager.StartClient();
    }
}