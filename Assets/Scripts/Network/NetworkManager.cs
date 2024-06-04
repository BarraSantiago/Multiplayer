using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using UnityEditor;
using UnityEngine;

namespace Network
{
    public class NetworkManager : IReceiveData
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject bodyPrefab;
        [SerializeField] private Material playerMaterial;

        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        
        public static Action<byte[], IPEndPoint> OnReceiveEvent;
        public static Action<GameObject> OnPlayerSpawned;
        public static Action OnCountdownStart;
        public static Action<byte[], IPEndPoint> OnHandshake;
        public static Action<byte[], IPEndPoint> OnMovePlayers;
        public static Action<byte[]> OnBulletFired;
        public static Action<IPEndPoint> OnClose;

        private bool countdownStarted = false;
        private bool gameStarted = false;
        private DateTime countdownStartTime;
        private Handshake _handshake;
        public static UdpConnection Connection;

        private void Start()
        {
            _handshake = new Handshake();
            _handshake.bodyPrefab = bodyPrefab;
        }

        public void StartServer(int port)
        {
            this.Port = port;
            Connection = new UdpConnection(port, this);
        }

        public void StartClient(IPAddress ip, int port, string name)
        {
            this.Port = port;
            this.IPAddress = ip;

            GameObject body = Instantiate(bodyPrefab);
            body.GetComponent<MeshRenderer>().material = playerMaterial;
            thisPlayer = body.AddComponent<Player>();
            thisPlayer.name = name;
            OnPlayerSpawned?.Invoke(body);
            
            Connection = new UdpConnection(ip, port, this);
        }

        private void Update()
        {
            Connection?.FlushReceiveData();


        }

        private MessageType CheckMessageType(byte[] data)
        {
            return (MessageType)BitConverter.ToInt32(data);
        }

        public int CalculateChecksum(byte[] data)
        {
            return data.Aggregate(0, (current, b) => current + b);
        }

        public void OnReceiveData(byte[] dataWithChecksum, IPEndPoint ip)
        {
            byte[] data = CheckCorrupted(dataWithChecksum);

            if (data == null) return;

            MessageType messageType = CheckMessageType(data);

            switch (messageType)
            {
                case MessageType.HandShake:
                    OnHandshake?.Invoke(data, ip);
                    break;

                case MessageType.Console:
                    OnReceiveEvent?.Invoke(data, ip);
                    break;

                case MessageType.Position:
                    OnMovePlayers?.Invoke(data, ip);
                    break;

                case MessageType.Ping:
                    SendPing();
                    break;

                case MessageType.Pong:
                    SendPong(ip);
                    break;

                case MessageType.Close:
                    OnClose?.Invoke(ip);
                    break;

                case MessageType.Shoot:
                    OnBulletFired?.Invoke(data);
                    break;

                case MessageType.Rejected:
                    HandleRejected(data);
                    break;

                case MessageType.CountdownStarted:
                    OnCountdownStart?.Invoke();
                    break;

                case MessageType.GameStarted:
                    break;

                case MessageType.Winner:
                    NetWinner netWinner = new NetWinner();
                    string winnerName = netWinner.Deserialize(data);
                    OnWinner?.Invoke("Winner is " + winnerName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private byte[] CheckCorrupted(byte[] dataWithChecksum)
        {
            byte[] data = new byte[dataWithChecksum.Length - sizeof(int)];
            byte[] checksumBytes = new byte[sizeof(int)];

            Buffer.BlockCopy(dataWithChecksum, 0, data, 0, data.Length);
            Buffer.BlockCopy(dataWithChecksum, data.Length, checksumBytes, 0, sizeof(int));

            int receivedChecksum = BitConverter.ToInt32(checksumBytes, 0);
            int calculatedChecksum = CalculateChecksum(data);

            if (receivedChecksum == calculatedChecksum) return data;

            Debug.LogError("Data is corrupted");
            return null;
        }

        public void Disconnect()
        {
            Connection.Close();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}