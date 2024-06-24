using System.Net;
using System.Net.Sockets;

namespace MultiplayerLib;

public class UdpConnection
{
    private struct DataReceived
    {
        public byte[] data;
        public IPEndPoint ipEndPoint;
    }

    private readonly UdpClient connection;
    private IReceiveData receiver = null;
    private Queue<DataReceived> dataReceivedQueue = new Queue<DataReceived>();

    object handler = new object();
    
    public UdpConnection(int port, IReceiveData receiver = null)
    {
        connection = new UdpClient(port);

        this.receiver = receiver;

        connection.BeginReceive(OnReceive, null);
    }

    public UdpConnection(IPAddress ip, int port, IReceiveData receiver = null)
    {
        connection = new UdpClient();
        connection.Connect(ip, port);

        this.receiver = receiver;

        connection.BeginReceive(OnReceive, null);
    }

    public void Close()
    {
        connection.Close();
    }

    public void FlushReceiveData()
    {
        lock (handler)
        {
            while (dataReceivedQueue.Count > 0)
            {
                DataReceived dataReceived = dataReceivedQueue.Dequeue();
                receiver?.OnReceiveData(dataReceived.data, dataReceived.ipEndPoint);
            }
        }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            DataReceived dataReceived = new DataReceived();
            dataReceived.data = connection.EndReceive(ar, ref dataReceived.ipEndPoint);

            lock (handler)
            {
                dataReceivedQueue.Enqueue(dataReceived);
            }
        }
        catch(SocketException e)
        {
            // This happens when a client disconnects, as we fail to send to that port.
            throw new Exception(e + " Client disconnected");
        }

        connection.BeginReceive(OnReceive, null);
    }

    private int CalculateChecksum(byte[] data)
    {
        return data.Aggregate(0, (current, b) => current + b);
    }
    
    public void Send(byte[] data)
    {
        
        int checksum = CalculateChecksum(data);
        byte[] checksumBytes = BitConverter.GetBytes(checksum);

        byte[] dataWithChecksum = new byte[data.Length + checksumBytes.Length];
        Buffer.BlockCopy(data, 0, dataWithChecksum, 0, data.Length);
        Buffer.BlockCopy(checksumBytes, 0, dataWithChecksum, data.Length, checksumBytes.Length);
        
        connection.Send(dataWithChecksum, dataWithChecksum.Length);
    }
    
    public void Send(byte[] data, IPEndPoint ipEndpoint)
    {
        int checksum = CalculateChecksum(data);
        byte[] checksumBytes = BitConverter.GetBytes(checksum);

        byte[] dataWithChecksum = new byte[data.Length + checksumBytes.Length];
        Buffer.BlockCopy(data, 0, dataWithChecksum, 0, data.Length);
        Buffer.BlockCopy(checksumBytes, 0, dataWithChecksum, data.Length, checksumBytes.Length);

        connection.Send(dataWithChecksum, dataWithChecksum.Length, ipEndpoint);
    }
}