using UnityEngine;
using UnityEngine.SceneManagement; // This is required to change scenes

public class MenuManager : MonoBehaviour
{
    // This function will be called when the "Host" button is clicked
    public void OnHostClicked()
    {
        // Replace "HostScene" with the actual name of your scene
        SceneManager.LoadScene("HostScene");
    }

    // This function will be called when the "Join" button is clicked
    public void OnJoinClicked()
    {
        // Replace "JoinScene" with the actual name of your scene
        SceneManager.LoadScene("JoinScene");
    }
}