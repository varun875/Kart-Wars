using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Manages the in-game multiplayer lifecycle:
/// 1. Spawns unique player/kart prefabs per client with ownership and non-overlapping spawn points.
/// 2. Handles late joiners.
/// 3. Locks the lobby upon match start (IsLocked = true).
/// 4. Handles client and host disconnects cleanly.
/// 5. Coordinates in-game leave/quit functionality.
/// 6. Provides room code display support.
/// </summary>
public class InGameNetworkManager : MonoBehaviour
{
    public static InGameNetworkManager Instance { get; private set; }

    [Header("Player Prefab")]
    [Tooltip("The player kart prefab with NetworkObject and NetworkTransform.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Baseline position for the starting grid if no scene spawn points are found.")]
    [SerializeField] private Vector3 defaultGridOrigin = new Vector3(609f, -89.9f, -2761f);
    [SerializeField] private Vector3 defaultGridRotation = new Vector3(0f, 90f, 0f);
    [SerializeField] private float slotForwardSpacing = 5.0f;
    [SerializeField] private float slotLateralSpacing = 3.0f;

    [Header("Scene Config")]
    [SerializeField] private string mainMenuScene = "Main Menu";
    [SerializeField] private string gameSceneName = "Game";

    [Header("Matchmaking Rules")]
    [Tooltip("If true, rejects incoming connections via Relay if the match has already started / lobby is locked.")]
    [SerializeField] private bool blockLateJoinersWhenLobbyLocked = false;

    /// <summary>
    /// Current Room/Relay code for this match.
    /// </summary>
    public string RoomCode { get; private set; } = string.Empty;

    // Track spawned karts per client ID
    private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new Dictionary<ulong, NetworkObject>();
    private readonly Dictionary<ulong, int> _clientSpawnSlots = new Dictionary<ulong, int>();
    private readonly HashSet<ulong> _spawnedClientIds = new HashSet<ulong>();

