# BifrostExtended
Extension library for Bifrost the Lightweight experimental cryptoprotocol :lock: :key:

## Bifrost Original Disclaimer
I'm just an amateur who's interested in cryptography and networking. This protocol or its implementation may be heavily flawed, and I promise absolutely no expectation of security. If you're designing a security critical application, please consider using a mature and well-documented cryptoprotocol such as TLS. Thank you.

## What is BifrostExtended?
BifrostExtended is an extension of Bifrost that hopes to create a seamless client/server library with strong encryption and easy setup. Seriously, ease of use is the #1 priority. With BE, multi-user servers are easy to create with a few lines of code as it leverages Bifrost and BouncyCastle to do the heavy lifting. The server and client is multi-threaded so you don't have to worry about performance. In addition, a simple Message system has been implemented that allows transparent serializing/deserializing network packets as well as adds extra features such as Authentication levels and Certificate storage.

## What is the original Bifrost?
Bifrost is a cryptoprotocol, designed to be reliable, secure, lightweight and easy to understand. The whole library is around 1k lines of fully documented C#. Bifrost was designed in response to TLS, which has a very long and verbose specification document. In contrast, Bifrost is very easy to understand and doesn't require much effort to set up.

## Cryptographic primitives
Bifrost mostly depends on the excellent [BouncyCastle](http://bouncycastle.org/) library to do crypto. Since version 0.3, Bifrost has been able to do cipher selection, click [here](https://github.com/hexafluoride/Bifrost/wiki/Cipher-suites) to view a list of available cipher suites.

## Public key infrastructure
Since Bifrost is designed to be simple, it has its own PKI designed around PEM keypairs and raw signature files.

## Message format
Read more about Bifrost's message format in the [wiki](https://github.com/hexafluoride/Bifrost/wiki/Message-format).

## Simple example
Server side:

``` csharp

		static void Main(string[] args)
        {
				//Register our 'ChatMessage' packet handlers
            Handler.RegisterServerMessageType(typeof(ChatMessage), HandleChatMessage);

				//Create a new Server with a 100 connection limit
            server = new Server(100);
				//Remember unknown certificates. If false, the Certificate must already be known.
            server.RememberCertificates = true;
				//Start the server on port 8888
            server.Start(8888);
				//Register the Event handlers, not shown in this example
            server.OnServerDataReceived += Server_OnServerDataReceived;
            server.OnUserConnected += Server_OnUserConnected;

				//Read console input and send a message
            while (true)
            {
                string input = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(input))
                    continue;

                server.BroadcastMessage(new ChatMessage("Server", input), AuthState.Unauthenticated);
                Console.WriteLine("You said: " + input);
            }
        }

		private static void HandleChatMessage(ClientData client, IMessage message)
        {
				//Convert the Message received to the corret message type
            ChatMessage chatMessage = (ChatMessage)message;

				//Print out the message contents
            Console.WriteLine(chatMessage.user + " says: " + chatMessage.message);

				//broadcast the received ChatMessage to all 'Unauthenticated' (or greater connected users) except the original client
            server.BroadcastMessage(message, AuthState.Unauthenticated, client);
        }
```

Client:
``` csharp

			//Create our Client class, provided from BifrostExtended
        static Client client;
		static void Main(string[] args)
        {
				//Register our 'ChatMessage' packet handlers
            Handler.RegisterClientMessageType(typeof(ChatMessage), HandleChatMessage);

				//Initialize a new Client()
            client = new Client();
				//Enable Automatic Reconnection
            client.AutoReconnect = true;
				//Remember unknown certificates. If false, the Certificate must already be known.
            client.RememberCertificates = true;
				//Register the Event handlers, not shown in this example
            client.OnClientConnectionChange += Client_OnClientConnectionChange;
            client.OnClientDataReceived += Client_OnClientDataReceived;
				//Connect to server at 127.0.0.1 on port 8888
            client.Connect("127.0.0.1", 8888);

				//Get console input and send a new ChatMessage
            while (true)
            {
                string input = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(input))
                    continue;

                client.SendMessage(new ChatMessage("Client", input));
                Console.WriteLine("You said: " + input);
            }
        }
		
			//handle all ChatMessages sent from the Server
		private static void HandleChatMessage(Client client, IMessage message)
        {
            ChatMessage chatMessage = (ChatMessage)message;

            Console.WriteLine(chatMessage.user + " says: " + chatMessage.message);
        }
```

ChatMessage message type:
``` csharp
public class ChatMessage : IMessage
    {
        public string user;
        public string message;

        public ChatMessage(string user, string message)
        {
            this.user = user;
            this.message = message;
        }
    }
```