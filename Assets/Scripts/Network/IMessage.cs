using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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

    public class NetServerToClientHs : IMessage<(int ID, string name)[]>
    {
        public (int ID, string name)[] data;

        public (int ID, string name)[] Deserialize(byte[] message)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();

            byte[] playerArray = new byte[message.Length - 4];

            // Removes the message type from the array
            Array.Copy(message, 4, playerArray, 0, playerArray.Length);

            using MemoryStream memoryStream = new MemoryStream(playerArray);

            return ((int ID, string name)[])binaryFormatter.Deserialize(memoryStream);
        }

        public MessageType GetMessageType()
        {
            return MessageType.HandShake;
        }

        public byte[] Serialize()
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();

            binaryFormatter.Serialize(memoryStream, data);

            memoryStream.Position = 0;

            List<byte> outData = new List<byte>();

            outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
            outData.AddRange(memoryStream.ToArray());

            return outData.ToArray();
        }
    }

    public class NetVector3 : IMessage<(Vector3 pos, int id)>
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
            int offset = 4; // Skip the message type

            // Read pos (Vector3) from the byte array
            float x = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float y = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            float z = BitConverter.ToSingle(message, offset);
            offset += sizeof(float);
            Vector3 pos = new Vector3(x, y, z);

            // Read id from the byte array
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