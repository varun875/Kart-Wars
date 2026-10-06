using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RoomCodeGen : MonoBehaviour
{
    [Header("UI References")]
    public Text roomCodeDisplay;
    public Button generateCodeButton;
    public Button hostButton;
    public Button copyCodeButton;

    [Header("Settings")]
    public int codeLength = 6;

    private string currentRoomCode;

    private const string CHARACTERS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public void GenerateCode()
    {
        char[] code = new char[codeLength];
        for (int i = 0; i < codeLength; i++)
            code[i] = CHARACTERS[Random.Range(0, CHARACTERS.Length)];

        currentRoomCode = new string(code);
        if (roomCodeDisplay != null)
            roomCodeDisplay.text = "Room Code: " + currentRoomCode;
        Debug.Log("Generated Code: " + currentRoomCode);
    }

    public void StartHost()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            if (roomCodeDisplay != null)
                roomCodeDisplay.text = "Generate a code first!";
            return;
        }

        if (HostManager.Instance != null)
        {
            HostManager.Instance.StartHostAndLoadGameScene();
        }
    }

    public void CopyCodeToClipboard()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            if (roomCodeDisplay != null)
                roomCodeDisplay.text = "No code to copy!";
            return;
        }

        GUIUtility.systemCopyBuffer = currentRoomCode;
        if (roomCodeDisplay != null)
            roomCodeDisplay.text = "Copied: " + currentRoomCode;
    }
}