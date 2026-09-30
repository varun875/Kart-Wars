using UnityEngine;
using Unity.Netcode;

public class NetworkLatencyTracker : MonoBehaviour
{
    public FPSDisplay fpsDisplay;
    public float updateInterval = 1f;

    private float timer = 0f;

    void Update()
    {
        // Check if connected via NGO
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
        {
            fpsDisplay.UpdateLatency(-1f); // shows "Ping: --" when offline
            return;
        }

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;

            // NGO RTT via NetworkManager
            float latencyMs = (float)(NetworkManager.Singleton.NetworkConfig
                .NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId));

            fpsDisplay.UpdateLatency(latencyMs);
        }
    }
}