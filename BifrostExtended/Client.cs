﻿using Bifrost;
using BifrostExtended.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static BifrostExtended.Delegates;

namespace BifrostExtended
{
    public class Client
    {
        public bool AutoReconnect = true;
        public ClientData ClientData;
        public bool NoAuthentication = false;
        public bool RememberCertificates = false;
        private TcpClient client;
        private CancellationToken clientCancellationToken;
        private CancellationTokenSource clientCancellationTokenSource = new CancellationTokenSource();
        private ClientLink link;
        private Logger logger = Bifrost.LogManager.GetCurrentClassLogger();

        private TcpTunnel tunnel;

        public event ClientConnectionState OnClientConnectionChange;

        public event BifrostExtended.Delegates.ClientDataReceived OnClientDataReceived;

        public event Delegates.LogMessage OnLogEvent;

        public bool IsConnected { get; private set; }

        public bool IsConnecting { get; private set; }

        public Client()
        {
            Bifrost.LogManager.SetMinimumLogLevel(Bifrost.SerilogLogLevel.Information);
            Bifrost.EventSink.OnLogEvent += EventSink_OnLogEvent;

            Bifrost.CertManager.GenerateCertificateAuthority();
        }

        public void SetLogLevel(BifrostExtended.SerilogLogLevel logLevel)
        {
            Bifrost.LogManager.SetMinimumLogLevel((Bifrost.SerilogLogLevel)logLevel);
        }

        public void IgnoreLogClass(string ignoredClass)
        {
            Bifrost.LogManager.IgnoreLogClass(ignoredClass);
        }

        public bool Connect(string host, int port)
        {
            if (IsConnected)
                Stop();

            clientCancellationToken = clientCancellationTokenSource.Token;

            if (AutoReconnect)
            {
                Task.Factory.StartNew(() => ReconnectMonitor(clientCancellationToken, host, port),
                        clientCancellationToken,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);
            }
            else
            {
                Task.Factory.StartNew(() => ConnectThread(clientCancellationToken, host, port),
                            clientCancellationToken,
                            TaskCreationOptions.None,
                            TaskScheduler.Default);
            }

            if (IsConnected || IsConnecting)
                return true;
            else
                return false;

        }

        public EncryptedLink GetServerFromLink(ClientLink clientLink)
        {
            return clientLink.GetEncryptedLink();
        }

        public bool SendMessage(IMessage msg)
        {
            string serialized = JsonConvert.SerializeObject(msg, Formatting.None);

            Type t = msg.GetType();

            Message message = new Message(MessageType.Data, 0x01);
            message.Store["type"] = Encoding.UTF8.GetBytes(t.Name);
            message.Store["message"] = Utilities.Compress(Encoding.UTF8.GetBytes(serialized));

            try
            {
                link.SendMessage(message);
            }
            catch (Exception ex)
            {
                logger.Trace(ex.Message, "Client SendMessage");
                return false;
            }
            return true;
        }

        public void Stop()
        {
            if (clientCancellationToken.CanBeCanceled)
                clientCancellationTokenSource.Cancel();

            if (link != null)
                link.Close();

            Thread.Sleep(100);
            link = null;
        }

        public void TrustClientCertificate(bool trusted)
        {
            ClientData.Connection.ClientLink.SetCertificateAuthorityTrust(trusted);
        }

        public bool IsConnectionTrusted()
        {
            return link.TrustedCertificateUsed;
        }

        private void ConnectThread(CancellationToken cancellationToken, string host, int port)
        {

            if (cancellationToken.IsCancellationRequested)
                return;

            logger.Info($"Attempting to connect to {host}:{port}..");
            IsConnecting = true;
            try
            {
                client = new TcpClient(host, port);
            }
            catch (Exception ex)
            {
                logger.Error($"Connection error: {ex.Message}");
                IsConnecting = false;
                IsConnected = false;
                return;
            }

            logger.Debug($"Connected. Setting up tunnel..");
            tunnel = new TcpTunnel(client);

            logger.Debug($"Setting up link..");
            link = new ClientLink(tunnel);

            link.RememberRemoteCertAuthority = RememberCertificates;
            link.NoAuthentication = NoAuthentication;

            logger.Debug($"Creating Keys..");

            var (ca, priv, sign) = Bifrost.CertManager.GenerateKeys();

            logger.Debug($"Loading keys into Bifrost..");
            link.LoadCertificatesNonBase64(ca, priv, sign);

            var connection = new UserConnection(client, clientLink: link);
            var user = new ClientData(connection);
            user.ClientKeys.ServerCertificateAuthority = ca;
            user.ClientKeys.PrivateKey = priv;
            user.ClientKeys.SignKey = sign;
            ClientData = user;

            link.OnDataReceived += Link_OnDataReceived;
            link.OnLinkClosed += Link_OnLinkClosed;
            var result = link.PerformHandshake();

            if (result.Type != HandshakeResultType.Successful)
            {
                logger.Warn($"Handshake failed with type {result.Type}");
                IsConnecting = false;
                IsConnected = false;
                Utilities.RaiseEventOnUIThread(OnClientConnectionChange, this, false);
                return;
            }
            else
            {
                logger.Debug($"Handshake was successful!");
                IsConnecting = false;
                IsConnected = true;

                Utilities.RaiseEventOnUIThread(OnClientConnectionChange, this, true);
            }
        }

        private Delegate EventSink_OnLogEvent(string log)
        {
            Utilities.RaiseEventOnUIThread(OnLogEvent, log);
            return null;
        }

        private void Link_OnDataReceived(EncryptedLink link, Dictionary<string, byte[]> Store)
        {
            // If the store contains a Message type..
            if (Store.ContainsKey("type") && Handler.GetClientMessageType(Encoding.UTF8.GetString(Store["type"])) != null)
            {
                IMessage message = Messages.Handler.ConvertClientPacketToMessage(Store["type"], Utilities.Decompress(Store["message"]));
                Handler.HandleClientMessage(this, message);
            }
            else
            {
                logger.Warn("Unknown MessageType sent from Server: " + Encoding.UTF8.GetString(Store["type"]));
                
                Utilities.RaiseEventOnUIThread(OnClientDataReceived, this, Store);
            }
        }

        private void Link_OnLinkClosed(EncryptedLink link)
        {
            IsConnected = false;
            IsConnecting = false;
            Utilities.RaiseEventOnUIThread(OnClientConnectionChange, this, false);
        }

        private void ReconnectMonitor(CancellationToken clientCancellationToken, string host, int port)
        {
            logger.Info($"AutoReconnect Monitor started..");
            while (AutoReconnect && !clientCancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(5000);
                if (!IsConnected && !IsConnecting && AutoReconnect)
                {
                    ConnectThread(clientCancellationToken, host, port);
                }
            }
        }
    }
}