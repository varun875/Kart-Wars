using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Networked player health, death state, respawn flow, component disabling.
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

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead = false;

    [SyncVar]
    private uint lastAttackerNetId;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnHealthUpdated?.Invoke(currentHealth, maxHealth);
    }

    [Server]
    public void TakeDamage(float damage, uint attackerNetId)
    {
        if (isDead) return;

        var invuln = GetComponent<TemporaryInvulnerability>();
        if (invuln != null && invuln.IsInvulnerable) return;

        lastAttackerNetId = attackerNetId;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        RpcPlayHitEffect();

        if (currentHealth <= 0) Die();
    }

    [ClientRpc]
    private void RpcPlayHitEffect()
    {
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position);

        StartCoroutine(FlashRed());
    }

    private IEnumerator FlashRed()
    {
        Color[] orig = new Color[meshRenderers.Length];
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null)
            {
                orig[i] = meshRenderers[i].material.color;
                meshRenderers[i].material.color = Color.red;
            }
        }
        yield return new WaitForSeconds(0.1f);
        for (int i = 0; i < meshRenderers.Length; i++)
            if (meshRenderers[i] != null)
                meshRenderers[i].material.color = orig[i];
    }

    [Server]
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (MirrorGameMaster.Instance != null)
        {
            MirrorGameMaster.Instance.AddKill(lastAttackerNetId, netId);
        }

        RpcOnDeath();
    }

    [ClientRpc]
    private void RpcOnDeath()
    {
        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position);

        if (deathEffectPrefab != null)
            Destroy(Instantiate(deathEffectPrefab, transform.position, Quaternion.identity), 3f);

        foreach (var r in meshRenderers)
            if (r != null) r.enabled = false;

        DisableComponents();
        OnDeath?.Invoke();

        if (isLocalPlayer)
        {
            var respawnUI = FindAnyObjectByType<RespawnUI>();
            if (respawnUI != null)
                respawnUI.ShowRespawnUI(respawnTime);
        }
    }

    private void DisableComponents()
    {
        if (kartController != null) kartController.DisableControls();
        foreach (var c in componentsToDisable)
            if (c != null) c.enabled = false;
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
    }

    [Server]
    public void Respawn(Vector3 pos, Quaternion rot)
    {
        currentHealth = maxHealth;
        isDead = false;

        transform.SetPositionAndRotation(pos, rot);
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }

        RpcOnRespawn();

        var invuln = GetComponent<TemporaryInvulnerability>();
        if (invuln != null) invuln.StartInvulnerability();
    }

    [ClientRpc]
    private void RpcOnRespawn()
    {
        foreach (var r in meshRenderers)
            if (r != null) r.enabled = true;

        EnableComponents();
        OnRespawned?.Invoke();
    }

    private void EnableComponents()
    {
        if (kartController != null) kartController.EnableControls();
        foreach (var c in componentsToDisable)
            if (c != null) c.enabled = true;
    }

    [Server]
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
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
