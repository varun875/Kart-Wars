using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Helper utility that ensures NetworkManager and UnityTransport exist and are configured
/// at runtime without requiring manual scene modifications.
/// </summary>
public static class NetworkManagerHelper
{
    private static NetworkManager _cachedManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureTransport();
    }

    /// <summary>
    /// Ensures that NetworkManager and its UnityTransport exist.
    /// If none is present, creates a persistent [NetworkManager] GameObject with UnityTransport.
    /// Returns the active UnityTransport instance.
    /// </summary>
    public static UnityTransport EnsureTransport()
    {
        NetworkManager nm = EnsureNetworkManager();
        if (nm == null) return null;

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<UnityTransport>();
        }

        if (nm.NetworkConfig == null)
        {
            nm.NetworkConfig = new NetworkConfig();
        }

        if (nm.NetworkConfig.NetworkTransport == null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
        }

        return transport;
    }

    /// <summary>
    /// Ensures NetworkManager.Singleton exists.
    /// </summary>
    public static NetworkManager EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            _cachedManager = NetworkManager.Singleton;
            ConfigureExistingManager(_cachedManager);
            return _cachedManager;
        }

        var existing = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
        if (existing != null)
        {
            _cachedManager = existing;
            ConfigureExistingManager(_cachedManager);
            return _cachedManager;
        }

        // Dynamically instantiate runtime NetworkManager
        GameObject nmGo = new GameObject("[NetworkManager]");
        UnityEngine.Object.DontDestroyOnLoad(nmGo);

        var utp = nmGo.AddComponent<UnityTransport>();
        var nm = nmGo.AddComponent<NetworkManager>();

        var config = new NetworkConfig
        {
            NetworkTransport = utp,
            ProtocolVersion = 0,
            RpcHashSize = HashSize.VarIntFourBytes,
            EnableNetworkLogs = true
        };

        RegisterPrefab(config, "Player Kart", "Assets/Prefabs/Player Kart.prefab");
        RegisterPrefab(config, "Pickup", "Assets/Prefabs/Pickup.prefab");
        RegisterPrefab(config, "Mine", "Assets/Prefabs/Mine.prefab");
        RegisterPrefab(config, "Boom", "Assets/Prefabs/Boom.prefab");

        nm.NetworkConfig = config;
        _cachedManager = nm;

        Debug.Log("[NetworkManagerHelper] Initialized runtime NetworkManager with UnityTransport.");
        return nm;
    }

    private static void ConfigureExistingManager(NetworkManager nm)
    {
        if (nm == null) return;

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<UnityTransport>();
        }

        if (nm.NetworkConfig == null)
        {
            nm.NetworkConfig = new NetworkConfig();
        }

        if (nm.NetworkConfig.NetworkTransport == null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
        }

        RegisterPrefab(nm.NetworkConfig, "Player Kart", "Assets/Prefabs/Player Kart.prefab");
        RegisterPrefab(nm.NetworkConfig, "Pickup", "Assets/Prefabs/Pickup.prefab");
        RegisterPrefab(nm.NetworkConfig, "Mine", "Assets/Prefabs/Mine.prefab");
        RegisterPrefab(nm.NetworkConfig, "Boom", "Assets/Prefabs/Boom.prefab");
    }

    private static void RegisterPrefab(NetworkConfig config, string resourceName, string assetPath)
    {
        if (config == null || config.Prefabs == null) return;

        GameObject prefab = Resources.Load<GameObject>(resourceName);
#if UNITY_EDITOR
        if (prefab == null && !string.IsNullOrEmpty(assetPath))
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }
#endif
        if (prefab != null)
        {
            bool alreadyRegistered = false;
            foreach (var existing in config.Prefabs.Prefabs)
            {
                if (existing.Prefab == prefab)
                {
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
            {
                config.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }

            if (resourceName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                config.PlayerPrefab = prefab;
            }
        }
    }
}
