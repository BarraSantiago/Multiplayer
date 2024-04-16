using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public struct Client
{
    public float timeStamp;
    public int id;
    public IPEndPoint ipEndPoint;

    public Client(IPEndPoint ipEndPoint, int id, float timeStamp)
    {
        this.timeStamp = timeStamp;
        this.id = id;
        this.ipEndPoint = ipEndPoint;
    }
}

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
{
    public IPAddress ipAddress { get; private set; }

    public int port { get; private set; }

    public static Client client;
    public bool isServer { get; private set; }

    public int TimeOut = 30;

    public Action<byte[], IPEndPoint> OnReceiveEvent;

    private UdpConnection connection;

    private readonly Dictionary<int, Client> clients = new Dictionary<int, Client>();
    private readonly Dictionary<IPEndPoint, int> ipToId = new Dictionary<IPEndPoint, int>();

    int clientId = 0; // This id should be generated during first handshake

    public void StartServer(int port)
    {
        isServer = true;
        this.port = port;
        connection = new UdpConnection(port, this);
    }

    public void StartClient(IPAddress ip, int port)
    {
        isServer = false;

        this.port = port;
        this.ipAddress = ip;

        connection = new UdpConnection(ip, port, this);

        AddClient(new IPEndPoint(ip, port));
    }

    void AddClient(IPEndPoint ip)
    {
        if (!ipToId.ContainsKey(ip))
        {
            Debug.Log("Adding client: " + ip.Address);

            int id = clientId;
            ipToId[ip] = clientId;

            clients.Add(clientId, new Client(ip, id, Time.realtimeSinceStartup));
            SendHandshake();

            clientId++;
        }
    }

    private void SendHandshake()
    {
        NetHandShake netHandShake = new NetHandShake();

        netHandShake.data = client.id = -1;

        SendToServer(netHandShake.Serialize());
    }

    void RemoveClient(IPEndPoint ip)
    {
        if (ipToId.ContainsKey(ip))
        {
            Debug.Log("Removing client: " + ip.Address);
            clients.Remove(ipToId[ip]);
        }
    }

    public void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        AddClient(ip);

        MessageType messageType = CheckMessageType(data);

        NetConsole console = new NetConsole();
        switch (messageType)
        {
            case MessageType.HandShake:
                RecieveHandshake(data, ip);
                break;

            case MessageType.Console:
                OnReceiveEvent?.Invoke(data, ip);

                break;
            case MessageType.Position:
                break;
        }
    }

    private void RecieveHandshake(byte[] data, IPEndPoint ipEndPoint)
    {
        NetHandShake netHandShake = new NetHandShake();
        if (isServer)
        {
            netHandShake.data = ipToId[ipEndPoint];
            connection.Send(netHandShake.Serialize(), ipEndPoint);
            // TODO aca el server deberia mandarle a todos los clientes la lista nueva de clientes
            Broadcast();
        }
        else if(client.id == -1)
        {
            
            int newId = netHandShake.Deserialize(data);
            client = new Client(ipEndPoint, newId, Time.realtimeSinceStartup);
        }
        else
        {
            
        }
    }

    private MessageType CheckMessageType(byte[] data)
    {
        return (MessageType)BitConverter.ToInt32(data);
    }

    public void SendToServer(byte[] data)
    {
        connection.Send(data);
    }

    public void Broadcast(byte[] data)
    {
        using (var iterator = clients.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }
    }

    void Update()
    {
        // Flush the data in main thread
        if (connection != null)
            connection.FlushReceiveData();
    }
}