    private Vector3 _gridOriginPos;
    private Quaternion _gridOriginRot;
    private bool _hasInitializedGrid = false;
    private bool _hasLockedLobby = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeInGame()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name.Equals("Game", StringComparison.OrdinalIgnoreCase))
        {
            EnsureInstanceExists();
        }
    }

    private static void EnsureInstanceExists()
    {
        if (Instance == null)
        {
            var existing = FindAnyObjectByType<InGameNetworkManager>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject managerObj = new GameObject("[InGameNetworkManager]");
                Instance = managerObj.AddComponent<InGameNetworkManager>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterNetworkCallbacks();
    }

    private void Start()
    {
        InitializeForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.Equals(gameSceneName, StringComparison.OrdinalIgnoreCase))
        {
            InitializeForCurrentScene();
        }
    }

    private void InitializeForCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.name.Equals(gameSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CacheRoomCode();
        SetupGridOrigin();
        RegisterNetworkCallbacks();

        // If running as server/host, lock lobby and spawn all currently connected players
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ServerStartMatchRoutine());
        }
    }

    private void CacheRoomCode()
    {
        if (HostManager.Instance != null && !string.IsNullOrEmpty(HostManager.Instance.GetRelayJoinCode()))
        {
            RoomCode = HostManager.Instance.GetRelayJoinCode();
        }
        else if (!string.IsNullOrEmpty(ClientManager.LastJoinedRoomCode))
        {
            RoomCode = ClientManager.LastJoinedRoomCode;
        }
    }

    private void SetupGridOrigin()
    {
        if (_hasInitializedGrid) return;

        // 1. Check if an in-scene placeholder kart exists (e.g. "base_basic_shaded")
        GameObject sceneKart = GameObject.Find("base_basic_shaded");
        if (sceneKart != null)
        {
            _gridOriginPos = sceneKart.transform.position;
            _gridOriginRot = sceneKart.transform.rotation;
            sceneKart.SetActive(false);
            _hasInitializedGrid = true;
            return;
        }

        // 2. Check if RespawnManager has configured spawn points
        if (RespawnManager.Instance != null)
        {
            Transform respawnPt = RespawnManager.Instance.GetSpawnPoint();
            if (respawnPt != null && respawnPt != RespawnManager.Instance.transform)
            {
                _gridOriginPos = respawnPt.position;
                _gridOriginRot = respawnPt.rotation;
                _hasInitializedGrid = true;
                return;
            }
        }

        // 3. Check for scene objects tagged "Respawn"
        try
        {
            GameObject taggedRespawn = GameObject.FindWithTag("Respawn");
            if (taggedRespawn != null)
            {
                _gridOriginPos = taggedRespawn.transform.position;
                _gridOriginRot = taggedRespawn.transform.rotation;
                _hasInitializedGrid = true;
                return;
            }
        }
        catch { }

        // 4. Fallback to configured defaults
        _gridOriginPos = defaultGridOrigin;
        _gridOriginRot = Quaternion.Euler(defaultGridRotation);
        _hasInitializedGrid = true;
    }

    private void RegisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Register ConnectionApprovalCallback to enforce lobby lock against Relay joiners
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCheck;
        }
    }

    private void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        if (NetworkManager.Singleton.ConnectionApprovalCallback == ConnectionApprovalCheck)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
        }
    }

    /// <summary>
    /// Connection approval callback: rejects incoming connections if the match has started and the lobby is locked.
    /// Also disables default NGO player object creation since we spawn custom karts with ownership ourselves.
    /// </summary>
    private void ConnectionApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (blockLateJoinersWhenLobbyLocked && _hasLockedLobby)
        {
            response.Approved = false;
            response.Reason = "Match in progress. Lobby is locked.";
            Debug.Log($"[InGameNetworkManager] Connection rejected for client {request.ClientNetworkId}: Match in progress.");
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = false; // We spawn custom player kart with ownership ourselves
    }

    private IEnumerator ServerStartMatchRoutine()
    {
        // Brief frame yield to let NGO complete scene transition synchronization
        yield return null;

        // 1. Lock the lobby when Game scene loads and match starts (Requirement 4 & 8)
        LockLobby();

        // 2. Spawn all connected players (Requirement 1 & 2)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnPlayerForClient(clientId);
            }
        }
    }

    /// <summary>
    /// Locks the lobby so no new players can join mid-race.
    /// Requirement 4 & 8: Debug.Log("Lobby locked");
    /// </summary>
    public void LockLobby()
    {
        if (_hasLockedLobby) return;
        _hasLockedLobby = true;

        if (HostManager.Instance != null)
        {
            _ = HostManager.Instance.LockLobbyAsync();
        }

        // Exact Debug.Log required by Requirement 8
        Debug.Log("Lobby locked");
    }

    /// <summary>
    /// Spawns a player/kart prefab for the given client ID with ownership.
    /// Uses non-overlapping staggered spawn points.
    /// Requirement 1 & 8: Debug.Log($"Player spawned: {clientId}");
    /// </summary>
    public void SpawnPlayerForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // Immediate reservation check to prevent double-spawning across coroutine / event frames
        if (_spawnedClientIds.Contains(clientId))
        {
            return;
        }
        _spawnedClientIds.Add(clientId);

        ResolvePlayerPrefab();
        if (playerPrefab == null)
        {
            Debug.LogError("[InGameNetworkManager] Failed to spawn player: playerPrefab is null!");
            _spawnedClientIds.Remove(clientId);
            return;
        }

        // Allocate a unique spawn slot
        int slotIndex = GetOrAssignSlot(clientId);
        GetSpawnTransform(slotIndex, out Vector3 spawnPos, out Quaternion spawnRot);

        // Instantiate and spawn with ownership
        GameObject kartInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
        NetworkObject netObj = kartInstance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.SpawnWithOwnership(clientId, true);
            _spawnedPlayers[clientId] = netObj;

            // Exact Debug.Log required by Requirement 8
            Debug.Log($"Player spawned: {clientId}");
        }
        else
        {
            Debug.LogError("[InGameNetworkManager] Player prefab does not have a NetworkObject component!");
        }
    }

    private int GetOrAssignSlot(ulong clientId)
    {
        if (_clientSpawnSlots.TryGetValue(clientId, out int slot))
        {
            return slot;
        }

        int newSlot = _clientSpawnSlots.Count;
        _clientSpawnSlots[clientId] = newSlot;
        return newSlot;
    }

    /// <summary>
    /// Calculates a staggered 2-column starting grid spawn point so players don't overlap (Requirement 2).
    /// </summary>
    public void GetSpawnTransform(int slotIndex, out Vector3 position, out Quaternion rotation)
    {
        // If RespawnManager has configured spawn points, prefer those if within range
        if (RespawnManager.Instance != null)
        {
            Transform respawnPt = RespawnManager.Instance.GetSpawnPoint();
            if (respawnPt != null && respawnPt != RespawnManager.Instance.transform)
            {
                position = respawnPt.position;
                rotation = respawnPt.rotation;
                return;
            }
        }

        // Staggered starting grid
        int row = slotIndex / 2;
        float lateralSign = (slotIndex % 2 == 0) ? -1f : 1f;

        Vector3 forward = _gridOriginRot * Vector3.forward;
        Vector3 right = _gridOriginRot * Vector3.right;

        Vector3 offset = (right * (lateralSign * (slotLateralSpacing * 0.5f))) - (forward * (row * slotForwardSpacing));
        position = _gridOriginPos + offset;
        rotation = _gridOriginRot;
    }

    private void ResolvePlayerPrefab()
    {
        if (playerPrefab != null) return;

        // Try finding registered prefab from NetworkManager config
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig?.Prefabs?.Prefabs != null)
        {
            foreach (var netPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                if (netPrefab.Prefab != null && netPrefab.Prefab.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    playerPrefab = netPrefab.Prefab;
                    return;
                }
            }

            // Fallback: pick the first registered prefab with NetworkObject
            foreach (var netPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                if (netPrefab.Prefab != null && netPrefab.Prefab.GetComponent<NetworkObject>() != null)
                {
                    playerPrefab = netPrefab.Prefab;
                    return;
                }
            }
        }

        // Try Resources
        playerPrefab = Resources.Load<GameObject>("Player Kart");
    }

    /// <summary>
    /// Handles late joiners (Requirement 1).
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    /// <summary>
    /// Handles client and host disconnects cleanly (Requirement 5 & 8).
    /// - Client disconnects: server removes their player object cleanly, with no errors for the host.
    /// - Host disconnects: clients get sent back to Main Menu with a short message.
    /// Requirement 8: Debug.Log($"Player disconnected: {clientId}");
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        // Exact Debug.Log required by Requirement 8
        Debug.Log($"Player disconnected: {clientId}");

        if (NetworkManager.Singleton == null) return;

        // Server handling a client disconnect
        if (NetworkManager.Singleton.IsServer)
        {
            _spawnedClientIds.Remove(clientId);
            if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject netObj))
            {
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                _spawnedPlayers.Remove(clientId);
            }
        }
        else
        {
            // Non-server client: if host disconnected or connection lost
            if (clientId == NetworkManager.ServerClientId || !NetworkManager.Singleton.IsConnectedClient)
            {
                ClientManager.DisconnectReason = "Host disconnected from the match.";
                if (NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                SceneManager.LoadScene(mainMenuScene);
            }
        }
    }

    /// <summary>
    /// In-game Leave functionality (Requirement 6).
    /// Client: Shutdown() and returns to Main Menu.
    /// Host: deletes lobby, stops heartbeat, shuts down, and returns to Main Menu.
    /// </summary>
    public void LeaveGame()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                Debug.Log("[InGameNetworkManager] Host leaving: resetting host data and deleting lobby...");
                if (HostManager.Instance != null)
                {
                    HostManager.Instance.ResetHostData();
                }
            }
            else
            {
                Debug.Log("[InGameNetworkManager] Client leaving: shutting down NGO...");
            }

            if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        SceneManager.LoadScene(mainMenuScene);
    }
}
