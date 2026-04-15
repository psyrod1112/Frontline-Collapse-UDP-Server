
using ProtoBuf;

namespace CapstoneUdpServer.Network
{
    public static class ProtobufSerializer
    {
        public static byte[] Serialize<T>(uint packetType, T message)
        {
            using var ms = new MemoryStream();
            byte[] packetBytes = BitConverter.GetBytes(packetType);
            ms.Write(packetBytes, 0, 4);
            Serializer.Serialize(ms, message);
            return ms.ToArray();
        }

        public static T Deserialize<T>(byte[] data)
        {
            using var ms = new MemoryStream(data, 4, data.Length - 4);
            return Serializer.Deserialize<T>(ms);
        }

        public static uint PeekType(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return 0;
            }

            return BitConverter.ToUInt32(data, 0);
        }
    }
    
}