using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using UnityEngine;
using Utils;

namespace Network
{
    public class Server
    {
        [SerializeField] public double countdownSeconds = 10;

        public Action<string> OnWinner;
        public readonly Dictionary<IPEndPoint, int> IPToId = new Dictionary<IPEndPoint, int>();
        public readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public int timeOut = 10;
        private Handshake _handshake;
        private bool countdownStarted;
        private DateTime countdownStartTime;
        public void Initialize()
        {
            NetworkManager.OnClose += RemoveClient;
            NetworkManager.OnHandshake += HandleHandshake;
            NetworkManager.OnMovePlayers += MovePlayers;
            NetworkManager.OnBulletFired += HandleBullets;
            
        }
        
        private void RemoveClient(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var id)) return;

            NetworkManager.Connection.Send(BitConverter.GetBytes((int)MessageType.Close), ip);
            Clients.Remove(id);
            Players.Remove(IPToId[ip]);
            IPToId.Remove(ip);
        }


        public void Broadcast(byte[] data)
        {
            using var iterator = Clients.GetEnumerator();

            while (iterator.MoveNext())
            {
                NetworkManager.Connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }

        private void CheckPings()
        {
            if (NetworkManager.Connection == null) return;


            bool clientRemoved = false;
            List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();

            foreach (var client in from client in Clients.Values let timeOutTime = client.timeStamp where timeOutTime.AddSeconds(timeOut) <= DateTime.UtcNow || Players[client.id].hp <= 0 select client)
            {
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


        public void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVec3 netVector3 = new NetVec3(data);
            (Vec3 pos, int id) newData = netVector3.data;


            if (!IPToId.TryGetValue(ip, out var value)) return;

            Player player = Players[value];
            player.gameObject.transform.position = newData.pos.ToVector3();
            Players[IPToId[ip]] = player;
            Broadcast(data);
        }

        public void SendPong(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var value)) return;

            var client = Clients[value];

            client.timeStamp = DateTime.UtcNow;

            Clients[IPToId[ip]] = client;

            NetworkManager.Connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);
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

            CloseServer();
        }

        private void HandleHandshake(byte[] data, IPEndPoint ip)
        {
            NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

            Player player = _handshake.ServerRecieveHandshake(data, ip);


            if (player) Players.Add(IPToId[ip], player);

            (int ID, string name, Vec3 pos)[] players = new (int ID, string name, Vec3 pos)[Players.Count];

            int i = 0;
            foreach (var value in Players)
            {
                players[i++] = (value.Key, value.Value.name,
                    Vec3.FromVector3(value.Value.gameObject.transform.position));
            }

            netServerToClientHs.data = players;

            Broadcast(netServerToClientHs.Serialize());

            if (Players.Count < 2 || countdownStarted) return;

            countdownStarted = true;
            NetworkManager.OnCountdownStart?.Invoke();
            countdownStartTime = DateTime.UtcNow;
            Broadcast(BitConverter.GetBytes((int)MessageType.CountdownStarted));
        }

        private void HandleBullets(byte[] data)
        {
            NetShoot netShoot = new NetShoot(data);
            
            // TODO add id to the bullet
            
            Broadcast(data);
        }

        private void CloseServer()
        {
            List<IPEndPoint> clientsToRemove = Clients.Values.Select(client => client.ipEndPoint).ToList();

            foreach (var client in clientsToRemove)
            {
                RemoveClient(client);
            }

            Disconnect();
        }
        
        private void StartGame()
        {
            if (!countdownStarted ||
                !((DateTime.UtcNow - countdownStartTime).TotalMinutes >= (countdownSeconds / 60))) return;
            Broadcast(BitConverter.GetBytes((int)MessageType.GameStarted));
        }

        private void RemovePlayer(int id)
        {
            Players.Remove(id);
        }

        public void Disconnect()
        {
            NetworkManager.Connection.Close();
            Application.Quit();
        }
    }
}