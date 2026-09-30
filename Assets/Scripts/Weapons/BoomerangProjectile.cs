using UnityEngine;
using Mirror;

/// <summary>
/// Server-side boomerang logic. Moves, curves, returns to owner, 
/// damages players, and despawns.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BoomerangProjectile : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float returnSpeed = 30f;
    [SerializeField] private float curveAmount = 2f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Damage Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float knockbackForce = 10f;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Audio & Visuals")]
    [SerializeField] private AudioClip spinSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private ParticleSystem trailParticles;
    [SerializeField] private ParticleSystem hitParticles;

    // Synced state
    [SyncVar]
    private bool isReturning = false;

    // Server-side tracking
    private GameObject owner;
    private uint ownerNetId;
    private Vector3 initialPosition;
    private Vector3 moveDirection;
    private float curveDirection = 1f;
    private float lifetime;
    private bool hasHitPlayer = false;

    // Components
    private Rigidbody rb;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (spinSound != null && audioSource != null)
        {
            audioSource.clip = spinSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (trailParticles != null)
        {
            trailParticles.Play();
        }
    }

    /// <summary>
    /// Initialize boomerang with owner and direction (called on server)
    /// </summary>
    [Server]
    public void Initialize(GameObject ownerObject, Vector3 direction)
    {
        owner = ownerObject;
        ownerNetId = ownerObject.GetComponent<NetworkIdentity>().netId;
        initialPosition = transform.position;
        moveDirection = direction.normalized;
        
        // Randomly curve left or right
        curveDirection = Random.value > 0.5f ? 1f : -1f;
    }

    private void Update()
    {
        // Spin visual
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        
        lifetime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        if (isReturning)
        {
            MoveTowardsOwner();
        }
        else
        {
            MoveForward();
        }

        // Check lifetime
        if (lifetime >= maxLifetime)
        {
            DestroyBoomerang();
        }
    }

    [Server]
    private void MoveForward()
    {
        // Check if we should return
        float distanceTraveled = Vector3.Distance(transform.position, initialPosition);
        if (distanceTraveled >= maxDistance)
        {
            isReturning = true;
            return;
        }

        // Apply curve to direction
        moveDirection = Quaternion.Euler(0, curveAmount * curveDirection * Time.fixedDeltaTime, 0) * moveDirection;

        // Move forward
        Vector3 newPosition = transform.position + moveDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);

        // Face movement direction
        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
    }

    [Server]
    private void MoveTowardsOwner()
    {
        if (owner == null)
        {
            DestroyBoomerang();
            return;
        }

        Vector3 targetPosition = owner.transform.position + Vector3.up;
        Vector3 direction = (targetPosition - transform.position).normalized;

        // Move towards owner
        Vector3 newPosition = transform.position + direction * returnSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);

        // Check if close to owner
        float distance = Vector3.Distance(transform.position, targetPosition);
        if (distance < 1.5f)
        {
            CatchBoomerang();
        }
    }

    [Server]
    private void CatchBoomerang()
    {
        // Could give ammo back, trigger pickup sound, etc.
        RpcPlayCatchEffect();
        DestroyBoomerang();
    }

    [ClientRpc]
    private void RpcPlayCatchEffect()
    {
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position, 0.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;
        if (hasHitPlayer) return;

        // Ignore owner
        NetworkIdentity otherIdentity = other.GetComponentInParent<NetworkIdentity>();
        if (otherIdentity != null && otherIdentity.netId == ownerNetId)
        {
            // If returning and hit owner, catch it
            if (isReturning)
            {
                CatchBoomerang();
            }
            return;
        }

        // Check for player health
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && !playerHealth.IsDead)
        {
            // Apply damage
            playerHealth.TakeDamage(damage, ownerNetId);
            
            // Apply knockback
            Rigidbody targetRb = other.GetComponentInParent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDir = (other.transform.position - transform.position).normalized;
                targetRb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
            }

            hasHitPlayer = true;
            RpcPlayHitEffect(other.ClosestPoint(transform.position));

            // Return to owner after hitting
            isReturning = true;
            return;
        }

        // Check for enemy kart
        EnemyKart enemyKart = other.GetComponentInParent<EnemyKart>();
        if (enemyKart != null)
        {
            enemyKart.TakeDamage(damage, ownerNetId);
            
            hasHitPlayer = true;
            RpcPlayHitEffect(other.ClosestPoint(transform.position));
            isReturning = true;
            return;
        }

        // Hit environment - bounce back
        if (!other.isTrigger && other.GetComponentInParent<NetworkIdentity>() == null)
        {
            RpcPlayHitEffect(other.ClosestPoint(transform.position));
            isReturning = true;
        }
    }

    [ClientRpc]
    private void RpcPlayHitEffect(Vector3 hitPosition)
    {
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, hitPosition);
        }

        if (hitParticles != null)
        {
            Instantiate(hitParticles, hitPosition, Quaternion.identity);
        }
    }

    [Server]
    private void DestroyBoomerang()
    {
        if (trailParticles != null)
        {
            trailParticles.Stop();
        }

        NetworkServer.Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}
