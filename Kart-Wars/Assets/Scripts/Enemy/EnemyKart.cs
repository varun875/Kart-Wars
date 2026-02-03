using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Server-authoritative AI/enemy health, death/respawn logic.
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

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    [SyncVar]
    private bool isDead = false;

    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!isServer || isDead || !isAI) return;
        UpdateAI();
    }

    [Server]
    private void UpdateAI()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Transform target = waypoints[currentWaypointIndex];
        if (target == null) return;

        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        float dist = Vector3.Distance(transform.position, target.position);

        if (dist < waypointThreshold)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            return;
        }

        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        if (rb != null) rb.linearVelocity = transform.forward * moveSpeed;
        else transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    [Server]
    public void TakeDamage(float damage, uint attackerNetId)
    {
        if (isDead) return;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        RpcPlayHitEffect();
        if (currentHealth <= 0) Die(attackerNetId);
    }

    [ClientRpc]
    private void RpcPlayHitEffect()
    {
        if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, transform.position);
    }

    [Server]
    private void Die(uint killerNetId)
    {
        if (isDead) return;
        isDead = true;

        if (MirrorGameMaster.Instance != null)
            MirrorGameMaster.Instance.AddScore(killerNetId, scoreValue);

        RpcPlayDeathEffect();
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
        if (canRespawn) StartCoroutine(RespawnCoroutine());
        else StartCoroutine(DestroyAfterDelay());
    }

    [ClientRpc]
    private void RpcPlayDeathEffect()
    {
        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);
        if (deathEffectPrefab != null) Destroy(Instantiate(deathEffectPrefab, transform.position, Quaternion.identity), 3f);
        foreach (var r in meshRenderers) if (r != null) r.enabled = false;
        OnDeath?.Invoke();
    }

    [Server]
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    public void Respawn()
    {
        currentHealth = maxHealth;
        isDead = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }
        currentWaypointIndex = 0;
        RpcOnRespawn();
    }

    [ClientRpc]
    private void RpcOnRespawn()
    {
        foreach (var r in meshRenderers) if (r != null) r.enabled = true;
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        OnHealthUpdated?.Invoke(newHealth, maxHealth);
    }
}
