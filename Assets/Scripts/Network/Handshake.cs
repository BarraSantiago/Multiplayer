using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using UnityEngine;
using Utils;

namespace Network
{
    public class Handshake : MonoBehaviour
    {
        public static Action<GameObject> onPlayerSpawned;
        public GameObject bodyPrefab;
        private int _clientId = 0;
        
        private void AddClient(IPEndPoint ip)
        {
            if (NetworkManager.Instance.IPToId.ContainsKey(ip)) return;

            int id = _clientId;
            NetworkManager.Instance.IPToId[ip] = _clientId;

            NetworkManager.Instance.Clients.Add(_clientId, new Client(ip, id, DateTime.UtcNow));

            _clientId++;
        }

        public Player ServerRecieveHandshake(byte[] data, IPEndPoint ip)
        {
            AddClient(ip);

            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();

            string newName = netClientToServerHs.Deserialize(data);
            Player player = new Player();

            player.name = newName;
            player.clientID = NetworkManager.Instance.IPToId[ip];
            player.hp = 2;
            player.position = Vec3.FromVector3(Vector3.one * player.clientID);

            player.body = Instantiate(bodyPrefab, player.position.ToVector3(), Quaternion.identity);
            player.hasBody = true;
            player.body.transform.name = player.clientID.ToString();
            return player;
        }

        public Dictionary<int, Player> ClientRecieveHandshake(byte[] data, Dictionary<int, Player> _players, Player thisPlayer)
        {
            NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

            (int ID, string name)[] newPlayers = netServerToClientHs.Deserialize(data);

            Dictionary<int, Player> playersList = new Dictionary<int, Player>();

            for (int i = 0; i < newPlayers.Length; i++)
            {
                if (_players != null && _players.Any(player => player.Value.clientID == newPlayers[i].ID)) continue;
                
                GameObject body = Instantiate(bodyPrefab, Vector3.one * newPlayers[i].ID, Quaternion.identity);
                Vector3 position;
                position.x = 1;
                position.y = 1 * i;
                position.z = 1;
                body.transform.position = position;

                if (newPlayers[i].ID != thisPlayer.clientID)
                {
                    playersList.Add(newPlayers[i].ID, new Player
                    {
                        clientID = newPlayers[i].ID,
                        name = newPlayers[i].name,
                        hp = 3,
                        position = Vec3.FromVector3(Vector3.one * newPlayers[i].ID),
                        body = body,
                        hasBody = true
                    });
                }

                if (newPlayers[i].name != thisPlayer.name && !thisPlayer.hasBody) continue;

                onPlayerSpawned?.Invoke(body);
                thisPlayer.clientID = newPlayers[i].ID;
                thisPlayer.hasBody = true;
            }

            return playersList;
        }

        public byte[] PrepareHandshake(string _name)
        {
            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();

            netClientToServerHs.data = _name;
            NetworkManager.Instance.thisPlayer.name = _name;

            return netClientToServerHs.Serialize();
        }
    }
}