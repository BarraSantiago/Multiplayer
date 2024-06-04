using System;
using System.Net;
using Network;
using UnityEngine.UI;

namespace UI
{
    public class ChatScreen : MonoBehaviourSingleton<ChatScreen>
    {
        public Text messages;
        public Text ms;
        public InputField inputMessage;

        protected override void Initialize()
        {
            inputMessage.onEndEdit.AddListener(OnEndEdit);

            this.gameObject.SetActive(false);

            NetworkManager.OnReceiveEvent += OnReceiveDataEvent;
        }

        private void Update()
        {
            ms.text = "MS: " + NetworkManager.Instance.MS.ToString("");
        }

        private void OnReceiveDataEvent(byte[] data, IPEndPoint ep)
        {
            NetConsole console = new NetConsole();

            if (NetworkManager.Instance.IsServer)
            {
                NetworkManager.Instance.Broadcast(data);
            }

            messages.text += console.Deserialize(data) + System.Environment.NewLine;
        }

        private void OnEndEdit(string str)
        {
            if (inputMessage.text == "") return;


            int numberToAdd = (int)MessageType.Console;
            byte[] numberToAddBytes = BitConverter.GetBytes(numberToAdd);
            byte[] messageBytes =
                System.Text.Encoding.UTF8.GetBytes(NetworkManager.Instance.thisPlayer.name + ": " + str);

            byte[] combinedBytes = new byte[numberToAddBytes.Length + messageBytes.Length];
            Buffer.BlockCopy(numberToAddBytes, 0, combinedBytes, 0, numberToAddBytes.Length);
            Buffer.BlockCopy(messageBytes, 0, combinedBytes, numberToAddBytes.Length, messageBytes.Length);

            NetworkManager.Connection.Send(combinedBytes);

            inputMessage.ActivateInputField();
            inputMessage.Select();
            inputMessage.text = "";
        }
    }
}