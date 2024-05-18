using UnityEngine.UI;
using System.Net;
using UnityEngine;

public class NetworkScreen : MonoBehaviour
{
    public Button connectBtn;
    public Button startServerBtn;
    public InputField portInputField;
    public InputField addressInputField;
    public InputField nameInputField;

    protected void Awake()
    {
        connectBtn.onClick.AddListener(OnConnectBtnClick);
        startServerBtn.onClick.AddListener(OnStartServerBtnClick);
    }

    void OnConnectBtnClick()
    {
        IPAddress ipAddress = IPAddress.Parse(addressInputField.text);
        int port = System.Convert.ToInt32(portInputField.text);
        string name = nameInputField.text;

        NetworkManager.Instance.StartClient(ipAddress, port, name);
        
        SwitchToChatScreen();
    }

    void OnStartServerBtnClick()
    {
        int port = System.Convert.ToInt32(portInputField.text);
        string name = nameInputField.text;

        NetworkManager.Instance.StartServer(port, name);
        SwitchToChatScreen();
    }

    void SwitchToChatScreen()
    {
        ChatScreen.Instance.gameObject.SetActive(true);
        this.gameObject.SetActive(false);
    }
}
