using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Network
{
    public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject bodyPrefab;
        [SerializeField] private Material playerMaterial;
        [SerializeField] public double countdownSeconds = 10;

        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public Player thisPlayer;
        public double MS { get; private set; } = 0;
        public IPAddress IPAddress { get; private set; }

        public int Port { get; private set; }

        //public bool IsServer { get; private set; }
        public Server server;
        public int MaxPlayers { get; set; } = 4;

        public int timeOut = 10;

        public Action<byte[], IPEndPoint> OnReceiveEvent;
        public Action<GameObject> OnPlayerSpawned;
        public Action<string> OnRejected;
        public Action<string> OnWinner;
        public Action OnGameStart;
        public Action OnCountdownStart;

        private bool countdownStarted = false;
        private bool gameStarted = false;
        private DateTime countdownStartTime;
        private Handshake _handshake;
        private DateTime _time;
        public UdpConnection Connection;

        private void Start()
        {
            _handshake = gameObject.AddComponent<Handshake>();
            _handshake.bodyPrefab = bodyPrefab;
        }

        public void StartServer(int port)
        {
            server = new Server();

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
            _time = DateTime.UtcNow;

            SendToServer(_handshake.PrepareHandshake(name));
            SendPing();
        }

        private void Update()
        {
            Connection?.FlushReceiveData();

            CheckPings();

            if (gameStarted)
            {
                if (Players.Count == 1)
                {
                    EndGame();
                }

                return;
            }

            if (!countdownStarted ||
                !((DateTime.UtcNow - countdownStartTime).TotalMinutes >= (countdownSeconds / 60))) return;
            gameStarted = true;
            Broadcast(BitConverter.GetBytes((int)MessageType.GameStarted));
            OnGameStart?.Invoke();
        }

        private void OnDestroy()
        {
            CheckDisconnect();
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
                    HandleHandshake(data, ip);
                    break;

                case MessageType.Console:
                    OnReceiveEvent?.Invoke(data, ip);
                    break;

                case MessageType.Position:
                    MovePlayers(data, ip);
                    break;

                case MessageType.Ping:
                    SendPing();
                    break;

                case MessageType.Pong:
                    SendPong(ip);
                    break;

                case MessageType.Close:
                    if (IsServer)
                    {
                        RemoveClient(ip);
                        HandleHandshake(null, null);
                    }
                    else
                    {
                        Disconnect();
                    }

                    break;

                case MessageType.Shoot:
                    HandleBullets(data);
                    break;

                case MessageType.Rejected:
                    HandleRejected(data);
                    break;

                case MessageType.CountdownStarted:
                    OnCountdownStart?.Invoke();
                    break;

                case MessageType.GameStarted:
                    OnGameStart?.Invoke();
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


        private void HandleBullets(byte[] data)
        {
            NetShoot netShoot = new NetShoot(data);
            (Vector3 pos, Vector3 target, int id) newData = netShoot.data;

            if (IsServer)
            {
                Bullet bullet = Instantiate(bulletPrefab, newData.pos, Quaternion.identity).AddComponent<Bullet>();
                bullet.SetTarget(newData.target);
                bullet.clientID = newData.id;
                Broadcast(data);
            }
            else
            {
                if (newData.id == thisPlayer.clientID) return;

                Bullet bullet = Instantiate(bulletPrefab, newData.pos, Quaternion.identity).AddComponent<Bullet>();
                bullet.SetTarget(newData.target);
                bullet.clientID = newData.id;
            }
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