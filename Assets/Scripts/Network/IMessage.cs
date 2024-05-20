using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

namespace Network
{
    public enum MessageType
    {
        HandShake = -1,
        Console,
        Position,
        Ping,
        Pong,
        Close,
        Dispose,
        Shoot
    }


    public interface IMessage<T>
    {
        public MessageType GetMessageType();
        public byte[] Serialize();
        public T Deserialize(byte[] message);
    }

    public class NetClientToServerHs : IMessage<string>
    {
        public string data;

        public string Deserialize(byte[] message)
        {
            string outData;

            outData = System.Text.Encoding.UTF8.GetString(message, 4, message.Length - 4);

            return outData;
        }

        public MessageType GetMessageType()
        {
            return MessageType.HandShake;
        }

        public byte[] Serialize()
        {
            List<byte> outData = new List<byte>();

            outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

            outData.AddRange(data.Select(letter => (byte)letter));

            return outData.ToArray();
        }
    }

public class NetServerToClientHs : IMessage<(int ID, string name, Vector3 pos)[]>
{
    public (int ID, string name, Vector3 pos)[] data;

    public (int ID, string name, Vector3 pos)[] Deserialize(byte[] message)
    {
        int offset = 4; // Skip the message type
        int count = BitConverter.ToInt32(message, offset);
        offset += sizeof(int);

        var result = new (int ID, string name, Vector3 pos)[count];

        for (int i = 0; i < count; i++)
        {
            int id = BitConverter.ToInt32(message, offset);
            offset += sizeof(int);

            int nameLength = BitConverter.ToInt32(message, offset);
            offset += sizeof(int);

            string name = Encoding.UTF8.GetString(message, offset, nameLength);
            offset += nameLength;

            float x = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);

            float y = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);

            float z = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);

            result[i] = (id, name, new Vector3(x, y, z));
        }

        return result;
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(data.Length));

        foreach (var item in data)
        {
            outData.AddRange(BitConverter.GetBytes(item.ID));

            byte[] nameBytes = Encoding.UTF8.GetBytes(item.name);
            outData.AddRange(BitConverter.GetBytes(nameBytes.Length));
            outData.AddRange(nameBytes);

            outData.AddRange(BitConverter.GetBytes(item.pos.x));
            outData.AddRange(BitConverter.GetBytes(item.pos.y));
            outData.AddRange(BitConverter.GetBytes(item.pos.z));
        }

        return outData.ToArray();
    }
}    public class NetVector3 : IMessage<(Vector3 pos, int id)>
    {
        private static ulong lastMsgID = 0;
        public (Vector3 pos, int id) data;

        public NetVector3(Vector3 pos, int id)
        {
            data = (pos, id);
        }

        public NetVector3(byte[] data)
        {
            this.data = Deserialize(data);
        }

        public (Vector3 pos, int id) Deserialize(byte[] message)
        {
            int offset = 4;

            // pos
            float x = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float y = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float z = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            Vector3 pos = new Vector3(x, y, z);

            // id
            int id = BitConverter.ToInt32(message, offset);

            return (pos, id);
        }

        public MessageType GetMessageType()
        {
            return MessageType.Position;
        }

        public byte[] Serialize()
        {
            List<byte> outData = new List<byte>();

            outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

            outData.AddRange(BitConverter.GetBytes(data.pos.x));
            outData.AddRange(BitConverter.GetBytes(data.pos.y));
            outData.AddRange(BitConverter.GetBytes(data.pos.z));

            outData.AddRange(BitConverter.GetBytes(data.id));

            return outData.ToArray();
        }
    }

    public class NetShoot : IMessage<(Vector3 pos, Vector3 dir, int id)>
    {
        public (Vector3 pos, Vector3 dir, int id) data;

        public NetShoot(Vector3 pos, Vector3 dir, int id)
        {
            data = (pos, dir, id);
        }

        public NetShoot(byte[] data)
        {
            this.data = Deserialize(data);
        }

        public (Vector3 pos, Vector3 dir, int id) Deserialize(byte[] message)
        {
            int offset = 4; 

            // pos
            float x = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float y = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float z = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            Vector3 pos = new Vector3(x, y, z);

            // dir
            x = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            y = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            z = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            Vector3 dir = new Vector3(x, y, z);

            // id
            int id = BitConverter.ToInt32(message, offset);

            return (pos, dir, id);
        }

        public MessageType GetMessageType()
        {
            return MessageType.Shoot;
        }

        public byte[] Serialize()
        {
            List<byte> outData = new List<byte>();

            outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

            outData.AddRange(BitConverter.GetBytes(data.pos.x));
            outData.AddRange(BitConverter.GetBytes(data.pos.y));
            outData.AddRange(BitConverter.GetBytes(data.pos.z));

            outData.AddRange(BitConverter.GetBytes(data.dir.x));
            outData.AddRange(BitConverter.GetBytes(data.dir.y));
            outData.AddRange(BitConverter.GetBytes(data.dir.z));

            outData.AddRange(BitConverter.GetBytes(data.id));

            return outData.ToArray();
        }
    }

    public class NetConsole : IMessage<string>
    {
        public string data;

        public MessageType messageType()
        {
            return MessageType.Console;
        }

        public string Deserialize(byte[] message)
        {
            byte[] messageWithoutHeader = new byte[message.Length - 4];

            Array.Copy(message, 4, messageWithoutHeader, 0, message.Length - 4);

            string outData = System.Text.Encoding.UTF8.GetString(messageWithoutHeader);

            return outData;
        }

        public MessageType GetMessageType()
        {
            return MessageType.HandShake;
        }

        public byte[] Serialize()
        {
            List<byte> outData = new List<byte>();

            outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

            foreach (char letter in data)
            {
                outData.Add((byte)letter);
            }

            return outData.ToArray();
        }
    }
}