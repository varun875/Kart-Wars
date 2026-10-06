using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Mine projectile. Arms after delay, explodes on proximity,
/// damages nearby players, handles visuals.
/// Uses Netcode for GameObjects (NGO).
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
    private readonly NetworkVariable<bool> isArmed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isExploding = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Server-side tracking
    private GameObject owner;
    private ulong ownerClientId;
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
    public void Initialize(GameObject ownerObject)
    {
        owner = ownerObject;
        var netObj = ownerObject.GetComponent<NetworkObject>();
        ownerClientId = netObj != null ? netObj.OwnerClientId : 0;
        spawnTime = Time.time;

        // Start arming coroutine
        StartCoroutine(ArmingSequence());
        
        // Start lifetime countdown
        StartCoroutine(LifetimeCoroutine());
    }

    private IEnumerator ArmingSequence()
    {
        yield return new WaitForSeconds(armingDelay);
        
        isArmed.Value = true;
        PlayArmSoundClientRpc();
    }

    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        
        if (!isExploding.Value)
        {
            // Self-destruct without explosion
            DestroyMine();
        }
    }

    [ClientRpc]
    private void PlayArmSoundClientRpc()
    {
        if (armSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(armSound);
        }
    }

    private void Update()
    {
        if (!isArmed.Value) return;

        UpdateVisuals();

        if (IsServer && !isExploding.Value)
        {
            CheckForPlayers();
        }
    }

    private void UpdateVisuals()
    {
        // Update material
        if (mineRenderer != null)
        {
            mineRenderer.material = isArmed.Value ? armedMaterial : disarmedMaterial;
        }

        // Blink warning light
        if (warningLight != null && isArmed.Value)
        {
            warningLight.enabled = true;

            if (Time.time - lastBlinkTime >= 1f / Mathf.Max(0.1f, blinkRate))
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

    private void CheckForPlayers()
    {
        if (!IsServer) return;

        // Get all colliders in detection range
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);

        foreach (Collider col in colliders)
        {
            NetworkObject netObj = col.GetComponentInParent<NetworkObject>();
            if (netObj == null) continue;

            // Check if this is the owner
            if (netObj.OwnerClientId == ownerClientId)
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

    public void Explode()
    {
        if (!IsServer) return;
        if (isExploding.Value) return;
        isExploding.Value = true;

        // Get all objects in explosion radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        HashSet<NetworkObject> damagedObjects = new HashSet<NetworkObject>();

        foreach (Collider col in colliders)
        {
            NetworkObject netObj = col.GetComponentInParent<NetworkObject>();
            if (netObj != null && damagedObjects.Contains(netObj))
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
                playerHealth.TakeDamage(actualDamage, ownerClientId);
                
                if (netObj != null)
                {
                    damagedObjects.Add(netObj);
                }
            }

            // Apply damage to enemies
            EnemyKart enemyKart = col.GetComponentInParent<EnemyKart>();
            if (enemyKart != null)
            {
                enemyKart.TakeDamage(actualDamage, (uint)ownerClientId);
                
                if (netObj != null)
                {
                    damagedObjects.Add(netObj);
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
        ExplodeClientRpc();

        // Delayed destroy to allow effect to sync
        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        DestroyMine();
    }

    private void DestroyMine()
    {
        if (!IsServer) return;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void ExplodeClientRpc()
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
