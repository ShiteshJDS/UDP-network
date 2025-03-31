using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibData;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}


class ServerUDP
{
    private static readonly string configFile = @"../Setting.json";
    private static readonly string configContent = File.ReadAllText(configFile);
    private static readonly Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    private static readonly JsonSerializerOptions options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    
    private static Socket? socket;
    private static readonly Random random = new();
    private static readonly byte[] buffer = new byte[1024];
    private static List<DNSRecord> dnsRecords = new();

    public static void start()
    {
        try
        {
            Console.WriteLine("========== SERVER APPLICATION STARTING ==========");
            Thread.Sleep(1000);
            Console.WriteLine();

            Console.WriteLine("SERVER Starting server...");

            InitializeSocket();
            LoadDNSRecords();
            
            // Main server loop
            RunServerLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Socket Error. Terminating: {ex.Message}");
        }
    }
    
    private static void InitializeSocket()
    {
        // Create endpoints using settings file
        IPAddress ipAddress = IPAddress.Parse(setting.ServerIPAddress);
        IPEndPoint localEndpoint = new IPEndPoint(ipAddress, setting.ServerPortNumber);

        // Create and bind socket
        Console.WriteLine("SERVER: Creating and binding socket...");
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(localEndpoint);

        Console.WriteLine($"SERVER Server bound to {localEndpoint}, waiting for client connections...");
        Thread.Sleep(1000);
        Console.WriteLine();
    }
    
    private static void LoadDNSRecords()
    {
        Console.WriteLine("SERVER: Loading DNS records...");
        dnsRecords = ReadDNSRecords();
        Console.WriteLine($"SERVER Loaded {dnsRecords.Count} DNS records");
        Thread.Sleep(1000);
        Console.WriteLine();
    }
    
    private static void RunServerLoop()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remoteEP = sender;
        
