# BifrostExtended
Extension library utilizing [BifrostLSF](https://github.com/LostSoulfly/BifrostLSF), which is a modification of the original [Bifrost, by hexafluoride](https://github.com/hexafluoride/Bifrost/).

## I'm not a professional
Neither myself or hexafluoride are professionals with anything more than hobbyist experience in networking or cryptography and we do not provide any warranty or guarantee that your data is safe when using BifrostExtended or Bifrost.

## What is BifrostExtended?
BifrostExtended adds additional features when used along side of [BifrostLSF](https://github.com/LostSoulfly/BifrostLSF) that hopes to create a seamless client/server library with strong encryption and easy setup. Seriously, ease of use is the #1 priority. With BE, multi-user servers are easy to create with a few lines of code as it leverages Bifrost and BouncyCastle to do the heavy lifting. The server and client is multi-threaded so as to not slow you down. In addition, a simple Message system has been implemented that allows transparent serializing/deserializing network packets as well as adds extras such as Authentication and Privilege levels.

## Public key infrastructure
BifrostExtended generates all certificates as required on both the client and server. Initially, it will generate a public/private key that is saved to the program directory. These keys are not used in the usual key exchange and thus do not change (unless the files are deleted). Instead, they are used to sign and verify subsequent keys that are generated for each new connection.

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
	while (server.IsRunning)
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
	while (client.IsConnected)
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

'ChatMessage' network packet class:
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
