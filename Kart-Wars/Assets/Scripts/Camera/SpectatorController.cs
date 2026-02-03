using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Spectator camera that follows and cycles through alive players.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera spectatorCamera;
    [SerializeField] private float followDistance = 8f;
    [SerializeField] private float followHeight = 4f;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float lookAheadDistance = 2f;

    [Header("Controls")]
    [SerializeField] private KeyCode nextPlayerKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode prevPlayerKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode freeCamKey = KeyCode.F;

    [Header("UI")]
    [SerializeField] private TMPro.TextMeshProUGUI spectatingText;

    private List<PlayerHealth> alivePlayers = new List<PlayerHealth>();
    private int currentIndex = 0;
    private bool isActive = false;
    private bool isFreeCam = false;
    private Vector3 freeCamPosition;
    private Quaternion freeCamRotation;

    public bool IsActive => isActive;

    private void Awake()
    {
        if (spectatorCamera == null)
        {
            spectatorCamera = GetComponentInChildren<Camera>();
        }

        DisableSpectator();
    }

    private void Update()
    {
        if (!isActive) return;

        HandleInput();

        if (isFreeCam)
        {
            UpdateFreeCam();
        }
        else
        {
            FollowCurrentPlayer();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(nextPlayerKey))
        {
            CycleToNextPlayer();
        }
        else if (Input.GetKeyDown(prevPlayerKey))
        {
            CycleToPreviousPlayer();
        }
        else if (Input.GetKeyDown(freeCamKey))
        {
            ToggleFreeCam();
        }
    }

    public void EnableSpectator()
    {
        isActive = true;

        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(true);
        }

        RefreshPlayerList();

        if (alivePlayers.Count > 0)
        {
            currentIndex = 0;
            UpdateSpectatingUI();
        }
    }

    public void DisableSpectator()
    {
        isActive = false;
        isFreeCam = false;

        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(false);
        }

        if (spectatingText != null)
        {
            spectatingText.gameObject.SetActive(false);
        }
    }

    private void RefreshPlayerList()
    {
        alivePlayers.Clear();
        
        var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (!player.IsDead && player.GetComponent<NetworkIdentity>() != NetworkClient.localPlayer)
            {
                alivePlayers.Add(player);
            }
        }

        // Validate current index
        if (alivePlayers.Count > 0)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, alivePlayers.Count - 1);
        }
    }

    private void CycleToNextPlayer()
    {
        RefreshPlayerList();
        if (alivePlayers.Count == 0) return;

        currentIndex = (currentIndex + 1) % alivePlayers.Count;
        isFreeCam = false;
        UpdateSpectatingUI();
    }

    private void CycleToPreviousPlayer()
    {
        RefreshPlayerList();
        if (alivePlayers.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = alivePlayers.Count - 1;
        isFreeCam = false;
        UpdateSpectatingUI();
    }

    private void ToggleFreeCam()
    {
        isFreeCam = !isFreeCam;

        if (isFreeCam && spectatorCamera != null)
        {
            freeCamPosition = spectatorCamera.transform.position;
            freeCamRotation = spectatorCamera.transform.rotation;
        }

        UpdateSpectatingUI();
    }

    private void FollowCurrentPlayer()
    {
        RefreshPlayerList();
        if (alivePlayers.Count == 0 || spectatorCamera == null) return;

        if (currentIndex >= alivePlayers.Count)
        {
            currentIndex = 0;
            UpdateSpectatingUI();
        }

        PlayerHealth target = alivePlayers[currentIndex];
        if (target == null || target.IsDead)
        {
            RefreshPlayerList();
            return;
        }

        Transform targetTransform = target.transform;

        // Calculate desired camera position
        Vector3 targetPos = targetTransform.position 
            - targetTransform.forward * followDistance 
            + Vector3.up * followHeight;

        Vector3 lookAtPos = targetTransform.position 
            + targetTransform.forward * lookAheadDistance 
            + Vector3.up * 1.5f;

        // Smooth follow
        spectatorCamera.transform.position = Vector3.Lerp(
            spectatorCamera.transform.position, targetPos, smoothSpeed * Time.deltaTime);

        Quaternion targetRot = Quaternion.LookRotation(lookAtPos - spectatorCamera.transform.position);
        spectatorCamera.transform.rotation = Quaternion.Slerp(
            spectatorCamera.transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
    }

    private void UpdateFreeCam()
    {
        if (spectatorCamera == null) return;

        // WASD movement
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float ud = 0f;
        if (Input.GetKey(KeyCode.Q)) ud = -1f;
        if (Input.GetKey(KeyCode.E)) ud = 1f;

        Vector3 move = new Vector3(h, ud, v) * 10f * Time.deltaTime;
        freeCamPosition += spectatorCamera.transform.TransformDirection(move);

        // Mouse look
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * 3f;
            float mouseY = Input.GetAxis("Mouse Y") * 3f;
            freeCamRotation *= Quaternion.Euler(-mouseY, mouseX, 0f);
        }

        spectatorCamera.transform.position = freeCamPosition;
        spectatorCamera.transform.rotation = freeCamRotation;
    }

    private void UpdateSpectatingUI()
    {
        if (spectatingText == null) return;

        spectatingText.gameObject.SetActive(true);

        if (isFreeCam)
        {
            spectatingText.text = "Free Cam (F to toggle)";
        }
        else if (alivePlayers.Count > 0 && currentIndex < alivePlayers.Count)
        {
            string playerName = alivePlayers[currentIndex].name;
            spectatingText.text = $"Spectating: {playerName} (← →)";
        }
        else
        {
            spectatingText.text = "No players to spectate";
        }
    }
}
