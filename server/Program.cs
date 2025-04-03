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
using System.Timers;
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

    // Session timeout management
    private static System.Timers.Timer? sessionTimer;
    private static readonly int SESSION_TIMEOUT_MS = 10000; // 10 seconds
    private static EndPoint? activeClientEndPoint = null;
    private static bool sessionActive = false;

    public static void start()
    {
        try
        {
            Console.WriteLine("========== SERVER APPLICATION STARTING ==========");
            Thread.Sleep(3000);
            Console.WriteLine();

            Console.WriteLine("SERVER Starting server...");

            InitializeSocket();
            LoadDNSRecords();
            InitializeSessionTimer();

            // Loop voor de main server
            RunServerLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Socket Error. Terminating: {ex.Message}");
        }
    }

    private static void InitializeSocket()
    {
        try // error handling: socket creation
        {
            IPAddress ipAddress = IPAddress.Parse(setting.ServerIPAddress);
            IPEndPoint localEndpoint = new IPEndPoint(ipAddress, setting.ServerPortNumber);

            Console.WriteLine("SERVER: Creating and binding socket...");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndpoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error initializing socket: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void LoadDNSRecords()
    {
        Console.WriteLine("SERVER: Loading DNS records...");
        dnsRecords = ReadDNSRecords();
        Console.WriteLine($"SERVER Loaded {dnsRecords.Count} DNS records");
        Thread.Sleep(3000);
        Console.WriteLine();
    }

    private static void InitializeSessionTimer()
    {
        sessionTimer = new System.Timers.Timer(SESSION_TIMEOUT_MS);
        sessionTimer.Elapsed += OnSessionTimeout;
        sessionTimer.AutoReset = false;
    }

    private static void StartSessionTimer()
    {
        sessionTimer?.Stop();
        sessionTimer?.Start();
    }

    private static void StopSessionTimer()
    {
        sessionTimer?.Stop();
    }

    private static void OnSessionTimeout(object? sender, ElapsedEventArgs e)
    {
        if (sessionActive && activeClientEndPoint != null)
        {
            Console.WriteLine($"SERVER: Session timeout after {SESSION_TIMEOUT_MS / 1000} seconds of inactivity");
            SendEndMessage(activeClientEndPoint);
            Console.WriteLine("========== SERVER SESSION COMPLETED (TIMEOUT) ==========");
            sessionActive = false;
            activeClientEndPoint = null;
        }
    }

    private static void RunServerLoop()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remoteEP = sender;

        while (true)
        {
            Console.WriteLine("\n========== WAITING FOR CLIENT MESSAGE ==========");
            Console.WriteLine();

            Thread.Sleep(3000);

            // Ontvang message van client
            Message? clientMessage = ReceiveMessage(ref remoteEP);

            if (clientMessage != null)
            {
                // Update client session en reset timer
                if (!sessionActive)
                {
                    sessionActive = true;
                    activeClientEndPoint = remoteEP;
                }

                // Reset de session timer als we een message ontvangen
                StartSessionTimer();

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
            case MessageType.End:
                HandleEndMessage(clientMessage, remoteEP);
                break;
            default:
                Console.WriteLine($"SERVER Error: Unexpected message type: {clientMessage.MsgType}");
                SendErrorMessage("Unexpected message type", remoteEP);
                break;
        }
    }

    private static void HandleEndMessage(Message clientMessage, EndPoint remoteEP)
    {
        Console.WriteLine($"SERVER Received End message: {clientMessage.Content}");
        Console.WriteLine();
        SendEndMessage(remoteEP);
    }

    private static void HandleHelloMessage(Message clientMessage, EndPoint remoteEP)
    {
        Console.WriteLine($"SERVER Received Hello from client: {clientMessage.Content}");
        Console.WriteLine();

        // Create en send Welcome message
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
            DNSRecord? lookupRecord = JsonSerializer.Deserialize<DNSRecord>(clientMessage.Content.ToString(), options);
            if (lookupRecord != null && !string.IsNullOrEmpty(lookupRecord.Type) && !string.IsNullOrEmpty(lookupRecord.Name))
            {
                Console.WriteLine($"SERVER Received DNS Lookup for: {lookupRecord.Name} (Type: {lookupRecord.Type})");

                DNSRecord? matchingRecord = dnsRecords.FirstOrDefault(r =>
                    r.Name == lookupRecord.Name && r.Type == lookupRecord.Type);

                if (matchingRecord != null)
                {
                    SendDNSLookupReply(clientMessage.MsgId, matchingRecord, remoteEP);
                }
                else
                {
                    Console.WriteLine($"SERVER Reason: No matching DNS record found for {lookupRecord.Name} with type {lookupRecord.Type}");
                    SendErrorMessage($"Domain {lookupRecord.Name} not found", remoteEP);
                }
            }
            else
            {
                Console.WriteLine("SERVER Reason: Invalid DNS Lookup format");
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
        Console.WriteLine();
    }

    private static void SendDNSLookupReply(int originalMsgId, DNSRecord record, EndPoint remoteEP)
    {
        Message dnsReplyMessage = new Message
        {
            MsgId = originalMsgId, // Gebruik dezelfde msgId als de original message
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

        // End de session
        StopSessionTimer();
        sessionActive = false;
        activeClientEndPoint = null;
    }

    private static Message? ReceiveMessage(ref EndPoint remoteEP)
    {
        try // error handling: invalid of incomplete message
        {
            Console.WriteLine("SERVER: Waiting for client message...");
            int bytesReceived = socket.ReceiveFrom(buffer, ref remoteEP);
            string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

            Console.WriteLine($"SERVER Received from client {remoteEP}: {receivedJson}");
            Console.WriteLine();
            var message = JsonSerializer.Deserialize<Message>(receivedJson, options);

            if (message == null || !Enum.IsDefined(typeof(MessageType), message.MsgType))
            {
                Console.WriteLine("SERVER Error: Invalid or incomplete message received.");
                return null;
            }

            return message;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error receiving message: {ex.Message}");
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
        Thread.Sleep(3000);
        Console.WriteLine();
    }

    // Haal alle DNS records op uit het JSON bestand
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