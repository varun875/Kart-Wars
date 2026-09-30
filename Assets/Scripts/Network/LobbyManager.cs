using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the lobby player list using NGO NetworkList.
/// Syncs player names across host and all clients automatically.
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI lobbyStatusText;

    [Header("Settings")]
    public int maxPlayers = 4;
    public bool debugLogs = true;

    public static event System.Action<int> OnPlayerCountChanged;
    public static event System.Action OnLobbyReady;

    // NGO equivalent of Mirror's SyncList
    private NetworkList<Unity.Collections.FixedString64Bytes> playerNames;

    private void Awake()
    {
        // NetworkList must be initialized in Awake
        playerNames = new NetworkList<Unity.Collections.FixedString64Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to list changes on both host and client
        playerNames.OnListChanged += OnPlayerListChanged;
        RefreshUI();

        if (debugLogs) Debug.Log($"[LobbyManager] Network spawned. IsServer: {IsServer}");
    }

    public override void OnNetworkDespawn()
    {
        playerNames.OnListChanged -= OnPlayerListChanged;
    }

    /// <summary>
    /// Adds a player to the lobby. Server only.
    /// </summary>
    public void AddPlayer(string username)
    {
        if (!IsServer) return;

        if (playerNames.Count >= maxPlayers)
        {
            if (debugLogs) Debug.LogWarning($"[LobbyManager] Lobby full! Rejected: {username}");
            return;
        }

        playerNames.Add(username);
        OnPlayerCountChanged?.Invoke(playerNames.Count);

        if (debugLogs) Debug.Log($"[LobbyManager] Player joined: {username} ({playerNames.Count}/{maxPlayers})");

        if (playerNames.Count == maxPlayers)
            OnLobbyReady?.Invoke();
    }

    /// <summary>
    /// Removes a player from the lobby. Server only.
    /// </summary>
    public void RemovePlayer(string username)
    {
        if (!IsServer) return;

        for (int i = 0; i < playerNames.Count; i++)
        {
            if (playerNames[i].ToString() == username)
            {
                playerNames.RemoveAt(i);
                OnPlayerCountChanged?.Invoke(playerNames.Count);
                if (debugLogs) Debug.Log($"[LobbyManager] Player left: {username} ({playerNames.Count}/{maxPlayers})");
                return;
            }
        }
    }

    /// <summary>
    /// Called automatically when playerNames list changes on any client.
    /// </summary>
    private void OnPlayerListChanged(NetworkListEvent<Unity.Collections.FixedString64Bytes> changeEvent)
    {
        RefreshUI();
    }

    /// <summary>
    /// Rebuilds the player list UI.
    /// </summary>
    private void RefreshUI()
    {
        if (playerListContainer == null) return;

        // Clear existing entries
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        // Rebuild from current list
        foreach (var name in playerNames)
        {
            if (playerEntryPrefab == null) break;

            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            TextMeshProUGUI label = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = name.ToString();
        }

        if (playerCountText != null)
            playerCountText.text = $"Players: {playerNames.Count}/{maxPlayers}";

        if (lobbyStatusText != null)
            lobbyStatusText.text = playerNames.Count == maxPlayers
                ? "Lobby Full! Starting soon..."
                : "Waiting for players...";
    }

    /// <summary>Returns current player count.</summary>
    public int GetPlayerCount() => playerNames.Count;

    /// <summary>Returns true if lobby is full.</summary>
    public bool IsLobbyFull() => playerNames.Count >= maxPlayers;

    /// <summary>Returns true if at least one player is connected.</summary>
    public bool IsConnected() => playerNames.Count > 0;

    /// <summary>Returns a copy of current player names.</summary>
    public List<string> GetPlayerNames()
    {
        List<string> names = new List<string>();
        foreach (var name in playerNames)
            names.Add(name.ToString());
        return names;
    }
}