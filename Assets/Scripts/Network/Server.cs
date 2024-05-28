using System;
using System.Collections;
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
        public readonly Dictionary<IPEndPoint, int> IPToId = new Dictionary<IPEndPoint, int>();
        public readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public int timeOut = 10;
        private Handshake _handshake;

        private void RemoveClient(IPEndPoint ip)
        {
            if (!IPToId.TryGetValue(ip, out var id)) return;

            NetworkManager.Instance.Connection.Send(BitConverter.GetBytes((int)MessageType.Close), ip);
            if (Players[IPToId[ip]] && Players[IPToId[ip]].gameObject) Destroy(Players[IPToId[ip]].gameObject);
            Clients.Remove(id);
            Players.Remove(IPToId[ip]);
            IPToId.Remove(ip);
        }


        public void Broadcast(byte[] data)
        {
            using var iterator = Clients.GetEnumerator();

            while (iterator.MoveNext())
            {
                NetworkManager.Instance.Connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }

        private void CheckPings()
        {
            if (NetworkManager.Instance.Connection == null) return;


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


        public void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVector3 netVector3 = new NetVector3(data);
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

            NetworkManager.Instance.Connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);
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
            OnCountdownStart?.Invoke();
            countdownStartTime = DateTime.UtcNow;
            Broadcast(BitConverter.GetBytes((int)MessageType.CountdownStarted));
        }

        private void HandleBullets(byte[] data)
        {
            NetShoot netShoot = new NetShoot(data);
            (Vec3 pos, Vec3 target, int id) newData = netShoot.data;

            Bullet bullet = Instantiate(bulletPrefab, newData.pos, Quaternion.identity).AddComponent<Bullet>();
            bullet.SetTarget(newData.target);
            bullet.clientID = newData.id;
            Broadcast(data);
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

        private void RemovePlayer(int id)
        {
            if (Players[id] && Players[id].gameObject) Destroy(Players[id].gameObject);
            Players.Remove(id);
        }

        public void Disconnect()
        {
            NetworkManager.Instance.Connection.Close();
            Application.Quit();
        }
    }
}