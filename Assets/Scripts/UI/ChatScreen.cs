using System;
using System.Net;
using UnityEngine.UI;

public class ChatScreen : MonoBehaviourSingleton<ChatScreen>
{
    public Text messages;
    public InputField inputMessage;

    protected override void Initialize()
    {
        inputMessage.onEndEdit.AddListener(OnEndEdit);

        this.gameObject.SetActive(false);

        NetworkManager.Instance.OnReceiveEvent += OnReceiveDataEvent;
    }

    void OnReceiveDataEvent(byte[] data, IPEndPoint ep)
    {
        NetConsole console = new NetConsole();

        if (NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.Broadcast(data);
        }

        messages.text += console.Deserialize(data) + System.Environment.NewLine;
    }

    void OnEndEdit(string str)
    {
        if (inputMessage.text != "")
        {
            if (NetworkManager.Instance.isServer)
            {
                int numberToAdd = (int)MessageType.Console;
                byte[] numberToAddBytes = BitConverter.GetBytes(numberToAdd);
                byte[] messageBytes = System.Text.ASCIIEncoding.UTF8.GetBytes(NetworkManager.thisPlayer.name + ": "+ str);

                byte[] combinedBytes = new byte[numberToAddBytes.Length + messageBytes.Length];
                Buffer.BlockCopy(numberToAddBytes, 0, combinedBytes, 0, numberToAddBytes.Length);
                Buffer.BlockCopy(messageBytes, 0, combinedBytes, numberToAddBytes.Length, messageBytes.Length);

                NetworkManager.Instance.Broadcast(combinedBytes);
                
                messages.text += str + System.Environment.NewLine;
            }
            else
            {
                int numberToAdd = (int)MessageType.Console;
                byte[] numberToAddBytes = BitConverter.GetBytes(numberToAdd);
                byte[] messageBytes = System.Text.ASCIIEncoding.UTF8.GetBytes( NetworkManager.thisPlayer.name + ": "+ str);

                byte[] combinedBytes = new byte[numberToAddBytes.Length + messageBytes.Length];
                Buffer.BlockCopy(numberToAddBytes, 0, combinedBytes, 0, numberToAddBytes.Length);
                Buffer.BlockCopy(messageBytes, 0, combinedBytes, numberToAddBytes.Length, messageBytes.Length);

                NetworkManager.Instance.SendToServer(combinedBytes);
            }

            inputMessage.ActivateInputField();
            inputMessage.Select();
            inputMessage.text = "";
        }
    }
}