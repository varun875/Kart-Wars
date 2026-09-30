using UnityEngine;
using UnityEngine.SceneManagement;

public class HostAndJoin : MonoBehaviour
{
    private const string HOST_SCENE = "HostScene";
    private const string JOIN_SCENE = "JoinScene";

    public void OnHostPressed()
    {
        LogDebug("Host button pressed — loading HostScene");
        SceneManager.LoadScene(HOST_SCENE);
    }

    public void OnJoinPressed()
    {
        LogDebug("Join button pressed — loading JoinScene");
        SceneManager.LoadScene(JOIN_SCENE);
    }

    public void OnBackPressed()
    {
        LogDebug("Back button pressed — returning to Main Menu");
        SceneManager.LoadScene("Main Menu");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogDebug(string message)
    {
        Debug.Log($"[HostAndJoin] {message}");
    }
}