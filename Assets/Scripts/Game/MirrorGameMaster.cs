using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Server-authoritative game master. Tracks match timer, scores,
/// and game-over UI via NGO RPCs.
/// </summary>
public class MirrorGameMaster : NetworkBehaviour
{
    public static MirrorGameMaster Instance { get; private set; }

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 300f; // 5 minutes
    [SerializeField] private int scoreToWin = 10;
    [SerializeField] private bool useTimeLimit = true;
    [SerializeField] private bool useScoreLimit = true;

    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Transform scoreboardContainer;
    [SerializeField] private GameObject scoreEntryPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip countdownSound;

    // Synced game state
    private readonly NetworkVariable<float> matchTimeRemaining = new NetworkVariable<float>(
        300f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(
        GameState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Player scores tracking on server
    private readonly Dictionary<ulong, PlayerScoreData> playerScores = new Dictionary<ulong, PlayerScoreData>();

    // Events
    public event System.Action<GameState> OnGameStateChangedEvent;
    public event System.Action<ulong, int> OnPlayerScoreChanged;

    public GameState CurrentGameState => currentGameState.Value;
    public float MatchTimeRemaining => matchTimeRemaining.Value;
    public IReadOnlyDictionary<ulong, PlayerScoreData> PlayerScores => playerScores;

    public enum GameState
    {
        WaitingForPlayers,
        Countdown,
        Playing,
        GameOver
    }

    [System.Serializable]
    public struct PlayerScoreData
    {
        public string playerName;
        public int kills;
        public int deaths;
        public int score;

        public PlayerScoreData(string name)
        {
            playerName = name;
            kills = 0;
            deaths = 0;
            score = 0;
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
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentGameState.OnValueChanged += OnStateChanged;

        if (IsServer)
        {
            matchTimeRemaining.Value = matchDuration;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentGameState.OnValueChanged -= OnStateChanged;
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerUpdate();
        }

        UpdateTimerUI();
    }

    private void ServerUpdate()
    {
        switch (currentGameState.Value)
        {
            case GameState.WaitingForPlayers:
                CheckPlayersReady();
                break;

            case GameState.Playing:
                UpdateMatchTimer();
                CheckWinConditions();
                break;
        }
    }

    private void CheckPlayersReady()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.Count >= 1)
        {
            StartCountdown();
        }
    }

    public void StartCountdown()
    {
        if (!IsServer) return;
        currentGameState.Value = GameState.Countdown;
        StartCoroutine(CountdownCoroutine());
    }

    private System.Collections.IEnumerator CountdownCoroutine()
    {
        ShowCountdownClientRpc(3);
        yield return new WaitForSeconds(1f);
        ShowCountdownClientRpc(2);
        yield return new WaitForSeconds(1f);
        ShowCountdownClientRpc(1);
        yield return new WaitForSeconds(1f);
        ShowCountdownClientRpc(0); // GO!
        
        StartMatch();
    }

    [ClientRpc]
    private void ShowCountdownClientRpc(int count)
    {
        if (timerText != null)
        {
            if (count > 0)
            {
                timerText.text = count.ToString();
                timerText.fontSize = 72;
            }
            else
            {
                timerText.text = "GO!";
                timerText.fontSize = 72;
            }
        }

        if (countdownSound != null)
        {
            AudioSource.PlayClipAtPoint(countdownSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }

    private void StartMatch()
    {
        if (!IsServer) return;
        currentGameState.Value = GameState.Playing;
        matchTimeRemaining.Value = matchDuration;

        EnablePlayerControlsClientRpc();
    }

    [ClientRpc]
    private void EnablePlayerControlsClientRpc()
    {
        if (timerText != null)
        {
            timerText.fontSize = 36;
        }

        var localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer != null)
        {
            var kartController = localPlayer.GetComponent<MirrorKartController>();
            if (kartController != null)
            {
                kartController.EnableControls();
            }
        }
    }

    private void UpdateMatchTimer()
    {
        if (!useTimeLimit) return;

        matchTimeRemaining.Value -= Time.deltaTime;
        
        if (matchTimeRemaining.Value <= 0)
        {
            matchTimeRemaining.Value = 0;
            EndMatch();
        }
    }

    private void CheckWinConditions()
    {
        if (!useScoreLimit) return;

        foreach (var kvp in playerScores)
        {
            if (kvp.Value.score >= scoreToWin)
            {
                winnerClientId.Value = kvp.Key;
                EndMatch();
                return;
            }
        }
    }

    public void EndMatch()
    {
        if (!IsServer) return;
        if (currentGameState.Value == GameState.GameOver) return;

        currentGameState.Value = GameState.GameOver;

        if (winnerClientId.Value == 0 && playerScores.Count > 0)
        {
            var winner = playerScores.OrderByDescending(x => x.Value.score).First();
            winnerClientId.Value = winner.Key;
        }

        string winnerName = "Nobody";
        if (playerScores.TryGetValue(winnerClientId.Value, out PlayerScoreData winnerData))
        {
            winnerName = winnerData.playerName;
        }

        ShowGameOverClientRpc(winnerName);
    }

    [ClientRpc]
    private void ShowGameOverClientRpc(string winnerName)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (winnerText != null)
        {
            winnerText.text = $"{winnerName} Wins!";
        }

        if (gameOverSound != null)
        {
            AudioSource.PlayClipAtPoint(gameOverSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }

        var localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer != null)
        {
            var kartController = localPlayer.GetComponent<MirrorKartController>();
            if (kartController != null)
            {
                kartController.DisableControls();
            }
        }

        UpdateScoreboardUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        if (currentGameState.Value != GameState.Playing) return;

        int minutes = Mathf.FloorToInt(matchTimeRemaining.Value / 60f);
        int seconds = Mathf.FloorToInt(matchTimeRemaining.Value % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateScoreboardUI()
    {
        if (scoreboardContainer == null || scoreEntryPrefab == null) return;

        foreach (Transform child in scoreboardContainer)
        {
            Destroy(child.gameObject);
        }

        var sortedScores = playerScores.OrderByDescending(x => x.Value.score).ToList();

        foreach (var kvp in sortedScores)
        {
            GameObject entry = Instantiate(scoreEntryPrefab, scoreboardContainer);
            var texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
            
            if (texts.Length >= 4)
            {
                texts[0].text = kvp.Value.playerName;
                texts[1].text = kvp.Value.kills.ToString();
                texts[2].text = kvp.Value.deaths.ToString();
                texts[3].text = kvp.Value.score.ToString();
            }
        }
    }

    public void RegisterPlayer(ulong clientId, string playerName)
    {
        if (!IsServer) return;
        if (!playerScores.ContainsKey(clientId))
        {
            playerScores[clientId] = new PlayerScoreData(playerName);
        }
    }

    public void UnregisterPlayer(ulong clientId)
    {
        if (!IsServer) return;
        if (playerScores.ContainsKey(clientId))
        {
            playerScores.Remove(clientId);
        }
    }

    public void AddKill(ulong killerId, ulong victimId)
    {
        if (!IsServer) return;

        if (playerScores.TryGetValue(killerId, out PlayerScoreData killerData))
        {
            killerData.kills++;
            killerData.score++;
            playerScores[killerId] = killerData;
            OnPlayerScoreChanged?.Invoke(killerId, killerData.score);
        }

        if (playerScores.TryGetValue(victimId, out PlayerScoreData victimData))
        {
            victimData.deaths++;
            playerScores[victimId] = victimData;
        }
    }

    public void AddKill(uint killerId, uint victimId) => AddKill((ulong)killerId, (ulong)victimId);

    public void AddScore(ulong clientId, int points)
    {
        if (!IsServer) return;
        if (playerScores.TryGetValue(clientId, out PlayerScoreData data))
        {
            data.score += points;
            playerScores[clientId] = data;
            OnPlayerScoreChanged?.Invoke(clientId, data.score);
        }
    }

    public void AddScore(uint clientId, int points) => AddScore((ulong)clientId, points);

    private void OnStateChanged(GameState oldState, GameState newState)
    {
        OnGameStateChangedEvent?.Invoke(newState);
        Debug.Log($"[MirrorGameMaster] Game state changed: {oldState} -> {newState}");
    }

    public void RestartMatch()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.WaitingForPlayers;
        matchTimeRemaining.Value = matchDuration;
        winnerClientId.Value = 0;

        var keys = playerScores.Keys.ToList();
        foreach (var key in keys)
        {
            if (playerScores.TryGetValue(key, out PlayerScoreData data))
            {
                data.kills = 0;
                data.deaths = 0;
                data.score = 0;
                playerScores[key] = data;
            }
        }

        RespawnManager respawnManager = FindAnyObjectByType<RespawnManager>();
        if (respawnManager != null && NetworkManager.Singleton != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject != null)
                {
                    respawnManager.RespawnPlayer(client.PlayerObject.gameObject);
                }
            }
        }

        HideGameOverClientRpc();
    }

    [ClientRpc]
    private void HideGameOverClientRpc()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
}
