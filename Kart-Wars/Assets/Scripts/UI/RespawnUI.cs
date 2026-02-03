using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Local respawn UI with countdown, watch toggle, and input-triggered respawn.
/// </summary>
public class RespawnUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject respawnPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button watchButton;
    [SerializeField] private Image fadeOverlay;

    [Header("Settings")]
    [SerializeField] private KeyCode respawnKey = KeyCode.Space;
    [SerializeField] private string countdownFormat = "Respawning in {0:0.0}s";
    [SerializeField] private string readyText = "Press SPACE to Respawn";

    [Header("Audio")]
    [SerializeField] private AudioClip countdownTickSound;
    [SerializeField] private AudioClip respawnReadySound;

    private float respawnTimer;
    private bool isWaiting;
    private bool canRespawn;
    private int lastSecond;
    private SpectatorController spectator;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        spectator = FindAnyObjectByType<SpectatorController>();

        if (watchButton != null)
            watchButton.onClick.AddListener(ToggleSpectator);

        Hide();
    }

    private void Update()
    {
        if (!isWaiting) return;

        if (!canRespawn)
        {
            respawnTimer -= Time.deltaTime;

            // Countdown tick sound
            int currentSecond = Mathf.CeilToInt(respawnTimer);
            if (currentSecond != lastSecond && currentSecond > 0)
            {
                lastSecond = currentSecond;
                if (countdownTickSound != null)
                    audioSource.PlayOneShot(countdownTickSound);
            }

            if (countdownText != null)
                countdownText.text = string.Format(countdownFormat, respawnTimer);

            if (respawnTimer <= 0)
            {
                canRespawn = true;
                if (countdownText != null)
                    countdownText.text = "";
                if (instructionText != null)
                    instructionText.text = readyText;
                if (respawnReadySound != null)
                    audioSource.PlayOneShot(respawnReadySound);
            }
        }
        else
        {
            if (Input.GetKeyDown(respawnKey))
            {
                RequestRespawn();
            }
        }

        // Update fade effect based on timer
        if (fadeOverlay != null)
        {
            float alpha = isWaiting ? 0.5f : 0f;
            fadeOverlay.color = new Color(0, 0, 0, alpha);
        }
    }

    /// <summary>
    /// Show the respawn UI with countdown
    /// </summary>
    public void ShowRespawnUI(float countdown)
    {
        respawnTimer = countdown;
        isWaiting = true;
        canRespawn = false;
        lastSecond = Mathf.CeilToInt(countdown) + 1;

        if (respawnPanel != null)
            respawnPanel.SetActive(true);

        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "You were eliminated!";
        }

        if (watchButton != null)
            watchButton.gameObject.SetActive(true);

        // Enable spectator
        if (spectator != null)
            spectator.EnableSpectator();
    }

    /// <summary>
    /// Hide the respawn UI
    /// </summary>
    public void Hide()
    {
        isWaiting = false;
        canRespawn = false;

        if (respawnPanel != null)
            respawnPanel.SetActive(false);

        if (fadeOverlay != null)
            fadeOverlay.color = Color.clear;

        // Disable spectator
        if (spectator != null)
            spectator.DisableSpectator();
    }

    private void RequestRespawn()
    {
        Hide();

        // Request respawn from server
        RespawnManager manager = RespawnManager.Instance;
        if (manager != null)
        {
            manager.CmdRequestRespawn();
        }
    }

    private void ToggleSpectator()
    {
        if (spectator != null)
        {
            if (spectator.IsActive)
                spectator.DisableSpectator();
            else
                spectator.EnableSpectator();
        }
    }
}
