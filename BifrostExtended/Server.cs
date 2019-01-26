using Bifrost;
using BifrostExtended.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static BifrostExtended.Delegates;

namespace BifrostExtended
{
    public class Server
    {
        public int MaxConnections = 100;
        public bool NoAuthentication = false;
        public bool RememberCertificates = false;
        private readonly object _UserListLock = new object();
        private List<ClientData> Clients = new List<ClientData>();
        private TcpListener listener = null;
        private Logger logger = Bifrost.LogManager.GetCurrentClassLogger();

        private CancellationToken serverCancellationToken;

        private CancellationTokenSource serverCancellationTokenSource = new CancellationTokenSource();

        public event Delegates.LogMessage OnLogEvent;

        public event BifrostExtended.Delegates.ServerDataReceived OnServerDataReceived;

        public event UserConnected OnUserConnected;

        public bool IsRunning { get; private set; }

        public Server(int maxConnections = 100)
        {
            Bifrost.LogManager.SetMinimumLogLevel(Bifrost.SerilogLogLevel.Debug);
            Bifrost.EventSink.OnLogEvent += EventSink_OnLogEvent;

            Bifrost.CertManager.GenerateCertificateAuthority();

            MaxConnections = maxConnections;
        }

        public void SetLogLevel(BifrostExtended.SerilogLogLevel logLevel)
        {
            Bifrost.LogManager.SetMinimumLogLevel((Bifrost.SerilogLogLevel)logLevel);
        }

        public void IgnoreLogClass(string ignoredClass)
        {
            Bifrost.LogManager.IgnoreLogClass(ignoredClass);
        }

        public void BroadcastMessage(Dictionary<string, byte[]> Store, AuthState minimumAuthState = AuthState.Authenticated, ClientData skipUser = null)
        {
            Message msg = new Message(MessageType.Data, 0x01);
            msg.Store = Store;

            lock (_UserListLock)
            {
                foreach (var user in Clients)
                {
                    try
                    {
                        if (skipUser != null && user == skipUser)
                            continue;

                        if (user.AuthenticationState >= minimumAuthState)
                            user.Connection.ServerLink.SendMessage(msg);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Server BroadcastMessage: " + user.ClientName != string.Empty ? user.ClientGuid : user.ClientName);
                    }
                }
            }
        }

        public void BroadcastMessage(IMessage msg, AuthState minimumAuthState = AuthState.Authenticated, ClientData skipUser = null)
        {
            string serialized = JsonConvert.SerializeObject(msg, Formatting.None);

            Type t = msg.GetType();

            Message message = new Message(MessageType.Data, 0x01);
            message.Store["type"] = Encoding.UTF8.GetBytes(t.Name);
            message.Store["message"] = Encoding.UTF8.GetBytes(serialized);

            lock (_UserListLock)
            {
                foreach (var user in Clients)
                {
                    try
                    {
                        if (skipUser != null && user == skipUser)
                            continue;

                        if (user.AuthenticationState >= minimumAuthState)
                            user.Connection.ServerLink.SendMessage(message);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Server BroadcastMessage: " + user.ClientName != string.Empty ? user.ClientGuid : user.ClientName);
                    }
                }
            }
        }

        public ClientData GetClientFromLink(ServerLink serverLink)
        {
            lock (_UserListLock)
            {
                foreach (var user in Clients)
                {
                    if (user.Connection.ServerLink == serverLink)
                    {
                        logger.Debug("GetClientFromLink found: " + user.ClientId);
                        return user;
                    }
                }
            }
            return null;
        }

        public ClientData GetClientFromLink(EncryptedLink encryptedLink)
        {
            lock (_UserListLock)
            {
                foreach (var user in Clients)
                {
                    if (user.Connection.ServerLink.GetEncryptedLink() == encryptedLink)
                    {
                        logger.Debug("GetClientFromLink found: " + user.ClientId);
                        return user;
                    }
                }
            }
            return null;
        }

        public bool SendMessage(Dictionary<string, byte[]> Store, UserConnection user)
        {
            Message msg = new Message(MessageType.Data, 0x01);
            msg.Store = Store;

            try
            {
                user.ServerLink.SendMessage(msg);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Server SendMessage");
                return false;
            }
            return true;
        }

