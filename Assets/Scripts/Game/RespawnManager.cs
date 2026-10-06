using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-side respawn manager. Handles respawn positions, physics reset, health restore.
/// Uses Netcode for GameObjects (NGO).
/// </summary>
public class RespawnManager : NetworkBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool randomSpawn = true;

    [Header("Respawn Settings")]
    [SerializeField] private float invulnerabilityDuration = 3f;

    private int nextSpawnIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Get the next spawn point
    /// </summary>
    public Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[RespawnManager] No spawn points configured!");
            return transform;
        }

        Transform spawn;
        if (randomSpawn)
        {
            spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
        }
        else
        {
            spawn = spawnPoints[nextSpawnIndex];
            nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Length;
        }

        return spawn;
    }

    /// <summary>
    /// Respawn a player at a spawn point (server only)
    /// </summary>
    public void RespawnPlayer(GameObject playerObject)
    {
        if (!IsServer) return;
        if (playerObject == null) return;

        Transform spawn = GetSpawnPoint();
        Vector3 pos = spawn != null ? spawn.position : playerObject.transform.position;
        Quaternion rot = spawn != null ? spawn.rotation : playerObject.transform.rotation;

        // Reset physics
        Rigidbody rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Respawn via PlayerHealth
        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Respawn(pos, rot);
        }
        else
        {
            // Just teleport
            playerObject.transform.SetPositionAndRotation(pos, rot);
        }

        // Reset kart state
        MirrorKartController kart = playerObject.GetComponent<MirrorKartController>();
        if (kart != null)
        {
            kart.ResetVehicle();
            kart.EnableControls();
        }

        var invuln = playerObject.GetComponent<TemporaryInvulnerability>();
        if (invuln != null)
        {
            invuln.StartInvulnerability(invulnerabilityDuration);
        }
    }

    /// <summary>
    /// Request respawn from client
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestRespawnServerRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
                if (health != null && health.IsDead)
                {
                    RespawnPlayer(client.PlayerObject.gameObject);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.green;
        foreach (var spawn in spawnPoints)
        {
            if (spawn != null)
            {
                Gizmos.DrawWireSphere(spawn.position, 1f);
                Gizmos.DrawRay(spawn.position, spawn.forward * 2f);
            }
        }
    }
}
