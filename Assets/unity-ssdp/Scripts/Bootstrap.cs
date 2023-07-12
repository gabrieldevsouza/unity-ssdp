using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class Bootstrap : MonoBehaviour
{
    private bool isRunning = true;
    private Socket socket;
    private byte[] buffer = new byte[8192];
    private CancellationTokenSource cancellationTokenSource;

    private async UniTaskVoid Connect(CancellationToken cancellationToken)
    {
        // M-Search message body
        string ms = "M-SEARCH * HTTP/1.1\r\n" +
                    "HOST:239.255.255.250:1900\r\n" +
                    "ST:upnp:rootdevice\r\n" +
                    "MX:2\r\n" +
                    "MAN:\"ssdp:discover\"\r\n" +
                    "\r\n";

        // Set up a UDP socket for multicast
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Bind the socket to the local endpoint
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0);
        socket.Bind(localEndPoint);

        // Join the multicast group
        IPAddress multicastAddress = IPAddress.Parse("239.255.255.250");
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress));

        // Send M-Search message to multicast address for UPnP
        IPEndPoint remoteEndPoint = new IPEndPoint(multicastAddress, 1900);
        byte[] msBytes = Encoding.UTF8.GetBytes(ms);
        await socket.SendToAsync(new ArraySegment<byte>(msBytes), SocketFlags.None, remoteEndPoint);

        // Start receiving responses asynchronously
        while (!cancellationToken.IsCancellationRequested)
        {
            Debug.Log("Step 1");

            // Clear the buffer before each receive operation
            Array.Clear(buffer, 0, buffer.Length);

            int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
            Debug.Log("Step 2");
            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log(response);
        }
    }

    private void OnApplicationQuit()
    {
        // Clean up the socket when the application quits
        isRunning = false;
        cancellationTokenSource.Cancel();

        // Delay closing the socket to allow any pending responses to be processed
        UniTask.Delay(TimeSpan.FromSeconds(1)).ContinueWith(() =>
        {
            socket.Close();
        }).Forget();
    }

    private void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        Connect(cancellationTokenSource.Token).Forget();
    }
}
