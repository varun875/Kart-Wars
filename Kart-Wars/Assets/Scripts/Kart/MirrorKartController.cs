using UnityEngine;
using Mirror;
using System;

/// <summary>
/// Main networked kart controller. Handles movement, input, shooting boomerang, 
/// dropping mines, pickups, fall detection, and collision damage for the local player.
/// Uses Mirror NetworkBehaviour and SyncVar hooks.
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

    // Synced variables
    [SyncVar(hook = nameof(OnCurrentWeaponChanged))]
    private WeaponType currentWeapon = WeaponType.None;

    [SyncVar]
    private int boomerangAmmo = 0;

    [SyncVar]
    private int mineAmmo = 0;

    [SyncVar]
    private bool isDrifting = false;

    public WeaponType CurrentWeapon => currentWeapon;
    public int BoomerangAmmo => boomerangAmmo;
    public int MineAmmo => mineAmmo;
    public bool IsDrifting => isDrifting;

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

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        
        // Enable camera for local player
        Camera playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
        }

        // Enable audio listener for local player
        AudioListener listener = GetComponentInChildren<AudioListener>(true);
        if (listener != null)
        {
            listener.enabled = true;
        }

        lastValidPosition = transform.position;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        HandleInput();
        UpdateVisuals();
        CheckFallDetection();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        CheckGrounded();
        HandleMovement();
        HandleDrift();
    }

    private void HandleInput()
    {
        // Movement input
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        brakeInput = Input.GetKey(KeyCode.Space);
        driftInput = Input.GetKey(KeyCode.LeftShift);

        // Weapon input
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
        isGrounded = Physics.Raycast(transform.position, -transform.up, groundCheckDistance, groundLayer);
    }

    private void HandleMovement()
    {
        if (!isGrounded) return;

        // Calculate current speed
        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        // Throttle
        if (throttleInput > 0)
        {
            if (currentSpeed < maxSpeed)
            {
                rb.AddForce(transform.forward * throttleInput * acceleration, ForceMode.Acceleration);
            }
        }
        else if (throttleInput < 0)
        {
            if (currentSpeed > -reverseSpeed)
            {
                rb.AddForce(transform.forward * throttleInput * acceleration * 0.5f, ForceMode.Acceleration);
            }
        }

        // Brake
        if (brakeInput)
        {
            rb.AddForce(-rb.linearVelocity.normalized * brakeForce, ForceMode.Acceleration);
        }

        // Steering (only when moving)
        if (Mathf.Abs(currentSpeed) > 0.5f)
        {
            float turnMultiplier = isDrifting ? driftTurnMultiplier : 1f;
            float turn = steerInput * turnSpeed * turnMultiplier * Time.fixedDeltaTime;
            
            // Reverse steering direction when going backwards
            if (currentSpeed < 0)
            {
                turn = -turn;
            }

            Quaternion turnRotation = Quaternion.Euler(0, turn, 0);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    private void HandleDrift()
    {
        bool wasDrifting = isDrifting;
        isDrifting = driftInput && isGrounded && Mathf.Abs(steerInput) > 0.5f;

        if (isDrifting)
        {
            // Apply drift drag
            Vector3 velocity = rb.linearVelocity;
            velocity.x *= driftDrag;
            velocity.z *= driftDrag;
            rb.linearVelocity = velocity;
        }

        // Sync drift state to server
        if (wasDrifting != isDrifting)
        {
            CmdSetDrifting(isDrifting);
        }

        // Visual effects
        if (driftParticles != null)
        {
            if (isDrifting && !driftParticles.isPlaying)
            {
                driftParticles.Play();
            }
            else if (!isDrifting && driftParticles.isPlaying)
            {
                driftParticles.Stop();
            }
        }

        // Tire trails
        foreach (var trail in tireTrails)
        {
            if (trail != null)
            {
                trail.emitting = isDrifting;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (kartModel != null)
        {
            // Tilt kart based on steering
            float targetTilt = -steerInput * tiltAmount;
            Vector3 currentRotation = kartModel.localEulerAngles;
            float currentTilt = currentRotation.z > 180 ? currentRotation.z - 360 : currentRotation.z;
            float newTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * 5f);
            kartModel.localEulerAngles = new Vector3(currentRotation.x, currentRotation.y, newTilt);
        }

        // Engine sound
        if (engineAudioSource != null)
        {
            float speedRatio = rb.linearVelocity.magnitude / maxSpeed;
            engineAudioSource.pitch = Mathf.Lerp(0.8f, 1.5f, speedRatio);
        }
    }

    private void CheckFallDetection()
    {
        if (transform.position.y < fallThreshold)
        {
            // Request respawn from server
            CmdRequestFallRespawn();
        }
        else if (isGrounded)
        {
            lastValidPosition = transform.position;
        }
    }

    [Command]
    private void CmdRequestFallRespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(fallDamage, netId);
        }

        // Respawn at last valid position or spawn point
        RespawnManager respawnManager = FindAnyObjectByType<RespawnManager>();
        if (respawnManager != null)
        {
            respawnManager.RespawnPlayer(gameObject);
        }
    }

    #region Weapon System

    private void TryFireWeapon()
    {
        if (currentWeapon == WeaponType.Boomerang && boomerangAmmo > 0)
        {
            if (Time.time - lastBoomerangTime >= boomerangCooldown)
            {
                lastBoomerangTime = Time.time;
                CmdFireBoomerang();
            }
        }
    }

    private void TryDropMine()
    {
        if (currentWeapon == WeaponType.Mine && mineAmmo > 0)
        {
            if (Time.time - lastMineTime >= mineCooldown)
            {
                lastMineTime = Time.time;
                CmdDropMine();
            }
        }
    }

    [Command]
    private void CmdFireBoomerang()
    {
        if (boomerangAmmo <= 0) return;
        if (boomerangPrefab == null) return;

        boomerangAmmo--;

        Vector3 spawnPos = weaponSpawnPoint != null ? weaponSpawnPoint.position : transform.position + transform.forward + Vector3.up;
        Quaternion spawnRot = weaponSpawnPoint != null ? weaponSpawnPoint.rotation : transform.rotation;

        GameObject boomerang = Instantiate(boomerangPrefab, spawnPos, spawnRot);
        
        BoomerangProjectile projectile = boomerang.GetComponent<BoomerangProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(gameObject, transform.forward);
        }

        NetworkServer.Spawn(boomerang);

        // Play sound on all clients
        RpcPlayWeaponSound(true);

        // Update weapon if out of ammo
        UpdateCurrentWeapon();
    }

    [Command]
    private void CmdDropMine()
    {
        if (mineAmmo <= 0) return;
        if (minePrefab == null) return;

        mineAmmo--;

        Vector3 spawnPos = mineDropPoint != null ? mineDropPoint.position : transform.position - transform.forward * 2f;
        
        GameObject mine = Instantiate(minePrefab, spawnPos, Quaternion.identity);
        
        MineScript mineScript = mine.GetComponent<MineScript>();
        if (mineScript != null)
        {
            mineScript.Initialize(gameObject);
        }

        NetworkServer.Spawn(mine);

        // Play sound on all clients
        RpcPlayWeaponSound(false);

        // Update weapon if out of ammo
        UpdateCurrentWeapon();
    }

    [ClientRpc]
    private void RpcPlayWeaponSound(bool isBoomerang)
    {
        AudioClip clip = isBoomerang ? shootSound : mineDropSound;
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    [Command]
    private void CmdSetDrifting(bool drifting)
    {
        isDrifting = drifting;
    }

    /// <summary>
    /// Called by WeaponPickup to give the player a weapon
    /// </summary>
    [Server]
    public void GiveWeapon(WeaponType type, int amount)
    {
        switch (type)
        {
            case WeaponType.Boomerang:
                boomerangAmmo += amount;
                break;
            case WeaponType.Mine:
                mineAmmo += amount;
                break;
        }

        UpdateCurrentWeapon();
    }

    [Server]
    private void UpdateCurrentWeapon()
    {
        if (boomerangAmmo > 0)
        {
            currentWeapon = WeaponType.Boomerang;
        }
        else if (mineAmmo > 0)
        {
            currentWeapon = WeaponType.Mine;
        }
        else
        {
            currentWeapon = WeaponType.None;
        }
    }

    private void OnCurrentWeaponChanged(WeaponType oldWeapon, WeaponType newWeapon)
    {
        OnWeaponChanged?.Invoke(newWeapon);
    }

    #endregion

    #region Collision Damage

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer) return;
        if (invulnerability != null && invulnerability.IsInvulnerable) return;

        // Check for collision damage based on impact velocity
        float impactVelocity = collision.relativeVelocity.magnitude;
        
        if (impactVelocity >= collisionDamageThreshold)
        {
            if (Time.time - lastCollisionDamageTime >= collisionDamageCooldown)
            {
                lastCollisionDamageTime = Time.time;
                float damage = (impactVelocity - collisionDamageThreshold) * collisionDamageMultiplier;
                
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, netId);
                }
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Disable player controls (used during death/respawn)
    /// </summary>
    public void DisableControls()
    {
        enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Enable player controls
    /// </summary>
    public void EnableControls()
    {
        enabled = true;
    }

    /// <summary>
    /// Reset vehicle state
    /// </summary>
    public void ResetVehicle()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Stop effects
        if (driftParticles != null && driftParticles.isPlaying)
        {
            driftParticles.Stop();
        }

        foreach (var trail in tireTrails)
        {
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }
    }

    #endregion
}
