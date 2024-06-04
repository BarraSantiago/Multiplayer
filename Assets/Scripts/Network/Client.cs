using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using Utils;

namespace Network
{
    public class Client
    {
        public static Action<> OnFireBullet;
        public Action<string> OnRejected;
        public int id;
        public int timeOut = 10;
        public IPEndPoint ipEndPoint;
        public double MS { get; private set; } = 0;

        public DateTime timeStamp;
        private Handshake _handshake;

        public Client(IPEndPoint ipEndPoint, int id, DateTime timeStamp)
        {
            this.id = id;
            this.ipEndPoint = ipEndPoint;
            this.timeStamp = timeStamp;

            SendToServer(_handshake.PrepareHandshake(name));
            SendPing();
            NetworkManager.OnClose += Disconnect;
        }

        
        
        private void SendPing()
        {
            TimeSpan newDateTime = DateTime.UtcNow - timeStamp;
            MS = (float)newDateTime.Milliseconds;

            timeStamp = DateTime.UtcNow;

            SendToServer(BitConverter.GetBytes((int)MessageType.Pong));
        }

        public void MovePlayer(Vec3 pos)
        {
            NetVec3 netVector3 = new NetVec3(pos, id);
            SendToServer(netVector3.Serialize());
        }

        public void FireBullet(Vec3 pos, Vec3 dire)
        {
            NetShoot netVector3 = new NetShoot(pos, dire, id);
            SendToServer(netVector3.Serialize());
        }

        private void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVec3 netVector3 = new NetVec3(data);
            (Vec3 pos, int id) newData = netVector3.data;

            if (newData.id == id) return;

            Players[newData.id].gameObject.transform.position = newData.pos;
        }

        public void SendToServer(byte[] data)
        {
            NetworkManager.Connection.Send(data);
        }

        public void CheckPing()
        {
            DateTime timeOutTime = timeStamp.AddSeconds(timeOut);
            if (timeOutTime < DateTime.UtcNow)
            {
                CheckDisconnect();
            }
        }

        public void Disconnect(IPEndPoint ip)
        {
            NetworkManager.Connection.Close();
        }

        public void CheckDisconnect()
        {
            SendToServer(BitConverter.GetBytes((int)MessageType.Close));
            Disconnect(null);
        }

        public void HandleHandshake(Byte[] data)
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

        private void HandleBullets(byte[] data)
        {
            NetShoot netShoot = new NetShoot(data);
            (Vec3 pos, Vec3 target, int id) newData = netShoot.data;
            
            Bullet bullet = Instantiate(bulletPrefab, newData.pos, Quaternion.identity).AddComponent<Bullet>();
            bullet.SetTarget(newData.target);
            bullet.clientID = newData.id;
        }

        public void Reject(string reason)
        {
            NetworkManager.Instance.Connection.Close();
            Destroy(thisPlayer.gameObject);
            OnRejected?.Invoke(reason);
        }
    }
}