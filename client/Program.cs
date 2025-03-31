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
            Thread.Sleep(1000);
            Console.WriteLine();

            InitializeConnection();
            
            // Start protocol flow
            var welcomeMessage = PerformHandshake();
            
            if (welcomeMessage != null && welcomeMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("========== CLIENT HANDSHAKE COMPLETED ==========");
                Console.WriteLine($"CLIENT Server says: {welcomeMessage.Content}");
                Thread.Sleep(1000);
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
        Console.WriteLine("CLIENT Starting client application");

        // Create endpoints using settings file
        IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
        serverEndpoint = new IPEndPoint(serverIPAddress, setting.ServerPortNumber);

        // Create local endpoint for binding
        IPAddress localIPAddress = IPAddress.Parse(setting.ClientIPAddress);
        IPEndPoint localEndPoint = new IPEndPoint(localIPAddress, 0);

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        remoteEP = sender;
        Thread.Sleep(1000);
        Console.WriteLine();

        // Create socket
        Console.WriteLine("CLIENT: Creating and binding socket...");
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(localEndPoint);
        Thread.Sleep(1000);
        Console.WriteLine();
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
        // Valid domains (existing in the server's DNS records)
        string[] validDomains = { "www.sample.com", "www.test.com" };
        
        // Invalid domains (non-existent in the server's DNS records)
        string[] invalidDomains = { "www.abc.com", "www.abcd.com" };
        
        // Process all lookups
        foreach (var domain in validDomains)
        {
            PerformSingleDNSLookup(domain, "A");  // Using A record type
        }
        
        foreach (var domain in invalidDomains)
        {
            PerformSingleDNSLookup(domain, "A");  // Using A record type
        }
    }
    
    private static void PerformSingleDNSLookup(string domain, string recordType)
    {
        Console.WriteLine($"\n========== CLIENT PERFORMING DNS LOOKUP FOR {domain} ==========");
        
        // Create a proper DNSRecord object for lookup
        DNSRecord lookupRecord = new DNSRecord
        {
            Type = recordType,
            Name = domain
        };

        // Create and send DNSLookup message
        int messageId = random.Next(1, 10000);
        Message dnsLookupMessage = new Message
        {
            MsgId = messageId,
            MsgType = MessageType.DNSLookup,
            Content = lookupRecord
        };

        SendMessage(dnsLookupMessage);
        
        // Receive response (could be DNSLookupReply or Error)
        Message? response = ReceiveMessage("DNS Lookup Reply");
        
        if (response != null)
        {
            ProcessDNSResponse(response, messageId);
        }
        else
        {
            Console.WriteLine("CLIENT Error: No response received from server");
        }
    }
    
    private static void ProcessDNSResponse(Message response, int lookupMessageId)
    {
        if (response.MsgType == MessageType.DNSLookupReply)
        {
            Console.WriteLine("========== CLIENT DNS LOOKUP SUCCESSFUL ==========");
            
            // Handle DNS record
            try
            {
                string recordJson = response.Content.ToString();
                DNSRecord? record = JsonSerializer.Deserialize<DNSRecord>(recordJson, options);

                if (record != null)
                {
                    Console.WriteLine($"Type: {record.Type}");
                    Console.WriteLine($"Name: {record.Name}");
                    Console.WriteLine($"Value: {record.Value}");
                    Console.WriteLine($"TTL: {record.TTL}");
                    if (record.Priority != null)
                    {
                        Console.WriteLine($"Priority: {record.Priority}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CLIENT Error parsing DNS record: {ex.Message}");
            }
        }
        else if (response.MsgType == MessageType.Error)
        {
            Console.WriteLine($"CLIENT Error from server: {response.Content}");
        }
        else
        {
            Console.WriteLine($"CLIENT Unexpected response type: {response.MsgType}");
            return;
        }

        // Send Acknowledgment
        SendAcknowledgment(lookupMessageId);
        
        // Wait for End message after the last DNS lookup
        Message? endMessage = ReceiveMessage("End");
        
        if (endMessage != null && endMessage.MsgType == MessageType.End)
        {
            Console.WriteLine("========== CLIENT SESSION ENDED ==========");
        }
    }
    
    private static void SendAcknowledgment(int originalMessageId)
    {
        int messageId = random.Next(1, 10000);
        Message ackMessage = new Message
        {
            MsgId = messageId,
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
        Thread.Sleep(1000);
        Console.WriteLine();
    }
    
    private static Message? ReceiveMessage(string expectedType)
    {
        Console.WriteLine($"CLIENT: Waiting for {expectedType} message...");
        
        int bytesReceived = socket.ReceiveFrom(buffer, ref remoteEP);
        string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
        
        Console.WriteLine($"CLIENT Received from server: {receivedJson}");
        Thread.Sleep(1000);
        Console.WriteLine();
        
        return JsonSerializer.Deserialize<Message>(receivedJson, options);
    }
}