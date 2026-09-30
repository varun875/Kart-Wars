using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [Header("Settings")]
    public bool showOnRight = false;
    public int fontSize = 24;
    public Color fpsColor = Color.green;
    public Color latencyColor = Color.yellow;

    private float deltaTime = 0.0f;
    private bool showFPS = false;
    private bool showLatency = false;
    private float latencyMs = 0f;
    private GUIStyle fpsStyle;
    private GUIStyle latencyStyle;

    public void SetFPSVisible(bool visible) => showFPS = visible;
    public void SetLatencyVisible(bool visible) => showLatency = visible;

    // Call this from your network manager to update latency
    public void UpdateLatency(float ms) => latencyMs = ms;

    void Update()
    {
        if (showFPS)
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        if (!showFPS && !showLatency) return;

        if (fpsStyle == null)
        {
            fpsStyle = new GUIStyle();
            fpsStyle.fontSize = fontSize;
            fpsStyle.normal.textColor = fpsColor;
            fpsStyle.fontStyle = FontStyle.Bold;
        }

        if (latencyStyle == null)
        {
            latencyStyle = new GUIStyle();
            latencyStyle.fontSize = fontSize;
            latencyStyle.normal.textColor = latencyColor;
            latencyStyle.fontStyle = FontStyle.Bold;
        }

        float width = 160;
        float height = 40;
        float xPos = showOnRight ? Screen.width - width - 10 : 10;

        if (showFPS)
        {
            float fps = 1.0f / deltaTime;
            GUI.Label(new Rect(xPos, 10, width, height),
                string.Format("FPS: {0:0.}", fps), fpsStyle);
        }

        if (showLatency)
        {
            float yOffset = showFPS ? 40 : 10; // stack below FPS if both on
            string latencyText = latencyMs < 0
                ? "Ping: --"
                : string.Format("Ping: {0:0}ms", latencyMs);

            // Color-code latency: green < 80ms, yellow < 150ms, red >= 150ms
            latencyStyle.normal.textColor = latencyMs < 80 ? Color.green
                                          : latencyMs < 150 ? Color.yellow
                                          : Color.red;

            GUI.Label(new Rect(xPos, yOffset, width, height), latencyText, latencyStyle);
        }
    }
}