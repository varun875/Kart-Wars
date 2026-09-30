using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Server-authoritative game manager. Tracks match timer, scores via SyncDictionary,
/// and game-over UI via ClientRpc.
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
    [SyncVar(hook = nameof(OnMatchTimeChanged))]
    private float matchTimeRemaining;

    [SyncVar(hook = nameof(OnGameStateChanged))]
    private GameState currentGameState = GameState.WaitingForPlayers;

    [SyncVar]
    private uint winnerNetId;

    // Player scores dictionary - synced across network
    private readonly SyncDictionary<uint, PlayerScoreData> playerScores = new SyncDictionary<uint, PlayerScoreData>();

    // Events
    public event System.Action<GameState> OnGameStateChangedEvent;
    public event System.Action<uint, int> OnPlayerScoreChanged;

    public GameState CurrentGameState => currentGameState;
    public float MatchTimeRemaining => matchTimeRemaining;
    public IReadOnlyDictionary<uint, PlayerScoreData> PlayerScores => playerScores;

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

    public override void OnStartServer()
    {
        base.OnStartServer();
        matchTimeRemaining = matchDuration;
        playerScores.OnChange += OnPlayerScoresUpdated;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        playerScores.OnChange += OnPlayerScoresUpdated;
        
        // Hide game over panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (isServer)
        {
            ServerUpdate();
        }

        UpdateTimerUI();
    }

    [Server]
    private void ServerUpdate()
    {
        switch (currentGameState)
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

    [Server]
    private void CheckPlayersReady()
    {
        // Check if we have enough players to start
        int playerCount = NetworkServer.connections.Count;
        if (playerCount >= 1) // Adjust minimum players as needed
        {
            StartCountdown();
        }
    }

    [Server]
    public void StartCountdown()
    {
        currentGameState = GameState.Countdown;
        StartCoroutine(CountdownCoroutine());
    }

    [Server]
    private System.Collections.IEnumerator CountdownCoroutine()
    {
        RpcShowCountdown(3);
        yield return new WaitForSeconds(1f);
        RpcShowCountdown(2);
        yield return new WaitForSeconds(1f);
        RpcShowCountdown(1);
        yield return new WaitForSeconds(1f);
        RpcShowCountdown(0); // GO!
        
        StartMatch();
    }

    [ClientRpc]
    private void RpcShowCountdown(int count)
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

    [Server]
    private void StartMatch()
    {
        currentGameState = GameState.Playing;
        matchTimeRemaining = matchDuration;

        // Enable all player controls
        RpcEnablePlayerControls();
    }

    [ClientRpc]
    private void RpcEnablePlayerControls()
    {
        if (timerText != null)
        {
            timerText.fontSize = 36;
        }

        // Find local player and enable controls
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer != null)
        {
            var kartController = localPlayer.GetComponent<MirrorKartController>();
            if (kartController != null)
            {
                kartController.EnableControls();
            }
        }
    }

    [Server]
    private void UpdateMatchTimer()
    {
        if (!useTimeLimit) return;

        matchTimeRemaining -= Time.deltaTime;
        
        if (matchTimeRemaining <= 0)
        {
            matchTimeRemaining = 0;
            EndMatch();
        }
    }

    [Server]
    private void CheckWinConditions()
    {
        if (!useScoreLimit) return;

        foreach (var kvp in playerScores)
        {
            if (kvp.Value.score >= scoreToWin)
            {
                winnerNetId = kvp.Key;
                EndMatch();
                return;
            }
        }
    }

    [Server]
    public void EndMatch()
    {
        if (currentGameState == GameState.GameOver) return;

        currentGameState = GameState.GameOver;

        // Determine winner if not already set
        if (winnerNetId == 0 && playerScores.Count > 0)
        {
            var winner = playerScores.OrderByDescending(x => x.Value.score).First();
            winnerNetId = winner.Key;
        }

        // Get winner name
        string winnerName = "Nobody";
        if (playerScores.TryGetValue(winnerNetId, out PlayerScoreData winnerData))
        {
            winnerName = winnerData.playerName;
        }

        // Notify all clients
        RpcShowGameOver(winnerName);
    }

    [ClientRpc]
    private void RpcShowGameOver(string winnerName)
    {
        // Show game over UI
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

        // Disable player controls
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer != null)
        {
            var kartController = localPlayer.GetComponent<MirrorKartController>();
            if (kartController != null)
            {
                kartController.DisableControls();
            }
        }

        // Update scoreboard
        UpdateScoreboardUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        if (currentGameState != GameState.Playing) return;

        int minutes = Mathf.FloorToInt(matchTimeRemaining / 60f);
        int seconds = Mathf.FloorToInt(matchTimeRemaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateScoreboardUI()
    {
        if (scoreboardContainer == null || scoreEntryPrefab == null) return;

        // Clear existing entries
        foreach (Transform child in scoreboardContainer)
        {
            Destroy(child.gameObject);
        }

        // Sort players by score
        var sortedScores = playerScores.OrderByDescending(x => x.Value.score).ToList();

        // Create entries
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

    #region Score Management

    [Server]
    public void RegisterPlayer(NetworkIdentity playerIdentity, string playerName)
    {
        if (!playerScores.ContainsKey(playerIdentity.netId))
        {
            playerScores[playerIdentity.netId] = new PlayerScoreData(playerName);
        }
    }

    [Server]
    public void UnregisterPlayer(NetworkIdentity playerIdentity)
    {
        if (playerScores.ContainsKey(playerIdentity.netId))
        {
            playerScores.Remove(playerIdentity.netId);
        }
    }

    [Server]
    public void AddKill(uint killerNetId, uint victimNetId)
    {
        // Update killer stats
        if (playerScores.TryGetValue(killerNetId, out PlayerScoreData killerData))
        {
            killerData.kills++;
            killerData.score++;
            playerScores[killerNetId] = killerData;
        }

        // Update victim stats
        if (playerScores.TryGetValue(victimNetId, out PlayerScoreData victimData))
        {
            victimData.deaths++;
            playerScores[victimNetId] = victimData;
        }
    }

    [Server]
    public void AddScore(uint playerNetId, int points)
    {
        if (playerScores.TryGetValue(playerNetId, out PlayerScoreData data))
        {
            data.score += points;
            playerScores[playerNetId] = data;
        }
    }

    [Server]
    public void AddDeath(uint playerNetId)
    {
        if (playerScores.TryGetValue(playerNetId, out PlayerScoreData data))
        {
            data.deaths++;
            playerScores[playerNetId] = data;
        }
    }

    private void OnPlayerScoresUpdated(SyncDictionary<uint, PlayerScoreData>.Operation op, uint key, PlayerScoreData item)
    {
        OnPlayerScoreChanged?.Invoke(key, item.score);
        
        // Update UI if game is over
        if (currentGameState == GameState.GameOver)
        {
            UpdateScoreboardUI();
        }
    }

    public int GetPlayerScore(uint netId)
    {
        if (playerScores.TryGetValue(netId, out PlayerScoreData data))
        {
            return data.score;
        }
        return 0;
    }

    public PlayerScoreData? GetPlayerData(uint netId)
    {
        if (playerScores.TryGetValue(netId, out PlayerScoreData data))
        {
            return data;
        }
        return null;
    }

    #endregion

    #region Hook Callbacks

    private void OnMatchTimeChanged(float oldTime, float newTime)
    {
        // Timer UI is updated in Update()
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        OnGameStateChangedEvent?.Invoke(newState);

        Debug.Log($"[MirrorGameMaster] Game state changed: {oldState} -> {newState}");
    }

    #endregion

    #region Utility Methods

    [Server]
    public void RestartMatch()
    {
        currentGameState = GameState.WaitingForPlayers;
        matchTimeRemaining = matchDuration;
        winnerNetId = 0;

        // Reset all scores
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

        // Respawn all players
        RespawnManager respawnManager = FindAnyObjectByType<RespawnManager>();
        if (respawnManager != null)
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn.identity != null)
                {
                    respawnManager.RespawnPlayer(conn.identity.gameObject);
                }
            }
        }

        RpcHideGameOver();
    }

    [ClientRpc]
    private void RpcHideGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    #endregion
}
