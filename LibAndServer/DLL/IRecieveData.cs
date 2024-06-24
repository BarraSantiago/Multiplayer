using System.Net;

namespace DLL
{
    public interface IReceiveData
    {
        void OnReceiveData(byte[] data, IPEndPoint ipEndpoint);
    }
}