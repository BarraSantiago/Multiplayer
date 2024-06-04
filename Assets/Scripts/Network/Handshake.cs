using System;
using System.Collections.Generic;
using System.Net;
using Game;
using UnityEngine;
using Utils;

namespace Network
{
    public class Handshake : MonoBehaviour
    {
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
            if (ip == null || data == null) return null;
            if (NetworkManager.Instance.Players.Count >= NetworkManager.Instance.MaxPlayers)
            {
               
                    // Name is already used, reject the connection
                    NetRejectClient rejectClientMessage = new NetRejectClient { data = (int)ErrorType.ServerFull };
                    NetworkManager.Instance.Connection.Send(rejectClientMessage.Serialize(), ip);
                    return null;
                
            }
            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();
            string newName = netClientToServerHs.Deserialize(data);

            if (NetworkManager.Instance.Players.Values.Any(player => player.name == newName) || newName == "")
            {
                // Name is already used, reject the connection
                NetRejectClient rejectClientMessage = new NetRejectClient { data = (int)ErrorType.NameInUse };
                NetworkManager.Instance.Connection.Send(rejectClientMessage.Serialize(), ip);
                return null;
            }
            
            AddClient(ip);

            Vec3 pos = Vec3.FromVector3(Vector3.up * NetworkManager.Instance.IPToId[ip]);

            GameObject body = Instantiate(bodyPrefab, pos.ToVector3(), Quaternion.identity);

            Player player = body.AddComponent<Player>();

            player.name = newName;
            player.gameObject.transform.name = newName;
            player.clientID = NetworkManager.Instance.IPToId[ip];
            return player;
        }

        public Dictionary<int, Player> ClientRecieveHandshake(byte[] data)
        {
            NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

            (int ID, string name, Vec3 pos)[] newPlayers = netServerToClientHs.Deserialize(data);

            Dictionary<int, Player> playersList = new Dictionary<int, Player>();

            for (int i = 0; i < newPlayers.Length; i++)
            {
                if (NetworkManager.Instance.Players != null &&
                    NetworkManager.Instance.Players.Any(player => player.Value.clientID == newPlayers[i].ID)) continue;
                
                if (newPlayers[i].name == NetworkManager.Instance.thisPlayer.name && NetworkManager.Instance.thisPlayer.clientID == -1)
                {
                    
                    NetworkManager.Instance.thisPlayer.clientID = newPlayers[i].ID;
                    NetworkManager.Instance.thisPlayer.gameObject.transform.position = newPlayers[i].pos.ToVector3();
                    
                    continue;
                }
                
                if(newPlayers[i].ID == NetworkManager.Instance.thisPlayer.clientID) continue;
                
                GameObject body = Instantiate(bodyPrefab, Vector3.one * newPlayers[i].ID, Quaternion.identity);
                
                body.transform.position = newPlayers[i].pos.ToVector3();
                
                Player player = body.AddComponent<Player>();
                
                player.name = newPlayers[i].name;
                player.clientID = newPlayers[i].ID;
                player.gameObject.transform.name = newPlayers[i].name;

                playersList.Add(newPlayers[i].ID, player);
            }

            return playersList;
        }

        public byte[] PrepareHandshake(string _name)
        {
            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();

            netClientToServerHs.data = _name;
            if(!NetworkManager.Instance.IsServer) NetworkManager.Instance.thisPlayer.name = _name;

            return netClientToServerHs.Serialize();
        }
    }
}