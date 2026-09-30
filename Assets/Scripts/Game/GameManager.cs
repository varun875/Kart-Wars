using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

/// <summary>
/// Manages initialization of Unity Services and authentication.
/// Signs in anonymously on start and handles service ready state.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Fired when Unity Services are fully initialized and player is authenticated.
    /// </summary>
    public static event Action OnServicesReady;

    private static GameManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    /// <summary>
    /// Initializes Unity Services and authenticates anonymously.
    /// </summary>
    private async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        try
        {
            if (debugLogs) Debug.Log("[GameManager] Starting services initialization...");

            // Initialize Unity Services
            await UnityServices.InitializeAsync();

            // Check if already signed in
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                if (debugLogs) Debug.Log("[GameManager] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (debugLogs) Debug.Log($"[GameManager] Successfully authenticated as {AuthenticationService.Instance.PlayerId}");

            // Signal that services are ready
            OnServicesReady?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] Failed to initialize services: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Gets the current authenticated player ID.
    /// </summary>
    /// <returns>The player's unique ID, or empty string if not authenticated.</returns>
    public static string GetPlayerId()
    {
        return AuthenticationService.Instance.IsSignedIn 
            ? AuthenticationService.Instance.PlayerId 
            : string.Empty;
    }

    /// <summary>
    /// Checks if the player is currently authenticated.
    /// </summary>
    public static bool IsAuthenticated()
    {
        return AuthenticationService.Instance.IsSignedIn;
    }
}
