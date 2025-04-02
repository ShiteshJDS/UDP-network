using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    private static readonly string configFile = @"../Setting.json";
    private static readonly string configContent = File.ReadAllText(configFile);
    private static readonly Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    private static readonly JsonSerializerOptions options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    
    private static Socket? socket;
    private static IPEndPoint? serverEndpoint;
    private static EndPoint? remoteEP;
    private static readonly Random random = new();
    private static readonly byte[] buffer = new byte[1024];

    public static void start()
    {
        try
        {
            Console.WriteLine("========== CLIENT APPLICATION STARTING ==========");
            Thread.Sleep(3000);
            Console.WriteLine();

            InitializeConnection();
            
            // Start protocol flow
            var welcomeMessage = PerformHandshake();
            
            if (welcomeMessage != null && welcomeMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("========== CLIENT HANDSHAKE COMPLETED ==========");
                Console.WriteLine($"CLIENT Server says: {welcomeMessage.Content}");
                Thread.Sleep(3000);
                Console.WriteLine();
                
                // Perform multiple DNS lookups as required
                PerformDNSLookups();
            }
            else
            {
                Console.WriteLine("CLIENT Error: Expected Welcome message, received wrong message");
            }

            socket?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Socket Error. Terminating: {ex.Message}");
        }
    }
    
    private static void InitializeConnection()
    {
        try // error handling: socket creation
        {
            Console.WriteLine("CLIENT Starting client application");

            IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
            serverEndpoint = new IPEndPoint(serverIPAddress, setting.ServerPortNumber);

            IPAddress localIPAddress = IPAddress.Parse(setting.ClientIPAddress);
            IPEndPoint localEndPoint = new IPEndPoint(localIPAddress, 0);

            remoteEP = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("CLIENT: Creating and binding socket...");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CLIENT Error initializing connection: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    private static Message? PerformHandshake()
    {
        // Create and send Hello message
        int messageId = random.Next(1, 1000);
        Message helloMessage = new Message
        {
            MsgId = messageId,
            MsgType = MessageType.Hello,
            Content = "Hello from client"
        };

        SendMessage(helloMessage);
        
        // Receive Welcome message
        return ReceiveMessage("Welcome");
    }
    
    private static void PerformDNSLookups()
    {
        string[] domains = { "www.sample.com", "www.test.com", "www.abc.com", "www.abcd.com" };
        string[] recordTypes = { "A", "A", "A", "A" };

        for (int i = 0; i < domains.Length; i++)
        {
            PerformSingleDNSLookup(domains[i], recordTypes[i]);
        }

        // Wait for the End message
        try
        {
            Message? endMessage = ReceiveMessage("End");
            if (endMessage != null && endMessage.MsgType == MessageType.End)
            {
                Console.WriteLine("========== CLIENT SESSION ENDED ==========");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CLIENT Error receiving End message: {ex.Message}");
        }
    }
    
    private static void PerformSingleDNSLookup(string domain, string recordType)
    {
        Console.WriteLine($"\n========== CLIENT PERFORMING DNS LOOKUP FOR {domain} ==========");

        // Create and send DNSLookup message
        int messageId = random.Next(1, 10000);
        DNSRecord lookupRecord = new DNSRecord { Type = recordType, Name = domain };
        Message dnsLookupMessage = new Message
        {
            MsgId = messageId,
            MsgType = MessageType.DNSLookup,
            Content = lookupRecord
        };

        SendMessage(dnsLookupMessage);

        // Receive response (DNSLookupReply or Error)
        Message? response = ReceiveMessage("DNSLookupReply");
        if (response != null && response.MsgType == MessageType.DNSLookupReply)
        {
            Console.WriteLine();
            Console.WriteLine("========== CLIENT DNS LOOKUP SUCCESSFUL ==========");
            Console.WriteLine();

            Console.WriteLine($"Response: {response.Content}");

            // Send Ack message
            SendAcknowledgment(messageId);
        }
        else if (response != null && response.MsgType == MessageType.Error)
        {
            Console.WriteLine($"CLIENT Error from server: {response.Content}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("========== CLIENT DNS LOOKUP FAILURE ==========");
            Console.WriteLine();

        }
    }
    
    private static void SendAcknowledgment(int originalMessageId)
    {
        Message ackMessage = new Message
        {
            MsgId = random.Next(1, 10000),
            MsgType = MessageType.Ack,
            Content = originalMessageId
        };

        SendMessage(ackMessage, "Acknowledgment");
    }
    
    private static void SendMessage(Message message, string messageType = "")
    {
        string type = string.IsNullOrEmpty(messageType) ? message.MsgType.ToString() : messageType;
        Console.WriteLine($"CLIENT: Sending {type} message...");
        
        string jsonMessage = JsonSerializer.Serialize(message, options);
        byte[] msg = Encoding.UTF8.GetBytes(jsonMessage);
        
        Console.WriteLine($"CLIENT: {jsonMessage}");
        socket.SendTo(msg, msg.Length, SocketFlags.None, serverEndpoint);
        Thread.Sleep(3000);
        Console.WriteLine();
    }
    
    private static Message? ReceiveMessage(string expectedType)
    {
        try // error handling unexpected / invalid message types
        {
            Console.WriteLine($"CLIENT: Waiting for {expectedType} message...");
            Console.WriteLine();
            int bytesReceived = socket.ReceiveFrom(buffer, ref remoteEP);
            string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

            Console.WriteLine($"CLIENT Received from server: {receivedJson}");
            var message = JsonSerializer.Deserialize<Message>(receivedJson, options);

            if (message == null || message.MsgType.ToString() != expectedType)
            {
                Console.WriteLine($"CLIENT Error: Unexpected or invalid message type. Expected: {expectedType}, Received: {message?.MsgType}");
                return null;
            }

            return message;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CLIENT Error receiving message: {ex.Message}");
            return null;
        }
    }

    // Add a separate method to check for End message
    private static void CheckForEndMessage()
    {
        try
        {
            // Check if there's a message waiting with a shorter timeout
            socket.ReceiveTimeout = 1000; // 1 second timeout
            Message? endMessage = ReceiveMessage("End");
            
            if (endMessage != null && endMessage.MsgType == MessageType.End)
            {
                Console.WriteLine("========== CLIENT SESSION ENDED ==========");
                // Exit the application
                Environment.Exit(0);
            }
            
            // Reset timeout to default
            socket.ReceiveTimeout = 0;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            // No message available, continue with next lookup
            socket.ReceiveTimeout = 0;
        }
    }
}