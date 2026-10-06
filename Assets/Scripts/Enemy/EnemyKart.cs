using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Server-authoritative AI/enemy health, death/respawn logic.
/// Uses Netcode for GameObjects (NGO).
/// </summary>
public class EnemyKart : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private int scoreValue = 1;

    [Header("Visual/Audio")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private MeshRenderer[] meshRenderers;

    [Header("AI Movement")]
    [SerializeField] private bool isAI = true;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waypointThreshold = 2f;

    private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;
    public event System.Action<float, float> OnHealthUpdated;
    public event System.Action OnDeath;

    private int currentWaypointIndex = 0;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void Update()
    {
        if (!IsServer || isDead.Value || !isAI) return;
        HandleAIMovement();
    }

    private void HandleAIMovement()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform targetWaypoint = waypoints[currentWaypointIndex];
        if (targetWaypoint == null) return;

        Vector3 targetDirection = (targetWaypoint.position - transform.position).normalized;
        targetDirection.y = 0;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, targetWaypoint.position) < waypointThreshold)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    public void TakeDamage(float damage, uint killerNetId)
    {
        if (!IsServer || isDead.Value) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        PlayHitEffectClientRpc();

        if (currentHealth.Value <= 0)
        {
            Die(killerNetId);
        }
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc()
    {
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }
    }

    private void Die(uint killerNetId)
    {
        if (!IsServer || isDead.Value) return;
        isDead.Value = true;

        if (MirrorGameMaster.Instance != null)
        {
            MirrorGameMaster.Instance.AddScore(killerNetId, scoreValue);
        }

        OnDeathClientRpc();

        if (canRespawn)
        {
            StartCoroutine(RespawnCoroutine());
        }
    }

    [ClientRpc]
    private void OnDeathClientRpc()
    {
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
                if (r != null) r.enabled = false;
        }

        if (rb != null) rb.isKinematic = true;

        OnDeath?.Invoke();
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        currentHealth.Value = maxHealth;
        isDead.Value = false;
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        currentWaypointIndex = 0;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }

        OnRespawnClientRpc();
    }

    [ClientRpc]
    private void OnRespawnClientRpc()
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
                if (r != null) r.enabled = true;
        }
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        OnHealthUpdated?.Invoke(newValue, maxHealth);
    }
}
