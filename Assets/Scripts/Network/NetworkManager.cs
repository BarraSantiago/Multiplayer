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
    public struct Client
    {
        public DateTime timeStamp;
        public int id;
        public IPEndPoint ipEndPoint;

        public Client(IPEndPoint ipEndPoint, int id, DateTime timeStamp)
        {
            this.timeStamp = timeStamp;
            this.id = id;
            this.ipEndPoint = ipEndPoint;
        }
    }

    public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject bodyPrefab;
        [SerializeField] private Material playerMaterial;
        [SerializeField] public double countdownSeconds = 10;

        public readonly Dictionary<IPEndPoint, int> IPToId = new Dictionary<IPEndPoint, int>();
        public readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public Player thisPlayer;
        public double MS { get; private set; } = 0;
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        public bool IsServer { get; private set; }
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
            IsServer = true;
            this.Port = port;
            Connection = new UdpConnection(port, this);
        }

        public void StartClient(IPAddress ip, int port, string name)
        {
            IsServer = false;

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

        public void SendToServer(byte[] data)
        {
            Connection.Send(data);
        }

        public void Broadcast(byte[] data)
        {
            using var iterator = Clients.GetEnumerator();

            while (iterator.MoveNext())
            {
                Connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }

        private MessageType CheckMessageType(byte[] data)
        {
            return (MessageType)BitConverter.ToInt32(data);
        }

        public int CalculateChecksum(byte[] data)
        {
            int checksum = 0;
            foreach (byte b in data)
            {
                checksum += b;
            }

            return checksum;
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

        private void HandleRejected(byte[] data)
        {
            NetRejectClient netRejectClient = new NetRejectClient();
            ErrorType errorType = (ErrorType)netRejectClient.Deserialize(data);

            switch (errorType)
            {
                case ErrorType.None:
                    break;
                case ErrorType.NameInUse:
                    Reject("Name is already in use or is empty. Select a new one.");
                    break;
                case ErrorType.ServerFull:
                    Reject("Server is full.");
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

        public void Reject(string reason)
        {
            Connection.Close();
            Destroy(thisPlayer.gameObject);
            OnRejected?.Invoke(reason);
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

        private void HandleHandshake(byte[] data, IPEndPoint ip)
        {
            if (IsServer)
            {
                NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

                Player player = _handshake.ServerRecieveHandshake(data, ip);


                if (player) Players.Add(IPToId[ip], player);

                (int ID, string name, Vector3 pos)[] players = new (int ID, string name, Vector3 pos)[Players.Count];

                int i = 0;
                foreach (var value in Players)
                {
                    players[i++] = (value.Key, value.Value.name, value.Value.gameObject.transform.position);
                }

                netServerToClientHs.data = players;

                Broadcast(netServerToClientHs.Serialize());

                if (Players.Count < 2 || countdownStarted) return;

                countdownStarted = true;
                OnCountdownStart?.Invoke();
                countdownStartTime = DateTime.UtcNow;
                Broadcast(BitConverter.GetBytes((int)MessageType.CountdownStarted));
            }
            else
            {
                Dictionary<int, Player> newPlayers = _handshake.ClientRecieveHandshake(data);

                foreach (var player in Players.Where(player => newPlayers.Any(player2 => player2.Key == player.Key)))
                {
                    Players.Remove(newPlayers.First(player2 => player2.Key == player.Key).Key);
                }

                Dictionary<int, Player> playersToRemove = Players;

                foreach (var player in playersToRemove)
                {
                    RemovePlayer(player.Key);
                }

                Players = newPlayers;
            }
        }


        private void RemoveClient(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var id)) return;

            Connection.Send(BitConverter.GetBytes((int)MessageType.Close), ip);
            if (Players[IPToId[ip]] && Players[IPToId[ip]].gameObject) Destroy(Players[IPToId[ip]].gameObject);
            Clients.Remove(id);
            Players.Remove(IPToId[ip]);
            IPToId.Remove(ip);
        }

        private void RemovePlayer(int id)
        {
            if (Players[id] && Players[id].gameObject) Destroy(Players[id].gameObject);
            Players.Remove(id);
        }

        private void SendPing()
        {
            TimeSpan newDateTime = DateTime.UtcNow - _time;
            MS = (float)newDateTime.Milliseconds;

            _time = DateTime.UtcNow;

            SendToServer(BitConverter.GetBytes((int)MessageType.Pong));
        }

        private void SendPong(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var value)) return;

            var client = Clients[value];

            client.timeStamp = DateTime.UtcNow;

            Clients[IPToId[ip]] = client;

            Connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);
        }

        private void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVector3 netVector3 = new NetVector3(data);
            (Vector3 pos, int id) newData = netVector3.data;


            if (IsServer)
            {
                if (!IPToId.TryGetValue(ip, out var value)) return;

                Player player = Players[value];
                player.gameObject.transform.position = newData.pos;
                Players[IPToId[ip]] = player;
                Broadcast(data);
            }
            else
            {
                if (newData.id == thisPlayer.clientID) return;

                Players[newData.id].gameObject.transform.position = newData.pos;
            }
        }

        public void MovePlayer(Vec3 pos)
        {
            NetVector3 netVector3 = new NetVector3(pos.ToVector3(), thisPlayer.clientID);
            SendToServer(netVector3.Serialize());
        }

        public void FireBullet(Vec3 pos, Vec3 dire)
        {
            NetShoot netVector3 = new NetShoot(pos.ToVector3(), dire.ToVector3(), thisPlayer.clientID);
            SendToServer(netVector3.Serialize());
        }

        private void CheckPings()
        {
            if (Connection == null) return;

            if (IsServer)
            {
                bool clientRemoved = false;
                List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();

                foreach (var client in Clients.Values)
                {
                    DateTime timeOutTime = client.timeStamp;

                    if (timeOutTime.AddSeconds(timeOut) > DateTime.UtcNow && Players[client.id].hp > 0) continue;

                    clientsToRemove.Add(client.ipEndPoint);
                    clientRemoved = true;
                }

                foreach (var client in clientsToRemove)
                {
                    RemoveClient(client);
                }

                if (clientRemoved)
                {
                    _handshake.PrepareHandshake("");
                }
            }
            else
            {
                DateTime timeOutTime = _time.AddSeconds(timeOut);
                if (timeOutTime < DateTime.UtcNow)
                {
                    CheckDisconnect();
                }
            }
        }

        public void CheckDisconnect()
        {
            SendToServer(BitConverter.GetBytes((int)MessageType.Close));
            Disconnect();
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

        public void EndGame()
        {
            Player player = Players.Values.First();
            string winnerName = player.name;
            if (Players.Count > 0)
            {
                foreach (var player2 in Players.Values.Where(player2 => player2.hp > player.hp))
                {
                    winnerName = player2.name;
                }

                NetWinner netWinner = new NetWinner();
                netWinner.data = player.name;
                Broadcast(netWinner.Serialize());
            }

            if (winnerName != null) OnWinner?.Invoke("Winner is " + winnerName);

            StartCoroutine(CloseServer());

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator CloseServer()
        {
            yield return new WaitForSeconds(timeOut);

            List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();

            foreach (var client in Clients.Values)
            {
                clientsToRemove.Add(client.ipEndPoint);
            }

            foreach (var client in clientsToRemove)
            {
                RemoveClient(client);
            }
            Disconnect();
        }
    }
}