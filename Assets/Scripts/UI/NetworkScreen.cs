using System;
using System.Net;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    public class NetworkScreen : MonoBehaviour
    {
        [FormerlySerializedAs("NameUsedText")] public TMP_Text RejectedMessage;
        public Button connectBtn;
        public Button startServerBtn;
        public InputField portInputField;
        public InputField addressInputField;
        public InputField nameInputField;

        protected void Awake()
        {
            connectBtn.onClick.AddListener(OnConnectBtnClick);
            startServerBtn.onClick.AddListener(OnStartServerBtnClick);
            NetworkManager.Instance.OnRejected += OnNameInUse;
        }

        private void OnConnectBtnClick()
        {
            IPAddress ipAddress = IPAddress.Parse(addressInputField.text);
            int port = System.Convert.ToInt32(portInputField.text);
            string name = nameInputField.text;

            NetworkManager.Instance.StartClient(ipAddress, port, name);

            SwitchScreens();
        }

        private void OnStartServerBtnClick()
        {
            int port = System.Convert.ToInt32(portInputField.text);

            NetworkManager.Instance.StartServer(port);
            SwitchScreens();
        }

        private void SwitchScreens()
        {
            ChatScreen.Instance.gameObject.SetActive(true);
            this.gameObject.SetActive(false);
        }

        private void OnNameInUse(String message)
        {
            RejectedMessage.gameObject.SetActive(true);
            RejectedMessage.text = message;
            ChatScreen.Instance.gameObject.SetActive(false);
            this.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            NetworkManager.Instance.OnRejected -= OnNameInUse;
        }
    }
}