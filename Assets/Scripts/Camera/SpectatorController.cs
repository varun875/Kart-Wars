using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

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
        
        var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (var player in allPlayers)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (!player.IsDead && (netObj == null || !netObj.IsOwner))
            {
                alivePlayers.Add(player);
            }
        }
    }

    private void CycleToNextPlayer()
    {
        RefreshPlayerList();
        if (alivePlayers.Count == 0) return;

        currentIndex = (currentIndex + 1) % alivePlayers.Count;
        UpdateSpectatingUI();
    }

    private void CycleToPreviousPlayer()
    {
        RefreshPlayerList();
        if (alivePlayers.Count == 0) return;

        currentIndex = (currentIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
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
        if (alivePlayers.Count == 0)
        {
            RefreshPlayerList();
            if (alivePlayers.Count == 0) return;
        }

        if (currentIndex >= alivePlayers.Count)
        {
            currentIndex = 0;
        }

        PlayerHealth target = alivePlayers[currentIndex];
        if (target == null || target.IsDead)
        {
            RefreshPlayerList();
            return;
        }

        Transform targetTransform = target.transform;
        Vector3 desiredPosition = targetTransform.position - targetTransform.forward * followDistance + Vector3.up * followHeight;
        
        if (spectatorCamera != null)
        {
            spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            Vector3 lookTarget = targetTransform.position + targetTransform.forward * lookAheadDistance;
            spectatorCamera.transform.LookAt(lookTarget);
        }
    }

    private void UpdateFreeCam()
    {
        if (spectatorCamera == null) return;

        float moveSpeed = 15f;
        float rotSpeed = 3f;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (spectatorCamera.transform.forward * v + spectatorCamera.transform.right * h) * moveSpeed * Time.deltaTime;
        freeCamPosition += move;
        spectatorCamera.transform.position = freeCamPosition;

        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotSpeed;
            float mouseY = -Input.GetAxis("Mouse Y") * rotSpeed;
            freeCamRotation = Quaternion.Euler(freeCamRotation.eulerAngles.x + mouseY, freeCamRotation.eulerAngles.y + mouseX, 0);
            spectatorCamera.transform.rotation = freeCamRotation;
        }
    }

    private void UpdateSpectatingUI()
    {
        if (spectatingText == null) return;

        if (isFreeCam)
        {
            spectatingText.text = "Free Camera (WASD to move, Right Mouse to look)";
        }
        else if (alivePlayers.Count > 0 && currentIndex < alivePlayers.Count && alivePlayers[currentIndex] != null)
        {
            spectatingText.text = $"Spectating Player ({currentIndex + 1}/{alivePlayers.Count})";
        }
        else
        {
            spectatingText.text = "Waiting for players...";
        }

        spectatingText.gameObject.SetActive(true);
    }
}
