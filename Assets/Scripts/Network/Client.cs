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
        public DateTime timeStamp;
        public int id;
        public IPEndPoint ipEndPoint;

        public Client(IPEndPoint ipEndPoint, int id, DateTime timeStamp)
        {
            this.timeStamp = timeStamp;
            this.id = id;
            this.ipEndPoint = ipEndPoint;
        }

        private void SendPing()
        {
            TimeSpan newDateTime = DateTime.UtcNow - _time;
            MS = (float)newDateTime.Milliseconds;

            _time = DateTime.UtcNow;

            SendToServer(BitConverter.GetBytes((int)MessageType.Pong));
        }

        public void MovePlayer(Vec3 pos)
        {
            NetVector3 netVector3 = new NetVector3(pos, NetworkManager.Instance.thisPlayer.clientID);
            SendToServer(netVector3.Serialize());
        }

        public void FireBullet(Vec3 pos, Vec3 dire)
        {
            NetShoot netVector3 = new NetShoot(pos.ToVector3(), dire.ToVector3(),
                NetworkManager.Instance.thisPlayer.clientID);
            SendToServer(netVector3.Serialize());
        }

        private void MovePlayers(byte[] data, IPEndPoint ip)
        {
            NetVector3 netVector3 = new NetVector3(data);
            (Vec3 pos, int id) newData = netVector3.data;

            if (newData.id == NetworkManager.Instance.thisPlayer.clientID) return;

            Players[newData.id].gameObject.transform.position = newData.pos;
        }

        public void SendToServer(byte[] data)
        {
            NetworkManager.Instance.Connection.Send(data);
        }

        public void CheckPing()
        {
            DateTime timeOutTime = _time.AddSeconds(timeOut);
            if (timeOutTime < DateTime.UtcNow)
            {
                CheckDisconnect();
            }
        }

        public void Disconnect()
        {
            NetworkManager.Instance.Connection.Close();
        }

        public void CheckDisconnect()
        {
            SendToServer(BitConverter.GetBytes((int)MessageType.Close));
            Disconnect();
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

            if (newData.id == thisPlayer.clientID) return;

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