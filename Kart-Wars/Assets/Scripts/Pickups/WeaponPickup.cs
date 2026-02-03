using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Networked pickup spawner. Gives boomerang/mine, handles respawn and visuals.
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

    [SyncVar(hook = nameof(OnAvailableChanged))]
    private bool isAvailable = true;

    private Vector3 startPosition;
    private Collider pickupCollider;

    public bool IsAvailable => isAvailable;

    private void Awake()
    {
        startPosition = pickupModel != null ? pickupModel.transform.position : transform.position;
        pickupCollider = GetComponent<Collider>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateVisuals();
    }

    private void Update()
    {
        if (!isAvailable) return;

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
        if (!isServer) return;
        if (!isAvailable) return;

        MirrorKartController kart = other.GetComponentInParent<MirrorKartController>();
        if (kart != null)
        {
            GivePickup(kart);
        }
    }

    [Server]
    private void GivePickup(MirrorKartController kart)
    {
        // Give weapon to player
        kart.GiveWeapon(weaponType, ammoAmount);

        // Disable pickup
        isAvailable = false;

        // Play pickup effect on all clients
        RpcPlayPickupEffect();

        // Start respawn timer
        StartCoroutine(RespawnCoroutine());
    }

    [ClientRpc]
    private void RpcPlayPickupEffect()
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

    [Server]
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnTime);
        isAvailable = true;
    }

    private void OnAvailableChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (pickupModel != null)
        {
            pickupModel.SetActive(isAvailable);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = isAvailable;
        }

        if (idleParticles != null)
        {
            if (isAvailable && !idleParticles.isPlaying)
            {
                idleParticles.Play();
            }
            else if (!isAvailable && idleParticles.isPlaying)
            {
                idleParticles.Stop();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = weaponType == MirrorKartController.WeaponType.Boomerang 
            ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
