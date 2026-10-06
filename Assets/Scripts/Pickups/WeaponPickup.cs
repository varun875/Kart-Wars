using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Networked pickup spawner. Gives boomerang/mine, handles respawn and visuals.
/// Uses Netcode for GameObjects (NGO).
/// </summary>
public class WeaponPickup : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private MirrorKartController.WeaponType weaponType = MirrorKartController.WeaponType.Boomerang;
    [SerializeField] private int ammoAmount = 3;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnTime = 10f;

    [Header("Visuals")]
    [SerializeField] private GameObject pickupModel;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobHeight = 0.5f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem idleParticles;
    [SerializeField] private ParticleSystem pickupParticles;
    [SerializeField] private AudioClip pickupSound;

    private readonly NetworkVariable<bool> isAvailable = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 startPosition;
    private Collider pickupCollider;

    public bool IsAvailable => isAvailable.Value;

    private void Awake()
    {
        startPosition = pickupModel != null ? pickupModel.transform.position : transform.position;
        pickupCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isAvailable.OnValueChanged += OnAvailableChanged;
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isAvailable.OnValueChanged -= OnAvailableChanged;
    }

    private void Update()
    {
        if (!isAvailable.Value) return;

        // Rotate
        if (pickupModel != null)
        {
            pickupModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Bob up and down
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            pickupModel.transform.position = new Vector3(
                pickupModel.transform.position.x,
                newY,
                pickupModel.transform.position.z
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isAvailable.Value) return;

        MirrorKartController kart = other.GetComponentInParent<MirrorKartController>();
        if (kart != null)
        {
            GivePickup(kart);
        }
    }

    private void GivePickup(MirrorKartController kart)
    {
        if (!IsServer) return;

        // Give weapon to player
        kart.GiveWeapon(weaponType, ammoAmount);

        // Disable pickup
        isAvailable.Value = false;

        // Play pickup effect on all clients
        PlayPickupEffectClientRpc();

        // Start respawn timer
        StartCoroutine(RespawnCoroutine());
    }

    [ClientRpc]
    private void PlayPickupEffectClientRpc()
    {
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        if (pickupParticles != null)
        {
            pickupParticles.Play();
        }
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnTime);
        if (IsServer)
        {
            isAvailable.Value = true;
        }
    }

    private void OnAvailableChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (pickupModel != null)
        {
            pickupModel.SetActive(isAvailable.Value);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = isAvailable.Value;
        }

        if (idleParticles != null)
        {
            if (isAvailable.Value && !idleParticles.isPlaying)
            {
                idleParticles.Play();
            }
            else if (!isAvailable.Value && idleParticles.isPlaying)
            {
                idleParticles.Stop();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = weaponType == MirrorKartController.WeaponType.Boomerang 
            ? Color.cyan 
            : Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
