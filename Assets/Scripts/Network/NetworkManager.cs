using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Game;
using UnityEditor;
using UnityEngine;
using Utils;

public struct Client
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
}


public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
{
    public IPAddress ipAddress { get; private set; }
    public int port { get; private set; }
    public static Player thisPlayer;
    public bool isServer { get; private set; }
    public int TimeOut = 10;
    public static double MS { get; private set; } = 0;
    public Action<byte[], IPEndPoint> OnReceiveEvent;
    public GameObject bodyPrefab;

    private List<Player> players;
    private DateTime time;
    private UdpConnection connection;
    private readonly Dictionary<int, Client> clients = new Dictionary<int, Client>();
    private readonly Dictionary<IPEndPoint, int> ipToId = new Dictionary<IPEndPoint, int>();
    private int clientId = 0;

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
        time = DateTime.UtcNow;

        SendHandshake(name);
        SendPing();
    }

    private void Update()
    {
        // Flush the data in main thread
        if (connection != null)
            connection.FlushReceiveData();

        CheckPings();
    }

    public void SendToServer(byte[] data)
    {
        connection.Send(data);
    }

    public void Broadcast(byte[] data)
    {
        using var iterator = clients.GetEnumerator();

        while (iterator.MoveNext())
        {
            connection.Send(data, iterator.Current.Value.ipEndPoint);
        }
    }

    private MessageType CheckMessageType(byte[] data)
    {
        return (MessageType)BitConverter.ToInt32(data);
    }

    public void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        MessageType messageType = CheckMessageType(data);

        switch (messageType)
        {
            case MessageType.HandShake:
                RecieveHandshake(data, ip);
                break;

            case MessageType.Console:
                OnReceiveEvent?.Invoke(data, ip);
                break;

            case MessageType.Position:
                MovePlayers(data);
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

            case MessageType.Shoot:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void RecieveHandshake(byte[] data, IPEndPoint ip)
    {
        if (isServer)
        {
            AddClient(ip);

            NetClientToServerHS netClientToServerHs = new NetClientToServerHS();
            NetServerToClient netServerToClient = new NetServerToClient();

            string newName = netClientToServerHs.Deserialize(data);
            Player player = new Player();

            player.name = newName;
            player.clientID = ipToId[ip];
            player.hp = 2;
            player.position = Vec3.FromVector3(Vector3.one * player.clientID);
            players.Add(player);

            Instantiate(bodyPrefab, player.position.ToVector3(), Quaternion.identity);

            netServerToClient.data = players.ToArray();

            Broadcast(netServerToClient.Serialize());
        }
        else
        {
            NetServerToClient netServerToClient = new NetServerToClient();

            Player[] newPlayers = netServerToClient.Deserialize(data);
            List<Player> playersList = new List<Player>();

            // Player recognizes itself from the list and removes himself
            for (int i = 0; i < newPlayers.Length; i++)
            {
                GameObject body = Instantiate(bodyPrefab, Vector3.one * newPlayers[i].clientID, Quaternion.identity);
                Vector3 position;
                position.x = newPlayers[i].position.x;
                position.y = newPlayers[i].position.y * i;
                position.z = newPlayers[i].position.z;
                body.transform.position = position;

                if (newPlayers[i].name != thisPlayer.name) continue;

                thisPlayer = newPlayers[i];
                playersList = newPlayers.ToList();
                playersList.Remove(newPlayers[i]);
            }

            players = playersList;
        }
    }

    private void SendHandshake(string name)
    {
        NetClientToServerHS netClientToServerHs = new NetClientToServerHS();

        netClientToServerHs.data = name;
        thisPlayer.name = name;

        SendToServer(netClientToServerHs.Serialize());
    }

    private void AddClient(IPEndPoint ip)
    {
        if (ipToId.ContainsKey(ip)) return;

        int id = clientId;
        ipToId[ip] = clientId;

        clients.Add(clientId, new Client(ip, id, DateTime.UtcNow));

        clientId++;
    }

    private void RemoveClient(IPEndPoint ip)
    {
        if (!ipToId.TryGetValue(ip, out var id)) return;

        clients.Remove(id);
        players.Remove(players.FirstOrDefault(player => player.clientID == ipToId[ip]));
    }

    /// <summary>
    /// Client updates time and sends ping
    /// </summary>
    private void SendPing()
    {
        time = DateTime.UtcNow;

        SendToServer(BitConverter.GetBytes((int)MessageType.Pong));

        long ticks = DateTime.UtcNow.Ticks - (long)MS;

        // Convert ticks to milliseconds
        MS = TimeSpan.FromTicks(ticks).TotalMilliseconds;
    }

    /// <summary>
    /// Server updates player time and sends pong
    /// </summary>
    /// <param name="ip"> Client to send pong to </param>
    private void SendPong(IPEndPoint ip)
    {
        var client = clients[ipToId[ip]];

        client.timeStamp = DateTime.UtcNow;

        clients[ipToId[ip]] = client;

        connection.Send(BitConverter.GetBytes((int)MessageType.Ping), ip);

        long ticks = DateTime.UtcNow.Ticks - (long)MS;

        // Convert ticks to milliseconds
        MS = Math.Round(TimeSpan.FromTicks(ticks).TotalMilliseconds, 2);
    }

    private void MovePlayers(byte[] data)
    {
        NetVector3 netVector3 = new NetVector3(data);
        (Vector3 pos, int id) newData = netVector3.data;
        var oldData = players[newData.id];

        if (isServer)
        {
            oldData.position = Vec3.FromVector3(newData.pos);
            players[newData.id] = oldData;
            Broadcast(data);
        }
        else
        {
            if (newData.id == thisPlayer.clientID)
                return;

            oldData.position = Vec3.FromVector3(newData.pos);
            players[newData.id] = oldData;
        }
    }

    private void CheckPings()
    {
        if (connection == null) return;

        if (isServer)
        {
            bool clientRemoved = false;
            List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();
            foreach (var client in clients.Values)
            {
                DateTime timeOutTime = client.timeStamp;

                if (timeOutTime.AddSeconds(TimeOut) > DateTime.UtcNow) continue;

                clientsToRemove.Add(client.ipEndPoint);
                clientRemoved = true;
            }

            foreach (var client in clientsToRemove)
            {
                RemoveClient(client);
            }

            if (clientRemoved)
            {
                SendHandshake("");
            }
        }
        else
        {
            DateTime timeOutTime = time.AddSeconds(TimeOut);
            if (timeOutTime < DateTime.UtcNow)
            {
                Disconnect();
            }
        }
    }

    private void Disconnect()
    {
        connection.Close();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}