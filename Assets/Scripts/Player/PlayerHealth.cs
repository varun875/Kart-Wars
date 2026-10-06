using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Networked player health, death state, respawn flow, component disabling.
/// Uses Netcode for GameObjects (NGO).
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnTime = 3f;

    [Header("Visual/Audio")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private MeshRenderer[] meshRenderers;

    [Header("Components to Disable on Death")]
    [SerializeField] private MonoBehaviour[] componentsToDisable;

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

    private readonly NetworkVariable<ulong> lastAttackerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead.Value;
    public float RespawnTime => respawnTime;

    public event System.Action<float, float> OnHealthUpdated;
    public event System.Action OnDeath;
    public event System.Action OnRespawned;

    private MirrorKartController kartController;
    private Rigidbody rb;

    private void Awake()
    {
        kartController = GetComponent<MirrorKartController>();
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentHealth.OnValueChanged += OnHealthChanged;
        isDead.OnValueChanged += OnDeadStateChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }

        OnHealthUpdated?.Invoke(currentHealth.Value, maxHealth);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
        isDead.OnValueChanged -= OnDeadStateChanged;
    }

    public void TakeDamage(float damage, ulong attackerClientId)
    {
        if (!IsServer) return;
        if (isDead.Value) return;

        var invuln = GetComponent<TemporaryInvulnerability>();
        if (invuln != null && invuln.IsInvulnerable) return;

        lastAttackerClientId.Value = attackerClientId;
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        PlayHitEffectClientRpc();

        if (currentHealth.Value <= 0) Die();
    }

    public void TakeDamage(float damage, uint attackerNetId)
    {
        TakeDamage(damage, (ulong)attackerNetId);
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc()
    {
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position);

        StartCoroutine(FlashRed());
    }

    private IEnumerator FlashRed()
    {
        if (meshRenderers == null || meshRenderers.Length == 0) yield break;

        Color[] orig = new Color[meshRenderers.Length];
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].material != null)
            {
                orig[i] = meshRenderers[i].material.color;
                meshRenderers[i].material.color = Color.red;
            }
        }
        yield return new WaitForSeconds(0.1f);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].material != null)
                meshRenderers[i].material.color = orig[i];
        }
    }

    private void Die()
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        isDead.Value = true;

        if (MirrorGameMaster.Instance != null)
        {
            MirrorGameMaster.Instance.AddKill((uint)lastAttackerClientId.Value, (uint)OwnerClientId);
        }

        OnDeathClientRpc();
    }

    [ClientRpc]
    private void OnDeathClientRpc()
    {
        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position);

        if (deathEffectPrefab != null)
            Destroy(Instantiate(deathEffectPrefab, transform.position, Quaternion.identity), 3f);

        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
                if (r != null) r.enabled = false;
        }

        DisableComponents();
        OnDeath?.Invoke();

        if (IsOwner)
        {
            var respawnUI = FindAnyObjectByType<RespawnUI>();
            if (respawnUI != null)
                respawnUI.ShowRespawnUI(respawnTime);
        }
    }

    private void DisableComponents()
    {
        if (kartController != null) kartController.DisableControls();
        if (componentsToDisable != null)
        {
            foreach (var c in componentsToDisable)
                if (c != null) c.enabled = false;
        }
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
    }

    public void Respawn(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        currentHealth.Value = maxHealth;
        isDead.Value = false;

        transform.SetPositionAndRotation(pos, rot);
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }

        OnRespawnClientRpc();

        var invuln = GetComponent<TemporaryInvulnerability>();
        if (invuln != null) invuln.StartInvulnerability();
    }

    [ClientRpc]
    private void OnRespawnClientRpc()
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
                if (r != null) r.enabled = true;
        }

        EnableComponents();
        OnRespawned?.Invoke();
    }

    private void EnableComponents()
    {
        if (kartController != null && IsOwner) kartController.EnableControls();
        if (componentsToDisable != null)
        {
            foreach (var c in componentsToDisable)
                if (c != null) c.enabled = true;
        }
    }

    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
    }

    private void OnHealthChanged(float oldVal, float newVal)
    {
        OnHealthUpdated?.Invoke(newVal, maxHealth);
    }

    private void OnDeadStateChanged(bool oldVal, bool newVal)
    {
        if (newVal) OnDeath?.Invoke();
        else OnRespawned?.Invoke();
    }
}
