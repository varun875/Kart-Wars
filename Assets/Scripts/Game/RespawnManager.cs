using UnityEngine;
using Mirror;

/// <summary>
/// Server-side respawn manager. Handles respawn positions, physics reset, health restore.
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
    [Server]
    public void RespawnPlayer(GameObject playerObject)
    {
        if (playerObject == null) return;

        Transform spawn = GetSpawnPoint();
        Vector3 pos = spawn.position;
        Quaternion rot = spawn.rotation;

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
    }

    /// <summary>
    /// Request respawn from client
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdRequestRespawn(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null) return;
        
        PlayerHealth health = sender.identity.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead)
        {
            RespawnPlayer(sender.identity.gameObject);
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
