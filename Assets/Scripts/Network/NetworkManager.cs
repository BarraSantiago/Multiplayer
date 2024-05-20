using System;
using System.Collections.Generic;
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
    
        public readonly Dictionary<IPEndPoint, int> IPToId = new Dictionary<IPEndPoint, int>();
        public readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public Player thisPlayer;
        public double MS { get; private set; } = 0;
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        public bool IsServer { get; private set; }
        public int timeOut = 10;
        public Action<byte[], IPEndPoint> OnReceiveEvent;


        private Handshake _handshake = new Handshake();
        private DateTime _time;
        private UdpConnection _connection;

        private void Start()
        {
            _handshake.bodyPrefab = bodyPrefab;
            _handshake.playerMaterial = playerMaterial;
        }

        public void StartServer(int port)
        {
            IsServer = true;
            this.Port = port;
            _connection = new UdpConnection(port, this);
        }

        public void StartClient(IPAddress ip, int port, string name)
        {
            IsServer = false;

            this.Port = port;
            this.IPAddress = ip;
            thisPlayer.name = name;
            _connection = new UdpConnection(ip, port, this);
            _time = DateTime.UtcNow;
        
            SendToServer(_handshake.PrepareHandshake(name));
            SendPing();
        }

        private void Update()
        {
            // Flush the data in main thread
            if (_connection != null)
                _connection.FlushReceiveData();

            CheckPings();
        }

        public void SendToServer(byte[] data)
        {
            _connection.Send(data);
        }

        public void Broadcast(byte[] data)
        {
            using var iterator = Clients.GetEnumerator();

            while (iterator.MoveNext())
            {
                _connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }

        private MessageType CheckMessageType(byte[] data)
        {
            return (MessageType)BitConverter.ToInt32(data);
        }

        public void OnReceiveData(byte[] data, IPEndPoint ip)
        {
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
                    break;

                case MessageType.Dispose:
                    break;

                case MessageType.Shoot:
                    HandleBullets(data);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
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
                if(newData.id == thisPlayer.clientID) return;
            
                Bullet bullet = Instantiate(bulletPrefab, newData.pos, Quaternion.identity).GetComponent<Bullet>();
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

                Players.Add(IPToId[ip], player);

                (int ID, string name)[] players = new (int ID, string name)[Players.Count];

                foreach (var value in Players)
                {
                    players[value.Key] = (value.Key, value.Value.name);
                }

                netServerToClientHs.data = players;

                Broadcast(netServerToClientHs.Serialize());
            }
            else
            {
                Players = _handshake.ClientRecieveHandshake(data);
            }
        }


        private void RemoveClient(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var id)) return;

            Clients.Remove(id);
            Destroy(Players[IPToId[ip]].body);
            Players.Remove(IPToId[ip]);
        }

        /// <summary>
        /// Client updates time and sends ping
        /// </summary>
        private void SendPing()
        {
            _time = DateTime.UtcNow;

            SendToServer(BitConverter.GetBytes((int)MessageType.Pong));

            MS = DateTime.UtcNow.Ticks - MS;
        }

        /// <summary>
        /// Server updates player time and sends pong
        /// </summary>
        /// <param name="ip"> Client to send pong to </param>
        private void SendPong(IPEndPoint ip)
        {
            var client = Clients[IPToId[ip]];

            client.timeStamp = DateTime.UtcNow;

            Clients[IPToId[ip]] = client;

            _connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);

            long ticks = DateTime.UtcNow.Ticks - (long)MS;

            // Convert ticks to milliseconds
            MS = Math.Round(TimeSpan.FromTicks(ticks).TotalMilliseconds, 2);
        }

        private void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVector3 netVector3 = new NetVector3(data);
            (Vector3 pos, int id) newData = netVector3.data;
        

            if (IsServer)
            {
                Player player = Players[IPToId[ip]];
                player.body.transform.position = newData.pos;
                Players[IPToId[ip]] = player;
                Broadcast(data);
            }
            else
            {
                if (newData.id == thisPlayer.clientID) return;

                Players[newData.id].body.transform.position = newData.pos;
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
            if (_connection == null) return;

            if (IsServer)
            {
                bool clientRemoved = false;
                List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();
                foreach (var client in Clients.Values)
                {
                    DateTime timeOutTime = client.timeStamp;

                    if (timeOutTime.AddSeconds(timeOut) > DateTime.UtcNow) continue;

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
                    Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            _connection.Send(BitConverter.GetBytes((int)MessageType.Close));
            _connection.Close();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}