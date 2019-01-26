using Bifrost;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BifrostExtended.Messages
{
    public static class Handler
    {
        private static Logger logger = Bifrost.LogManager.GetCurrentClassLogger();

        private static Dictionary<string, Action<Client, IMessage>> registeredMessageHandlers = new Dictionary<string, Action<Client, IMessage>>();
        private static Dictionary<string, Type> registeredMessageTypes = new Dictionary<string, Type>();
        private static Dictionary<string, Action<ClientData, IMessage>> registeredServerMessageHandlers = new Dictionary<string, Action<ClientData, IMessage>>();

        public static IMessage ConvertClientPacketToMessage(byte[] identifier, byte[] packet)
        {
            Type messageType = GetClientMessageType($"{Encoding.UTF8.GetString(identifier)}");

            IMessage message;

            try
            {
                message = (IMessage)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(packet), messageType);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                message = null;
            }

            return message;
        }

        public static IMessage ConvertServerPacketToMessage(byte[] identifier, byte[] packet)
        {
            Type messageType = GetServerMessageType($"{Encoding.UTF8.GetString(identifier)}");

            IMessage message;

            try
            {
                message = (IMessage)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(packet), messageType);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                message = null;
            }

            return message;
        }

        public static Type GetClientMessageType(string identifier)
        {
            logger.Trace($"GetClientMessageType: {identifier}");
            if (registeredMessageTypes.ContainsKey($"Client.{identifier}"))
                return registeredMessageTypes[$"Client.{identifier}"];

            return null;
        }

        public static Type GetServerMessageType(string identifier)
        {
            logger.Trace($"GetServerMessageType: {identifier}");
            if (registeredMessageTypes.ContainsKey($"Server.{identifier}"))
                return registeredMessageTypes[$"Server.{identifier}"];

            return null;
        }

        public static void HandleClientMessage(Client client, IMessage message)
        {
            List<object> parameters = new List<object>() { client, message };

            Type t = message.GetType();

            var callback = GetClientMessageHandler(t);

            callback.DynamicInvoke(parameters.ToArray());
        }

        public static void HandleServerMessage(ClientData clientData, IMessage message)
        {
            List<object> parameters = new List<object>() { clientData, message };

            Type t = message.GetType();

            var callback = GetServerMessageHandler(t);

            callback.DynamicInvoke(parameters.ToArray());
        }

        public static void RegisterClientMessageType(Type messageIdentifier, Action<Client, IMessage> callbackMethod)
        {
            registeredMessageTypes.Add($"Client.{messageIdentifier.Name}", messageIdentifier);
            registeredMessageHandlers.Add($"Client.{messageIdentifier.Name}", callbackMethod);
        }

        public static void RegisterServerMessageType(Type messageIdentifier, Action<ClientData, IMessage> callbackMethod)
        {
            registeredMessageTypes.Add($"Server.{messageIdentifier.Name}", messageIdentifier);
            registeredServerMessageHandlers.Add($"Server.{messageIdentifier.Name}", callbackMethod);
        }

        public static void UnregisterClientMessageType(Type messageIdentifier)
        {
            if (registeredMessageTypes.ContainsKey($"Client.{messageIdentifier.Name}"))
                registeredMessageTypes.Remove($"Client.{messageIdentifier.Name}");

            if (registeredMessageHandlers.ContainsKey($"Client.{messageIdentifier.Name}"))
                registeredMessageHandlers.Remove($"Client.{messageIdentifier.Name}");
        }

        public static void UnregisterServerMessageType(Type messageIdentifier)
        {
            if (registeredMessageTypes.ContainsKey($"Server.{messageIdentifier.Name}"))
                registeredMessageTypes.Remove($"Server.{messageIdentifier.Name}");

            if (registeredMessageHandlers.ContainsKey($"Server.{messageIdentifier.Name}"))
                registeredMessageHandlers.Remove($"Server.{messageIdentifier.Name}");
        }

        private static Action<Client, IMessage> GetClientMessageHandler(Type messageIdentifier)
        {
            if (registeredMessageHandlers.ContainsKey($"Client.{messageIdentifier.Name}"))
                return registeredMessageHandlers[$"Client.{messageIdentifier.Name}"];

            return null;
        }

        private static Action<ClientData, IMessage> GetServerMessageHandler(Type messageIdentifier)
        {
            if (registeredServerMessageHandlers.ContainsKey($"Server.{messageIdentifier.Name}"))
                return registeredServerMessageHandlers[$"Server.{messageIdentifier.Name}"];

            return null;
        }
    }
}