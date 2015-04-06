using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ObooltNet
{
    public class NetConnection
    {
        public delegate void NetConnectionEventHandler<TEventArgs>(object sender, NetConnection connection, TEventArgs e);
        public delegate void NetConnectionEventHandler(object sender, NetConnection connection);

        private TcpClient client;
        private TcpListener listener;
        private List<Task> tasks = new List<Task>();

        public event NetConnectionEventHandler OnConnect;
        public event NetConnectionEventHandler OnDisconnect;
        public event NetConnectionEventHandler<byte[]> OnDataReceived;

        public int BufferSize = 1024;

        public EndPoint RemoteEndPoint
        {
            get
            {
                if (client == null)
                    throw new InvalidOperationException("Client is not connected");
                return client.Client.RemoteEndPoint;
            }
        }

        public NetConnection()
        {

        }

        private NetConnection(TcpClient client)
        {
            this.client = client;
        }

        public void Connect(string hostname, int port)
        {
            CheckServerUsedAsClient();
            CheckClientAlreadyConnected();

            client = new TcpClient();
            client.Connect(hostname, port);

            CallOnConnect(this);

            StartReceiveFrom(this);
        }
        public void Connect(IPAddress address, int port)
        {
            CheckServerUsedAsClient();
            CheckClientAlreadyConnected();

            client = new TcpClient();
            client.Connect(address, port);

            CallOnConnect(this);

            StartReceiveFrom(this);
        }
        public void Disconnect()
        {
            CheckServerUsedAsClient();
            CallOnDisconnect(this);
            client.Close();
        }

        public void Start(int port)
        {
            Start(IPAddress.Any, port);
        }
        public void Start(IPAddress address, int port)
        {
            CheckClientUsedAsServer();
            CheckServerAlreadyStarted();

            listener = new TcpListener(address, port);
            listener.Start();

            StartListen();
        }
        public void Stop()
        {
            CheckClientUsedAsServer();
            listener.Stop();
            Task.WhenAll(tasks).Wait();
        }

        public void Send(byte[] data)
        {
            CheckServerUsedAsClient();
            client.GetStream().Write(data, 0, data.Length);
        }

        private void CallOnDataReceived(NetConnection connection, byte[] data)
        {
            if (OnDataReceived != null)
                OnDataReceived(this, connection, data);
        }
        private void CallOnConnect(NetConnection client)
        {
            if (OnConnect != null)
                OnConnect(this, client);
        }
        private void CallOnDisconnect(NetConnection client)
        {
            if (OnDisconnect != null)
                OnDisconnect(this, client);
        }

        private void CheckServerUsedAsClient()
        {
            if (listener != null)
                throw new InvalidOperationException("Cannot use a server connection as a client");
        }
        private void CheckClientUsedAsServer()
        {
            if (client != null)
                throw new InvalidOperationException("Cannot use a client connection as a server");
        }
        private void CheckServerAlreadyStarted()
        {
            if (listener != null)
                throw new InvalidOperationException("Server is already started");
        }
        private void CheckClientAlreadyConnected()
        {
            if (client != null)
                throw new InvalidOperationException("Client is already connected");
        }

        private void StartListen()
        {
            tasks.Add(ListenAsync());
        }
        private async Task ListenAsync()
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                NetConnection connection = new NetConnection(client);
                StartReceiveFrom(connection);
                OnConnect(this, connection);
            }
        }

        private void StartReceiveFrom(NetConnection client)
        {
            tasks.Add(ReceiveFromAsync(client));
        }
        private async Task ReceiveFromAsync(NetConnection client)
        {
            try
            {
                NetworkStream stream = client.client.GetStream();
                byte[] buffer = new byte[BufferSize];
                MemoryStream ms = new MemoryStream();
                while (client.client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, bytesRead);
                    if (!stream.DataAvailable)
                    {
                        CallOnDataReceived(client, ms.ToArray());
                        ms.Seek(0, SeekOrigin.Begin);
                        ms.SetLength(0);
                    }
                }
                CallOnDisconnect(client);
            }
            catch
            {
                CallOnDisconnect(client);
                throw;
            }
        }
    }
}
