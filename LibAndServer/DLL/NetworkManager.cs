using System.Net;

namespace MultiplayerLib
{
    public abstract class NetworkManager : IReceiveData
    {
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        
        private Handshake _handshake;
        public static UdpConnection Connection;

        private void RecieveData()
        {
            Connection?.FlushReceiveData();
        }

        private MessageType CheckMessageType(byte[] data)
        {
            return (MessageType)BitConverter.ToInt32(data);
        }

        public int CalculateChecksum(byte[] data)
        {
            return data.Aggregate(0, (current, b) => current + b);
        }

        public void OnReceiveData(byte[] dataWithChecksum, IPEndPoint ip)
        {
            byte[] data = CheckCorrupted(dataWithChecksum);

            if (data == null) return;

            MessageType messageType = CheckMessageType(data);

            switch (messageType)
            {
                case MessageType.HandShake:
                    ReceiveHandshake(data);
                    break;

                case MessageType.Console: 
                    ReceiveConsole(data);
                    break;

                case MessageType.Position:
                    ReceivePosition(data);
                    break;

                case MessageType.Ping:
                    SendPing();
                    break;

                case MessageType.Close:
                    Close();
                    break;

                case MessageType.Shoot:
                    ShootBullet(data);
                    break;

                case MessageType.Rejected:
                    HandleRejected(data);
                    break;

                case MessageType.CountdownStarted:
                    StartTimer();
                    break;

                case MessageType.GameStarted:
                    break;

                case MessageType.Winner:
                    GameWinner(data);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected abstract void StartTimer();

        protected abstract void GameWinner(byte[] data);

        protected abstract void HandleRejected(byte[] data);

        protected abstract void ShootBullet(byte[] data);

        protected abstract void Close();

        protected abstract void SendPing();

        protected abstract void ReceivePosition(byte[] data);

        protected abstract void ReceiveConsole(byte[] data);

        protected abstract void ReceiveHandshake(byte[] data);
        
        private byte[] CheckCorrupted(byte[] dataWithChecksum)
        {
            byte[] data = new byte[dataWithChecksum.Length - sizeof(int)];
            byte[] checksumBytes = new byte[sizeof(int)];

            Buffer.BlockCopy(dataWithChecksum, 0, data, 0, data.Length);
            Buffer.BlockCopy(dataWithChecksum, data.Length, checksumBytes, 0, sizeof(int));

            int receivedChecksum = BitConverter.ToInt32(checksumBytes, 0);
            int calculatedChecksum = CalculateChecksum(data);

            return receivedChecksum == calculatedChecksum ? data : null;
        }

        public virtual void Disconnect()
        {
            Connection.Close();

        }
    }
}