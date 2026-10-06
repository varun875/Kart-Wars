using UnityEngine;
using Unity.Netcode;
using System;

/// <summary>
/// Main networked kart controller. Handles movement, input, shooting boomerang,
/// dropping mines, pickups, fall detection, and collision damage for the local player.
/// Uses NGO NetworkBehaviour and NetworkVariable hooks.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerHealth))]
public class MirrorKartController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float reverseSpeed = 10f;
    [SerializeField] private float brakeForce = 15f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float driftTurnMultiplier = 1.5f;
    [SerializeField] private float driftDrag = 0.95f;
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Fall Detection")]
    [SerializeField] private float fallThreshold = -20f;
    [SerializeField] private float fallDamage = 25f;

    [Header("Collision Damage")]
    [SerializeField] private float collisionDamageThreshold = 10f;
    [SerializeField] private float collisionDamageMultiplier = 0.5f;
    [SerializeField] private float collisionDamageCooldown = 1f;

    [Header("Weapons")]
    [SerializeField] private GameObject boomerangPrefab;
    [SerializeField] private GameObject minePrefab;
    [SerializeField] private Transform weaponSpawnPoint;
    [SerializeField] private Transform mineDropPoint;
    [SerializeField] private float boomerangCooldown = 0.5f;
    [SerializeField] private float mineCooldown = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip mineDropSound;

    [Header("Visuals")]
    [SerializeField] private Transform kartModel;
    [SerializeField] private float tiltAmount = 15f;
    [SerializeField] private ParticleSystem driftParticles;
    [SerializeField] private TrailRenderer[] tireTrails;

    // NGO NetworkVariables
    private NetworkVariable<WeaponType> currentWeapon = new NetworkVariable<WeaponType>(
        WeaponType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> boomerangAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> mineAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDrifting = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Public accessors
    public WeaponType CurrentWeapon => currentWeapon.Value;
    public int BoomerangAmmo => boomerangAmmo.Value;
    public int MineAmmo => mineAmmo.Value;
    public bool IsDrifting => isDrifting.Value;

    // Events for UI updates
    public event Action<WeaponType> OnWeaponChanged;
    public event Action<int> OnBoomerangAmmoChanged;
    public event Action<int> OnMineAmmoChanged;

    // Components
    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private TemporaryInvulnerability invulnerability;

    // Input state
    private float throttleInput;
    private float steerInput;
    private bool brakeInput;
    private bool driftInput;
    private bool fireInput;
    private bool mineInput;

    // Internal state
    private bool isGrounded;
    private float lastBoomerangTime;
    private float lastMineTime;
    private float lastCollisionDamageTime;
    private Vector3 lastValidPosition;

    // Helper to safely convert ulong clientId to uint
    private uint ToUint(ulong clientId) => (uint)(clientId & 0xFFFFFFFF);

    public enum WeaponType
    {
        None,
        Boomerang,
        Mine
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        invulnerability = GetComponent<TemporaryInvulnerability>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentWeapon.OnValueChanged += OnCurrentWeaponChanged;
        boomerangAmmo.OnValueChanged += (old, newVal) => OnBoomerangAmmoChanged?.Invoke(newVal);
        mineAmmo.OnValueChanged += (old, newVal) => OnMineAmmoChanged?.Invoke(newVal);

        Camera playerCamera = GetComponentInChildren<Camera>(true);
        AudioListener listener = GetComponentInChildren<AudioListener>(true);
        var cinemachineCamera = GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);

        if (IsOwner)
        {
            // Owner instance: enable local-only components
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);

            if (listener != null)
                listener.enabled = true;

            if (cinemachineCamera != null)
                cinemachineCamera.enabled = true;

            if (rb != null)
                rb.isKinematic = false;

            lastValidPosition = transform.position;
        }
        else
        {
            // Non-owner instances: disable local-only components and let NetworkTransform sync position
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);

            if (listener != null)
                listener.enabled = false;

            if (cinemachineCamera != null)
                cinemachineCamera.enabled = false;

            if (rb != null)
                rb.isKinematic = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentWeapon.OnValueChanged -= OnCurrentWeaponChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        HandleInput();
        UpdateVisuals();
        CheckFallDetection();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        CheckGrounded();
        HandleMovement();
        HandleDrift();
    }

    private void HandleInput()
    {
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        brakeInput = Input.GetKey(KeyCode.Space);
        driftInput = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetButtonDown("Fire1") && !fireInput)
        {
            fireInput = true;
            TryFireWeapon();
        }
        else
        {
            fireInput = Input.GetButton("Fire1");
        }

        if (Input.GetKeyDown(KeyCode.E) && !mineInput)
        {
            mineInput = true;
            TryDropMine();
        }
        else
        {
            mineInput = Input.GetKey(KeyCode.E);
        }
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(
            transform.position, -transform.up, groundCheckDistance, groundLayer
        );
    }

    private void HandleMovement()
    {
        if (!isGrounded) return;

        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (throttleInput > 0)
        {
            if (currentSpeed < maxSpeed)
                rb.AddForce(transform.forward * throttleInput * acceleration, ForceMode.Acceleration);
        }
        else if (throttleInput < 0)
        {
            if (currentSpeed > -reverseSpeed)
                rb.AddForce(transform.forward * throttleInput * acceleration * 0.5f, ForceMode.Acceleration);
        }

        if (brakeInput)
            rb.AddForce(-rb.linearVelocity.normalized * brakeForce, ForceMode.Acceleration);

        if (Mathf.Abs(currentSpeed) > 0.5f)
        {
            float turnMultiplier = isDrifting.Value ? driftTurnMultiplier : 1f;
            float turn = steerInput * turnSpeed * turnMultiplier * Time.fixedDeltaTime;

            if (currentSpeed < 0) turn = -turn;

            Quaternion turnRotation = Quaternion.Euler(0, turn, 0);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    private void HandleDrift()
    {
        bool wasDrifting = isDrifting.Value;
        bool newDrifting = driftInput && isGrounded && Mathf.Abs(steerInput) > 0.5f;

        if (newDrifting)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x *= driftDrag;
            velocity.z *= driftDrag;
            rb.linearVelocity = velocity;
        }

        if (wasDrifting != newDrifting)
            SetDriftingServerRpc(newDrifting);

        if (driftParticles != null)
        {
            if (newDrifting && !driftParticles.isPlaying)
                driftParticles.Play();
            else if (!newDrifting && driftParticles.isPlaying)
                driftParticles.Stop();
        }

        foreach (var trail in tireTrails)
        {
            if (trail != null)
                trail.emitting = newDrifting;
        }
    }

    private void UpdateVisuals()
    {
        if (kartModel != null)
        {
            float targetTilt = -steerInput * tiltAmount;
            Vector3 currentRotation = kartModel.localEulerAngles;
            float currentTilt = currentRotation.z > 180 ? currentRotation.z - 360 : currentRotation.z;
            float newTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * 5f);
            kartModel.localEulerAngles = new Vector3(currentRotation.x, currentRotation.y, newTilt);
        }

        if (engineAudioSource != null)
        {
            float speedRatio = rb.linearVelocity.magnitude / maxSpeed;
            engineAudioSource.pitch = Mathf.Lerp(0.8f, 1.5f, speedRatio);
        }
    }

    private void CheckFallDetection()
    {
        if (transform.position.y < fallThreshold)
            RequestFallRespawnServerRpc();
        else if (isGrounded)
            lastValidPosition = transform.position;
    }

    // ── SERVER RPCs ───────────────────────────────────────────────

    [ServerRpc]
    private void RequestFallRespawnServerRpc()
    {
        if (playerHealth != null)
            playerHealth.TakeDamage(fallDamage, ToUint(OwnerClientId));

        RespawnManager respawnManager = FindAnyObjectByType<RespawnManager>();
        if (respawnManager != null)
            respawnManager.RespawnPlayer(gameObject);
    }

    [ServerRpc]
    private void SetDriftingServerRpc(bool drifting)
    {
        isDrifting.Value = drifting;
    }

    [ServerRpc]
    private void FireBoomerangServerRpc()
    {
        if (boomerangAmmo.Value <= 0) return;
        if (boomerangPrefab == null) return;

        boomerangAmmo.Value--;

        Vector3 spawnPos = weaponSpawnPoint != null
            ? weaponSpawnPoint.position
            : transform.position + transform.forward + Vector3.up;

        Quaternion spawnRot = weaponSpawnPoint != null
            ? weaponSpawnPoint.rotation
            : transform.rotation;

        GameObject boomerang = Instantiate(boomerangPrefab, spawnPos, spawnRot);

        BoomerangProjectile projectile = boomerang.GetComponent<BoomerangProjectile>();
        if (projectile != null)
            projectile.Initialize(gameObject, transform.forward);

        boomerang.GetComponent<NetworkObject>().Spawn();

        PlayWeaponSoundClientRpc(true);
        UpdateCurrentWeapon();
    }

    [ServerRpc]
    private void DropMineServerRpc()
    {
        if (mineAmmo.Value <= 0) return;
        if (minePrefab == null) return;

        mineAmmo.Value--;

        Vector3 spawnPos = mineDropPoint != null
            ? mineDropPoint.position
            : transform.position - transform.forward * 2f;

        GameObject mine = Instantiate(minePrefab, spawnPos, Quaternion.identity);

        MineScript mineScript = mine.GetComponent<MineScript>();
        if (mineScript != null)
            mineScript.Initialize(gameObject);

        mine.GetComponent<NetworkObject>().Spawn();

        PlayWeaponSoundClientRpc(false);
        UpdateCurrentWeapon();
    }

    // ── CLIENT RPCs ───────────────────────────────────────────────

    [ClientRpc]
    private void PlayWeaponSoundClientRpc(bool isBoomerang)
    {
        AudioClip clip = isBoomerang ? shootSound : mineDropSound;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    // ── WEAPON SYSTEM ─────────────────────────────────────────────

    private void TryFireWeapon()
    {
        if (currentWeapon.Value == WeaponType.Boomerang && boomerangAmmo.Value > 0)
        {
            if (Time.time - lastBoomerangTime >= boomerangCooldown)
            {
                lastBoomerangTime = Time.time;
                FireBoomerangServerRpc();
            }
        }
    }

    private void TryDropMine()
    {
        if (currentWeapon.Value == WeaponType.Mine && mineAmmo.Value > 0)
        {
            if (Time.time - lastMineTime >= mineCooldown)
            {
                lastMineTime = Time.time;
                DropMineServerRpc();
            }
        }
    }

    public void GiveWeapon(WeaponType type, int amount)
    {
        if (!IsServer) return;

        switch (type)
        {
            case WeaponType.Boomerang:
                boomerangAmmo.Value += amount;
                break;
            case WeaponType.Mine:
                mineAmmo.Value += amount;
                break;
        }

        UpdateCurrentWeapon();
    }

    private void UpdateCurrentWeapon()
    {
        if (!IsServer) return;

        if (boomerangAmmo.Value > 0)
            currentWeapon.Value = WeaponType.Boomerang;
        else if (mineAmmo.Value > 0)
            currentWeapon.Value = WeaponType.Mine;
        else
            currentWeapon.Value = WeaponType.None;
    }

    private void OnCurrentWeaponChanged(WeaponType oldWeapon, WeaponType newWeapon)
    {
        OnWeaponChanged?.Invoke(newWeapon);
    }

    // ── COLLISION DAMAGE ──────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (invulnerability != null && invulnerability.IsInvulnerable) return;

        float impactVelocity = collision.relativeVelocity.magnitude;

        if (impactVelocity >= collisionDamageThreshold)
        {
            if (Time.time - lastCollisionDamageTime >= collisionDamageCooldown)
            {
                lastCollisionDamageTime = Time.time;
                float damage = (impactVelocity - collisionDamageThreshold) * collisionDamageMultiplier;

                if (playerHealth != null)
                    playerHealth.TakeDamage(damage, ToUint(OwnerClientId));
            }
        }
    }

    // ── PUBLIC METHODS ────────────────────────────────────────────

    public void DisableControls()
    {
        enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void EnableControls() => enabled = true;

    public void ResetVehicle()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (driftParticles != null && driftParticles.isPlaying)
            driftParticles.Stop();

        foreach (var trail in tireTrails)
        {
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }
    }
}