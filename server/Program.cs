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
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

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



   public static void start()
    {
        byte[] buffer = new byte[1024];
        Socket sock;
        
        try
        {

            Console.WriteLine("========== SERVER APPLICATION STARTING ==========");
            Thread.Sleep(3500);
            Console.WriteLine();

            Console.WriteLine("SERVER Starting server...");
            
            // Create endpoints using settings file as in original
            IPAddress ipAddress = IPAddress.Parse(setting.ServerIPAddress);
            IPEndPoint localEndpoint = new IPEndPoint(ipAddress, setting.ServerPortNumber);
            
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEP = (EndPoint)sender;
            
            // Create and bind socket
            Console.WriteLine("SERVER: Creating and binding socket...");
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Bind(localEndpoint);
            
            Console.WriteLine($"SERVER Server bound to {localEndpoint}, waiting for client connections...");
            Thread.Sleep(3500);
            Console.WriteLine();

            // Load DNS records
            Console.WriteLine("SERVER: Loading DNS records...");
            List<DNSRecord> dnsRecords = ReadDNSRecords();
            Console.WriteLine($"SERVER Loaded {dnsRecords.Count} DNS records");
            Thread.Sleep(3500);
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("\n========== WAITING FOR CLIENT MESSAGE ==========");
                Thread.Sleep(3500);
                
                // Receive message from client
                Console.WriteLine("SERVER: Waiting for client message...");
                int bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
                string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Console.WriteLine($"SERVER Received from client {remoteEP}: {receivedJson}");
                Thread.Sleep(3500);
                Console.WriteLine();

                // Deserialize message
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                Message? clientMessage = JsonSerializer.Deserialize<Message>(receivedJson, options);
                
                if (clientMessage != null)
                {
                    if (clientMessage.MsgType == MessageType.Hello)
                    {
                        Console.WriteLine($"SERVER Received Hello from client: {clientMessage.Content}");
                        
                        // Create Welcome message
                        Random random = new Random();
                        int messageId = random.Next(1, 10000);
                        Message welcomeMessage = new Message
                        {
                            MsgId = messageId,
                            MsgType = MessageType.Welcome,
                            Content = "Welcome from server"
                        };
                        
                        // Serialize and send Welcome message
                        string welcomeJson = JsonSerializer.Serialize(welcomeMessage, options);
                        byte[] msg = Encoding.UTF8.GetBytes(welcomeJson);                        
                        Console.WriteLine($"SERVER Sending Welcome message: {welcomeJson}");
                        sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
                        Thread.Sleep(3500);
                        Console.WriteLine();

                        Console.WriteLine("========== SERVER HANDSHAKE COMPLETED ==========");
                    }
                    else if (clientMessage.MsgType == MessageType.DNSLookup)
                    {
                        string domainName = clientMessage.Content.ToString();
                        Console.WriteLine($"SERVER Received DNS Lookup for: {domainName}");
                        
                        // Find matching DNS record, through list with LINQ
                        DNSRecord matchingRecord = dnsRecords.FirstOrDefault(r => r.Name == domainName);

                        if (matchingRecord != null)
                        {
                            // Create DNSLookupReply with the found record                            
                            Message dnsReplyMessage = new Message
                            {
                                MsgId = new Random().Next(1, 10000),
                                MsgType = MessageType.DNSLookupReply,
                                Content = matchingRecord
                            };
                            
                            string replyJson = JsonSerializer.Serialize(dnsReplyMessage, options);
                            byte[] msg = Encoding.UTF8.GetBytes(replyJson);
                            
                            Console.WriteLine($"SERVER Sending DNSLookupReply: {replyJson}");
                            sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
                        }
                        else
                        {
                            // Send Error message if record not found
                            Message errorMessage = new Message
                            {
                                MsgId = new Random().Next(1, 10000),
                                MsgType = MessageType.Error,
                                Content = $"Domain not found"
                            };
                            
                            string errorJson = JsonSerializer.Serialize(errorMessage, options);
                            byte[] msg = Encoding.UTF8.GetBytes(errorJson);
                            
                            Console.WriteLine($"SERVER Sending Error message: {errorJson}");
                            sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
                        }
                        Thread.Sleep(3500);
                        Console.WriteLine();
                    }
                    else if (clientMessage.MsgType == MessageType.Ack)
                    {
                        Console.WriteLine($"SERVER Received Acknowledgment: {clientMessage.Content}");
                        
                        // Send End message
                        Message endMessage = new Message
                        {
                            MsgId = new Random().Next(1, 10000),
                            MsgType = MessageType.End,
                            Content = "Session completed"
                        };
                        
                        string endJson = JsonSerializer.Serialize(endMessage, options);
                        byte[] msg = Encoding.UTF8.GetBytes(endJson);
                        
                        Console.WriteLine($"SERVER Sending End message: {endJson}");
                        sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
                        Thread.Sleep(3500);
                        Console.WriteLine();

                        Console.WriteLine("========== SERVER SESSION COMPLETED ==========");
                    }
                    else
                    {
                        Console.WriteLine($"SERVER Received unexpected message type: {clientMessage.MsgType}");
                        
                        // Send Error message
                        Message errorMessage = new Message
                        {
                            MsgId = new Random().Next(1, 10000),
                            MsgType = MessageType.Error,
                            Content = "Unexpected message type"
                        };
                        
                        string errorJson = JsonSerializer.Serialize(errorMessage, options);
                        byte[] msg = Encoding.UTF8.GetBytes(errorJson);
                        Console.WriteLine($"SERVER: Sending Error message: {errorJson}");
                        sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
                    }
                }
                else
                {
                    Console.WriteLine("SERVER Received invalid message format");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Socket Error. Terminating: {ex.Message}");
        }
    }
}