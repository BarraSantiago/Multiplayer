using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor;
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

public struct Player
{
    public int clientID;
    public string name;
    public Vector3 position;
}

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
{
    public IPAddress ipAddress { get; private set; }
    public int port { get; private set; }

    public static Player thisPlayer;
    public bool isServer { get; private set; }

    public int TimeOut = 10;
    public static float MS { get; private set; } = 0;
    public Action<byte[], IPEndPoint> OnReceiveEvent;

    private List<Player> players;
    private float time = 0;
    private UdpConnection connection;
    private readonly Dictionary<int, Client> clients = new Dictionary<int, Client>();
    private readonly Dictionary<IPEndPoint, int> ipToId = new Dictionary<IPEndPoint, int>();

    int clientId = 0; // This id should be generated during first handshake

    public void StartServer(int port, string name)
    {
        isServer = true;
        thisPlayer.name = name;
        this.port = port;
        connection = new UdpConnection(port, this);
        players = new List<Player>();
    }

    public void StartClient(IPAddress ip, int port, string name)
    {
        isServer = false;

        this.port = port;
        this.ipAddress = ip;
        thisPlayer.name = name;
        connection = new UdpConnection(ip, port, this);
        time = DateTime.Now.Ticks;

        AddClient(new IPEndPoint(ip, port));
        SendPing();
    }

    void AddClient(IPEndPoint ip)
    {
        if (!ipToId.ContainsKey(ip))
        {
            Debug.Log("Adding client: " + ip.Address);

            int id = clientId;
            ipToId[ip] = clientId;

            clients.Add(clientId, new Client(ip, id, DateTime.Now.Ticks));
            SendHandshake(thisPlayer.name);

            clientId++;
        }
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

        switch (messageType)
        {
            case MessageType.HandShake:
                RecieveHandshake(data);
                break;

            case MessageType.Console:
                OnReceiveEvent?.Invoke(data, ip);
                break;

            case MessageType.Position:
                break;

            case MessageType.Ping:
                SendPing();
                break;

            case MessageType.Pong:
                SendPong(ip);
                break;
            case MessageType.Close:
                break;

            case MessageType.Dispose:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    /// <summary>
    /// Client updates time and sends ping
    /// </summary>
    /// <param name="data"></param>
    private void SendPing()
    {
        time = DateTime.Now.Ticks;

        SendToServer(BitConverter.GetBytes((int)MessageType.Pong));
    }

    /// <summary>
    /// Server updates player time and sends pong
    /// </summary>
    /// <param name="ip"> Client to send pong to </param>
    private void SendPong(IPEndPoint ip)
    {
        var client = clients[ipToId[ip]];

        client.timeStamp = DateTime.Now.Ticks;

        clients[ipToId[ip]] = client;

        connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);
    }

    private void RecieveHandshake(byte[] data)
    {
        if (isServer)
        {
            NetClientToServerHS netClientToServerHs = new NetClientToServerHS();

            string name = netClientToServerHs.Deserialize(data);
            Player player = new Player();

            player.name = name;
            player.clientID = clientId;
            players.Add(player);

            // TODO aca el server deberia mandarle a todos los clientes la lista nueva de clientes
        }
        else
        {
            NetServerToClient netServerToClient = new NetServerToClient();

            Player[] newPlayers = netServerToClient.Deserialize(data);
            List<Player> playersList = new List<Player>();

            // Player recognizes itself from the list and removes himself
            for (int i = 0; i < newPlayers.Length; i++)
            {
                if (newPlayers[i].name != thisPlayer.name) continue;

                thisPlayer = newPlayers[i];
                playersList = newPlayers.ToList();
                playersList.Remove(newPlayers[i]);
                break;
            }

            players = playersList;
        }
    }

    private void SendHandshake(string name)
    {
        if (isServer)
        {
            NetServerToClient netServerToClient = new NetServerToClient();

            netServerToClient.data = players.ToArray();

            //TODO update server list of players
            Broadcast(netServerToClient.Serialize());
        }
        else
        {
            NetClientToServerHS netClientToServerHs = new NetClientToServerHS();

            netClientToServerHs.data = name;
            thisPlayer.name = name;

            SendToServer(netClientToServerHs.Serialize());
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

        CheckPings();
    }

    private void CheckPings()
    {
        if (connection == null) return;
        
        if (isServer)
        {
            bool clientRemoved = false;
            foreach (var client in clients.Values.Where(client => client.timeStamp + TimeOut < DateTime.Now.Ticks))
            {
                RemoveClient(client.ipEndPoint);
                clientRemoved = true;
            }

            if (clientRemoved)
            {
                SendHandshake("");
            }
        }
        else
        {
            MS = DateTime.Now.Ticks - time;

            if (time + TimeOut < DateTime.Now.Ticks)
            {
                //TODO client disconnects itself
                Disconnect();
            }
        }
    }

    public void Disconnect()
    {
    #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
}