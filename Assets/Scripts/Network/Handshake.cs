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
        public Material playerMaterial;
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
            
            Vec3 pos = Vec3.FromVector3(Vector3.up * NetworkManager.Instance.IPToId[ip]);
            
            Player player = new Player
            {
                name = newName,
                clientID = NetworkManager.Instance.IPToId[ip],
                hp = 3,
                position = pos,
                body = Instantiate(bodyPrefab, pos.ToVector3(), Quaternion.identity),
                hasBody = true
            };

            player.body.transform.name = player.clientID.ToString();
            return player;
        }

        public Dictionary<int, Player> ClientRecieveHandshake(byte[] data)
        {
            NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

            (int ID, string name)[] newPlayers = netServerToClientHs.Deserialize(data);

            Dictionary<int, Player> playersList = new Dictionary<int, Player>();

            for (int i = 0; i < newPlayers.Length; i++)
            {
                if (NetworkManager.Instance.Players != null &&
                    NetworkManager.Instance.Players.Any(player => player.Value.clientID == newPlayers[i].ID)) continue;

                GameObject body = Instantiate(bodyPrefab, Vector3.one * newPlayers[i].ID, Quaternion.identity);
                Vector3 position;
                position.x = 0;
                position.y = 1 * i;
                position.z = 0;
                body.transform.position = position;

                if (newPlayers[i].ID != NetworkManager.Instance.thisPlayer.clientID)
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

                if (NetworkManager.Instance.thisPlayer.hasBody) Destroy(body);
                
                if (newPlayers[i].name != NetworkManager.Instance.thisPlayer.name &&
                    !NetworkManager.Instance.thisPlayer.hasBody) continue;
                
                body.GetComponent<MeshRenderer>().material = playerMaterial;
                onPlayerSpawned?.Invoke(body);
                NetworkManager.Instance.thisPlayer.clientID = newPlayers[i].ID;
                NetworkManager.Instance.thisPlayer.hasBody = true;
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