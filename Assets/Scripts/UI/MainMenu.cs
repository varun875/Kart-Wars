using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private const string PLAY_SCENE = "HostScene";
    private const string SETTINGS_SCENE = "Settings";

    public void OnPlayPressed()
    {
        LogDebug("Play button pressed — loading HostScene");
        SceneManager.LoadScene(PLAY_SCENE);
    }

    public void OnSettingsPressed()
    {
        LogDebug("Settings button pressed — loading Settings");
        SceneManager.LoadScene(SETTINGS_SCENE);
    }

    public void OnQuitPressed()
    {
        LogDebug("Quit button pressed");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogDebug(string message)
    {
        Debug.Log($"[MainMenuManager] {message}");
    }
}