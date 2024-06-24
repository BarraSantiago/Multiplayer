using System.Net;
using System.Numerics;

namespace MultiplayerLib
{
    public class Handshake
    {
        private int _clientId = 0;
        private int _maxPlayers = 4;
        private int _playersCount = 0;
        public readonly Dictionary<IPEndPoint, int> IPToId = new Dictionary<IPEndPoint, int>();
        public Dictionary<int, Player> Players = new Dictionary<int, Player>();
        private void AddClient(IPEndPoint ip)
        {
            if (IPToId.ContainsKey(ip)) return;

            int id = _clientId;

            IPToId[ip] = _clientId;


            _clientId++;
        }

        public string ServerRecieveHandshake(byte[] data, IPEndPoint ip, UdpConnection Connection)
        {
            if (ip == null || data == null) return null;
            if (_playersCount >= _maxPlayers)
            {
                // Name is already used, reject the connection
                NetRejectClient rejectClientMessage = new NetRejectClient { data = (int)ErrorType.ServerFull };
                Connection.Send(rejectClientMessage.Serialize(), ip);
                return null;
            }

            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();
            string newName = netClientToServerHs.Deserialize(data);

            if (Players.Values.Any(player => player.name == newName) || newName == "")
            {
                // Name is already used, reject the connection
                NetRejectClient rejectClientMessage = new NetRejectClient { data = (int)ErrorType.NameInUse };
                Connection.Send(rejectClientMessage.Serialize(), ip);
                return null;
            }

            AddClient(ip);

            Vec3 pos = Vec3.FromVector3(Vector3.UnitY * IPToId[ip]);


            return newName;
        }

        public Dictionary<int, Player> ClientRecieveHandshake(byte[] data, Player thisPlayer)
        {
            NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

            (int ID, string name, Vec3 pos)[] newPlayers = netServerToClientHs.Deserialize(data);

            Dictionary<int, Player> playersList = new Dictionary<int, Player>();

            for (int i = 0; i < newPlayers.Length; i++)
            {
                if (Players != null &&
                    Players.Any(player => player.Value.clientID == newPlayers[i].ID)) continue;

                if (newPlayers[i].name == thisPlayer.name &&
                    thisPlayer.clientID == -1)
                {
                    thisPlayer.clientID = newPlayers[i].ID;
                    thisPlayer.pos = newPlayers[i].pos;

                    continue;
                }

                if (newPlayers[i].ID == thisPlayer.clientID) continue;


                Player player = new Player();

                player.name = newPlayers[i].name;
                player.clientID = newPlayers[i].ID;
                player.pos = newPlayers[i].pos;
                
                playersList.Add(newPlayers[i].ID, player);
            }

            return playersList;
        }

        public byte[] PrepareHandshake(string name)
        {
            NetClientToServerHs netClientToServerHs = new NetClientToServerHs();

            netClientToServerHs.data = name;

            return netClientToServerHs.Serialize();
        }
    }
}