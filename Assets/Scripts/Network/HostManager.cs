using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

/// <summary>
/// Handles creating a Relay allocation, setting up a Lobby, and starting the NGO host.
/// Manages up to 4 players max.
/// </summary>
public class HostManager : MonoBehaviour
{
    private const int MaxPlayers = 4;

    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Fired when host is successfully created.
    /// Returns lobbyCode and relayJoinCode.
    /// </summary>
    public static event Action<string, string> OnHostCreated;

    private string _currentLobbyId;
    private string _currentLobbyCode;
    private string _currentRelayJoinCode;

    /// <summary>
    /// Initializes the host by creating a Relay allocation and Lobby.
    /// </summary>
    public async System.Threading.Tasks.Task InitializeHostAsync()
    {
        try
        {
            if (!GameManager.IsAuthenticated())
                throw new Exception("Player is not authenticated. Ensure GameManager initialized first.");

            if (debugLogs) Debug.Log("[HostManager] Starting host initialization...");

            // Step 1: Create Relay allocation
            Allocation allocation = await CreateRelayAllocationAsync();
            _currentRelayJoinCode = await RelayService.Instance
                .GetJoinCodeAsync(allocation.AllocationId);

            if (debugLogs) Debug.Log($"[HostManager] Relay created. Join Code: {_currentRelayJoinCode}");

            // Step 2: Create Lobby with relay data
            await CreateLobbyAsync(_currentRelayJoinCode);

            if (debugLogs) Debug.Log($"[HostManager] Lobby created. Code: {_currentLobbyCode}");

            // Step 3: Setup UnityTransport with relay data
            SetupUnityTransportRelay(allocation);

            // Step 4: Start NGO host
            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
            {
                if (NetworkManager.Singleton.StartHost())
                {
                    if (debugLogs) Debug.Log("[HostManager] NGO Host started successfully");
                    OnHostCreated?.Invoke(_currentLobbyCode, _currentRelayJoinCode);
                }
                else
                {
                    throw new Exception("Failed to start NGO host");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Error initializing host: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Creates a Relay allocation for multiplayer.
    /// </summary>
    private async System.Threading.Tasks.Task<Allocation> CreateRelayAllocationAsync()
    {
        try
        {
            Allocation allocation = await RelayService.Instance
                .CreateAllocationAsync(MaxPlayers);
            return allocation;
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError($"[HostManager] Failed to create Relay allocation: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Creates a lobby with relay join code stored in data.
    /// </summary>
    private async System.Threading.Tasks.Task CreateLobbyAsync(string relayJoinCode)
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "RelayJoinCode",
                        new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode)
                    }
                }
            };

            string playerId = GameManager.GetPlayerId();

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                $"KartWars-{playerId[..6]}", // first 6 chars of player ID
                MaxPlayers,
                options
            );

            _currentLobbyId = lobby.Id;
            _currentLobbyCode = lobby.LobbyCode;
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError($"[HostManager] Failed to create lobby: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Configures UnityTransport with Relay connection data.
    /// Fixed: uses allocation.ConnectionData for host (not duplicated).
    /// </summary>
    private void SetupUnityTransportRelay(Allocation allocation)
    {
        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager");

            transport.SetRelayServerData(new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.ConnectionData, // host uses same ConnectionData for both
                true
            ));

            if (debugLogs) Debug.Log("[HostManager] UnityTransport configured with Relay data");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostManager] Failed to setup transport: {ex.Message}");
            throw;
        }
    }

    /// <summary>Returns current lobby code.</summary>
    public string GetLobbyCode() => _currentLobbyCode;

    /// <summary>Returns current relay join code.</summary>
    public string GetRelayJoinCode() => _currentRelayJoinCode;

    /// <summary>Returns current lobby ID.</summary>
    public string GetLobbyId() => _currentLobbyId;
}