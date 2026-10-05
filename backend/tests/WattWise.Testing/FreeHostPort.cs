using System.Net;
using System.Net.Sockets;

namespace WattWise.Testing;

/// <summary>Picks a random free loopback TCP port below the ephemeral range.</summary>
internal static class FreeHostPort
{
    private const int Lowest = 10000;
    private const int Highest = 32767;

    public static int Pick()
    {
        while (true)
        {
            int port = Random.Shared.Next(Lowest, Highest + 1);
            try
            {
                // Taken between this check and the container start is possible; the caller retries.
                using TcpListener listener = new(IPAddress.Loopback, port);
                listener.Start();
                return port;
            }
            catch (SocketException)
            {
            }
        }
    }
}