        public bool SendMessage(IMessage msg, UserConnection user)
        {
            string serialized = JsonConvert.SerializeObject(msg, Formatting.None);

            Type t = msg.GetType();

            Message message = new Message(MessageType.Data, 0x01);
            message.Store["type"] = Encoding.UTF8.GetBytes(t.Name);
            message.Store["message"] = Encoding.UTF8.GetBytes(serialized);

            try
            {
                user.ServerLink.SendMessage(message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Server SendMessage");
                return false;
            }
            return true;
        }

        public void Start(int listenPort)
        {
            serverCancellationToken = serverCancellationTokenSource.Token;

            this.listener = new TcpListener(IPAddress.Any, listenPort);
            this.listener.Start();

            Task.Factory.StartNew(() => KeyManager.MonitorKeyGeneration(serverCancellationToken),
                    serverCancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            Task listener = Task.Factory.StartNew(() => ThreadedServerStart(this.listener, serverCancellationToken),
                serverCancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            IsRunning = true;
            logger.Info($"Listening for clients on port {listenPort}..");
        }

        public void Stop()
        {
            if (serverCancellationToken.CanBeCanceled)
                serverCancellationTokenSource.Cancel();

            IsRunning = false;

            listener.Stop();
            listener = null;

            lock (_UserListLock)
            {
                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    Clients[i].Connection.ServerLink.Close();
                    //users[i].tcpTunnel.Close();
                    //users[i].tcpClient.Close();
                    //users[i].clientThread.Abort();
                }
            }

            logger.Info($"Server is stopped..");
        }

        public void TrustClientCertificate(ClientData client, bool trusted)
        {
            lock (_UserListLock)
            {
                client.Connection.ServerLink.SetCertificateAuthorityTrust(trusted);
            }
        }

        public bool IsConnectionTrusted(ClientData client)
        {
            return client.Connection.ServerLink.TrustedCertificateUsed;
        }

        private void CleanupClient(ClientData client)
        {
            try
            {
                lock (_UserListLock)
                {
                    if (client.Connected)
                        client.Connection.ServerLink.Close();

                    Clients.Remove(client);
                }
            }
            finally { }
        }

        private Delegate EventSink_OnLogEvent(string log)
        {
            OnLogEvent?.Invoke(log);
            return null;
        }

        private void Link_OnDataReceived(EncryptedLink link, Dictionary<string, byte[]> Store)
        {
            ClientData clientData = GetClientFromLink(link);

            // If the store contains a Message type..
            if (Store.ContainsKey("type") && Handler.GetServerMessageType(Encoding.UTF8.GetString(Store["type"])) != null)
            {
                IMessage message = Messages.Handler.ConvertServerPacketToMessage(Store["type"], Store["message"]);
                Handler.HandleServerMessage(clientData, message);
            }
            else
            {
                logger.Warn("Unknown MessageType sent from Client: " + Encoding.UTF8.GetString(Store["type"]));
                OnServerDataReceived?.Invoke(clientData, Store);
            }
        }

        private void Link_OnLinkClosed(EncryptedLink link)
        {
            ClientData cd = GetClientFromLink(link);
            cd.Connected = false;
            OnUserConnected?.Invoke(cd);
            CleanupClient(cd);
        }

        private void ProcessClient(object argument)
        {
            TcpClient client = (TcpClient)argument;

            logger.Debug($"Client socket accepted..");
            TcpTunnel tunnel = new TcpTunnel(client);
            logger.Debug($"Client tunnel created..");
            ServerLink link = new ServerLink(tunnel);
            logger.Debug($"Client link created..");

            link.RememberRemoteCertAuthority = RememberCertificates;
            link.NoAuthentication = NoAuthentication;

            //link.RememberPeerKeys = true;

            // Get a key from the precomputed keys list
            string ca, priv;
            byte[] sign;

            (ca, priv, sign) = KeyManager.GetNextAvailableKeys();

            if (String.IsNullOrEmpty(ca) || String.IsNullOrEmpty(priv) || sign.Length == 0)
            {
                logger.Error("GetNextAvailableKeys returned empty data!");
                link.Close();
                return;
            }

            logger.Debug($"Passing certificates into Bifrost..");
            link.LoadCertificatesNonBase64(ca, priv, sign);

            link.OnDataReceived += Link_OnDataReceived;
            link.OnLinkClosed += Link_OnLinkClosed;

            logger.Debug($"Performing handshake with client..");
            var result = link.PerformHandshake();

            if (result.Type == HandshakeResultType.Successful)
            {
                logger.Debug($"Handshake was a success!");
                var connection = new UserConnection(client, serverLink: link);
                var user = new ClientData(connection);
                user.ClientKeys.ServerCertificateAuthority = ca;
                user.ClientKeys.PrivateKey = priv;
                user.ClientKeys.SignKey = sign;
                // for use after handshake and when remembering clientCa (unimplemented)
                //user.Client.ClientKeys.ClientCertificateAuthority = clientCa;

                lock (_UserListLock)
                {
                    if (Clients.Count + 1 > MaxConnections)
                    {
                        link.Close();
                        return;
                    }
                    Clients.Add(user);
                }
                OnUserConnected?.Invoke(user);
            }
            else
            {
                logger.Info($"Handshake failure: {result.Type}");
                link.Close();
            }
        }

        private void ThreadedServerStart(TcpListener listener, CancellationToken token)
        {
            logger.Debug($"Threaded listen server started..");
            while (!token.IsCancellationRequested)
            {
                while (IsRunning && !listener.Pending())
                    Thread.Sleep(1);

                while (Clients.Count >= MaxConnections)
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (!IsRunning)
                    return;

                TcpClient client = new TcpClient();

                client = listener.AcceptTcpClient();

                Task task = Task.Factory.StartNew(() => ProcessClient(client),
                    serverCancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                //task.ContinueWith( taskResult => logger.Debug($"ProcessClient task {task.Id} has finished."));
            }
        }
    }
}