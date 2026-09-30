using UnityEngine;
using UnityEngine.UI;
using Mirror;
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
    private static Dictionary<string, string> roomCodeToIP = new Dictionary<string, string>();

    private const string CHARACTERS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public void GenerateCode()
    {
        char[] code = new char[codeLength];
        for (int i = 0; i < codeLength; i++)
            code[i] = CHARACTERS[Random.Range(0, CHARACTERS.Length)];

        currentRoomCode = new string(code);
        roomCodeDisplay.text = "Room Code: " + currentRoomCode;
        Debug.Log("Generated Code: " + currentRoomCode);
    }

    public void StartHost()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            roomCodeDisplay.text = "Generate a code first!";
            return;
        }

        string localIP = GetLocalIP();
        roomCodeToIP[currentRoomCode] = localIP;

        NetworkManager.singleton.networkAddress = localIP;
        NetworkManager.singleton.StartHost();

        roomCodeDisplay.text = "Hosting: " + currentRoomCode;
        Debug.Log($"Hosting with code: {currentRoomCode} on IP: {localIP}");
    }

    public void CopyCodeToClipboard()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            roomCodeDisplay.text = "No code to copy!";
            return;
        }

        GUIUtility.systemCopyBuffer = currentRoomCode;
        roomCodeDisplay.text = "Copied: " + currentRoomCode;
    }

    private string GetLocalIP()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }
}