        while (true)
        {
            Console.WriteLine("\n========== WAITING FOR CLIENT MESSAGE ==========");
            Thread.Sleep(1000);

            // Receive message from client
            Message? clientMessage = ReceiveMessage(ref remoteEP);
            
            if (clientMessage != null)
            {
                ProcessClientMessage(clientMessage, remoteEP);
            }
            else
            {
                Console.WriteLine("SERVER Received invalid message format");
            }
        }
    }
    
    private static void ProcessClientMessage(Message clientMessage, EndPoint remoteEP)
    {
        switch (clientMessage.MsgType)
        {
            case MessageType.Hello:
                HandleHelloMessage(clientMessage, remoteEP);
                break;
                
            case MessageType.DNSLookup:
                HandleDNSLookupMessage(clientMessage, remoteEP);
                break;
                
            case MessageType.Ack:
                HandleAcknowledgmentMessage(clientMessage, remoteEP);
                break;
                
            default:
                HandleUnexpectedMessage(clientMessage, remoteEP);
                break;
        }
    }
    
    private static void HandleHelloMessage(Message clientMessage, EndPoint remoteEP)
    {
        Console.WriteLine($"SERVER Received Hello from client: {clientMessage.Content}");

        // Create and send Welcome message
        int messageId = random.Next(1, 10000);
        Message welcomeMessage = new Message
        {
            MsgId = messageId,
            MsgType = MessageType.Welcome,
            Content = "Welcome from server"
        };

        SendMessage(welcomeMessage, remoteEP);
        Console.WriteLine("========== SERVER HANDSHAKE COMPLETED ==========");
    }
    
    private static void HandleDNSLookupMessage(Message clientMessage, EndPoint remoteEP)
    {
        try
        {
            // Deserialize the Content property into a DNSRecord object
            DNSRecord? lookupRecord = JsonSerializer.Deserialize<DNSRecord>(clientMessage.Content.ToString(), options);

            if (lookupRecord != null)
            {
                string domainName = lookupRecord.Name;
                string recordType = lookupRecord.Type;
                Console.WriteLine($"SERVER Received DNS Lookup for: {domainName} (Type: {recordType})");

                // Find matching DNS record using LINQ
                DNSRecord? matchingRecord = dnsRecords.FirstOrDefault(r => 
                    r.Name == domainName && r.Type == recordType);

                if (matchingRecord != null)
                {
                    SendDNSLookupReply(clientMessage.MsgId, matchingRecord, remoteEP);
                }
                else
                {
                    SendErrorMessage($"Domain {domainName} not found", remoteEP);
                }
            }
            else
            {
                Console.WriteLine("SERVER Error: Invalid DNS Lookup message content");
                SendErrorMessage("Invalid DNS Lookup format", remoteEP);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error processing DNS Lookup: {ex.Message}");
            SendErrorMessage("Error processing DNS request", remoteEP);
        }
    }
    
    private static void HandleAcknowledgmentMessage(Message clientMessage, EndPoint remoteEP)
    {
        Console.WriteLine($"SERVER Received Acknowledgment for message ID: {clientMessage.Content}");

        // Send End message
        SendEndMessage(remoteEP);
        Console.WriteLine("========== SERVER SESSION COMPLETED ==========");
    }
    
    private static void HandleUnexpectedMessage(Message clientMessage, EndPoint remoteEP)
    {
        Console.WriteLine($"SERVER Received unexpected message type: {clientMessage.MsgType}");
        SendErrorMessage("Unexpected message type", remoteEP);
    }
    
    private static void SendDNSLookupReply(int originalMsgId, DNSRecord record, EndPoint remoteEP)
    {
        Message dnsReplyMessage = new Message
        {
            MsgId = originalMsgId, // Use same message ID as request
            MsgType = MessageType.DNSLookupReply,
            Content = record
        };

        SendMessage(dnsReplyMessage, remoteEP, "DNSLookupReply");
    }
    
    private static void SendErrorMessage(string errorContent, EndPoint remoteEP)
    {
        Message errorMessage = new Message
        {
            MsgId = random.Next(1, 10000),
            MsgType = MessageType.Error,
            Content = errorContent
        };

        SendMessage(errorMessage, remoteEP, "Error");
    }
    
    private static void SendEndMessage(EndPoint remoteEP)
    {
        Message endMessage = new Message
        {
            MsgId = random.Next(1, 10000),
            MsgType = MessageType.End,
            Content = "Session completed"
        };

        SendMessage(endMessage, remoteEP, "End");
    }
    
    private static Message? ReceiveMessage(ref EndPoint remoteEP)
    {
        Console.WriteLine("SERVER: Waiting for client message...");
        int bytesReceived = socket.ReceiveFrom(buffer, ref remoteEP);
        string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
        
        Console.WriteLine($"SERVER Received from client {remoteEP}: {receivedJson}");
        Thread.Sleep(1000);
        Console.WriteLine();
        
        try
        {
            return JsonSerializer.Deserialize<Message>(receivedJson, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error deserializing message: {ex.Message}");
            return null;
        }
    }
    
    private static void SendMessage(Message message, EndPoint remoteEP, string messageType = "")
    {
        string type = string.IsNullOrEmpty(messageType) ? message.MsgType.ToString() : messageType;
        string messageJson = JsonSerializer.Serialize(message, options);
        byte[] msg = Encoding.UTF8.GetBytes(messageJson);
        
        Console.WriteLine($"SERVER Sending {type} message: {messageJson}");
        socket.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
        Thread.Sleep(1000);
        Console.WriteLine();
    }

    // Read DNS records from file
    public static List<DNSRecord> ReadDNSRecords()
    {
        try
        {
            string dnsRecordsFile = @"DNSrecords.json";
            string dnsRecordsContent = File.ReadAllText(dnsRecordsFile);
            List<DNSRecord>? dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsRecordsContent);
            return dnsRecords ?? new List<DNSRecord>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error reading DNS records: {ex.Message}");
            return new List<DNSRecord>();
        }
    }
}