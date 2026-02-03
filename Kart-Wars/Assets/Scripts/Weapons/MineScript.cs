using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Mine projectile. Arms after delay, explodes on proximity,
/// damages nearby players, handles visuals.
/// </summary>
public class MineScript : NetworkBehaviour
{
    [Header("Mine Settings")]
    [SerializeField] private float armingDelay = 1.5f;
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private float explosionForce = 15f;
    [SerializeField] private float lifetime = 60f;

    [Header("Visual Settings")]
    [SerializeField] private MeshRenderer mineRenderer;
    [SerializeField] private Material armedMaterial;
    [SerializeField] private Material disarmedMaterial;
    [SerializeField] private Light warningLight;
    [SerializeField] private float blinkRate = 2f;

    [Header("Explosion Effects")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private AudioClip armSound;
    [SerializeField] private AudioClip beepSound;
    [SerializeField] private AudioClip explosionSound;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool detectOwner = false;
    [SerializeField] private float ownerDetectionDelay = 3f;

    // Synced state
    [SyncVar(hook = nameof(OnArmedStateChanged))]
    private bool isArmed = false;

    [SyncVar]
    private bool isExploding = false;

    // Server-side tracking
    private GameObject owner;
    private uint ownerNetId;
    private float spawnTime;
    private float lastBlinkTime;
    private bool lightState;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (warningLight != null)
        {
            warningLight.enabled = false;
        }
    }

    /// <summary>
    /// Initialize the mine with its owner (called on server)
    /// </summary>
    [Server]
    public void Initialize(GameObject ownerObject)
    {
        owner = ownerObject;
        ownerNetId = ownerObject.GetComponent<NetworkIdentity>().netId;
        spawnTime = Time.time;

        // Start arming coroutine
        StartCoroutine(ArmingSequence());
        
        // Start lifetime countdown
        StartCoroutine(LifetimeCoroutine());
    }

    [Server]
    private IEnumerator ArmingSequence()
    {
        yield return new WaitForSeconds(armingDelay);
        
        isArmed = true;
        RpcPlayArmSound();
    }

    [Server]
    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        
        if (!isExploding)
        {
            // Self-destruct without explosion
            NetworkServer.Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void RpcPlayArmSound()
    {
        if (armSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(armSound);
        }
    }

    private void Update()
    {
        if (!isArmed) return;

        UpdateVisuals();

        if (isServer && !isExploding)
        {
            CheckForPlayers();
        }
    }

    private void UpdateVisuals()
    {
        // Update material
        if (mineRenderer != null)
        {
            mineRenderer.material = isArmed ? armedMaterial : disarmedMaterial;
        }

        // Blink warning light
        if (warningLight != null && isArmed)
        {
            warningLight.enabled = true;

            if (Time.time - lastBlinkTime >= 1f / blinkRate)
            {
                lastBlinkTime = Time.time;
                lightState = !lightState;
                warningLight.intensity = lightState ? 2f : 0.5f;

                // Play beep sound
                if (lightState && beepSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(beepSound, 0.3f);
                }
            }
        }
    }

    [Server]
    private void CheckForPlayers()
    {
        // Get all colliders in detection range
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);

        foreach (Collider col in colliders)
        {
            NetworkIdentity identity = col.GetComponentInParent<NetworkIdentity>();
            if (identity == null) continue;

            // Check if this is the owner
            if (identity.netId == ownerNetId)
            {
                // Only detect owner after delay
                if (!detectOwner && Time.time - spawnTime < ownerDetectionDelay)
                {
                    continue;
                }
            }

            // Check if player is alive
            PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
            if (health != null && !health.IsDead)
            {
                Explode();
                return;
            }

            // Check for vehicles
            MirrorKartController kart = col.GetComponentInParent<MirrorKartController>();
            if (kart != null)
            {
                Explode();
                return;
            }
        }
    }

    [Server]
    public void Explode()
    {
        if (isExploding) return;
        isExploding = true;

        // Get all objects in explosion radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        HashSet<NetworkIdentity> damagedObjects = new HashSet<NetworkIdentity>();

        foreach (Collider col in colliders)
        {
            NetworkIdentity identity = col.GetComponentInParent<NetworkIdentity>();
            if (identity != null && damagedObjects.Contains(identity))
            {
                continue; // Already damaged this object
            }

            // Calculate damage falloff based on distance
            float distance = Vector3.Distance(transform.position, col.transform.position);
            float damageMultiplier = 1f - (distance / explosionRadius);
            damageMultiplier = Mathf.Clamp01(damageMultiplier);

            float actualDamage = explosionDamage * damageMultiplier;

            // Apply damage to players
            PlayerHealth playerHealth = col.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.TakeDamage(actualDamage, ownerNetId);
                
                if (identity != null)
                {
                    damagedObjects.Add(identity);
                }
            }

            // Apply damage to enemies
            EnemyKart enemyKart = col.GetComponentInParent<EnemyKart>();
            if (enemyKart != null)
            {
                enemyKart.TakeDamage(actualDamage, ownerNetId);
                
                if (identity != null)
                {
                    damagedObjects.Add(identity);
                }
            }

            // Apply explosion force
            Rigidbody rb = col.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce * damageMultiplier, transform.position, explosionRadius, 0.5f, ForceMode.Impulse);
            }
        }

        // Trigger explosion effect on all clients
        RpcExplode();

        // Delayed destroy to allow effect to sync
        StartCoroutine(DestroyAfterDelay());
    }

    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcExplode()
    {
        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        // Spawn explosion effect
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // Disable renderer
        if (mineRenderer != null)
        {
            mineRenderer.enabled = false;
        }

        if (warningLight != null)
        {
            warningLight.enabled = false;
        }
    }

    private void OnArmedStateChanged(bool oldValue, bool newValue)
    {
        // Visual update handled in Update()
    }

    private void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Explosion radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
