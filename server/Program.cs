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

    // DONE: [Read the JSON file and return the list of DNSRecords]
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
        // DONE: [Create a socket and endpoints and bind it to the server IP address and port number]

        // DONE:[Receive and print a received Message from the client]

        // DONE:[Receive and print Hello]

        // DONE:[Send Welcome to the client]

        try
        {
            Console.WriteLine("SERVER Starting server application...");
            
            // Create a socket and endpoints and bind it to the server IP address and port number
            IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
            IPEndPoint serverEndPoint = new IPEndPoint(serverIPAddress, setting.ServerPortNumber);
            
            // Create the UDP socket
            using Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
            // Bind the socket to the server endpoint
            serverSocket.Bind(serverEndPoint);
            
            Console.WriteLine($"SERVER Server bound to {serverEndPoint}, waiting for client connections...");
            
            // Load DNS records
            List<DNSRecord> dnsRecords = ReadDNSRecords();
            Console.WriteLine($"SERVER Loaded {dnsRecords.Count} DNS records");
            
            // Allocate receive buffer
            byte[] receiveBuffer = new byte[1024];
            EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            while (true)
            {
                Console.WriteLine("SERVER Waiting for incoming messages...");
                
                // Receive and print a received Message from the client
                int bytesReceived = serverSocket.ReceiveFrom(receiveBuffer, ref clientEndPoint);
                string receivedJson = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
                
                Console.WriteLine($"SERVER Received from client {clientEndPoint}: {receivedJson}");
                
                // Deserialize the received message
                Message? clientMessage = JsonSerializer.Deserialize<Message>(receivedJson);
                
                if (clientMessage != null)
                {
                    // Handle Hello message
                    if (clientMessage.MsgType == MessageType.Hello)
                    {
                        Console.WriteLine($"SERVER Received Hello from client: {clientMessage.Content}");
                        
                        // Create and send Welcome message
                        Random random = new Random();
                        int messageId = random.Next(1, 10000);
                        
                        Message welcomeMessage = new Message
                        {
                            MsgId = messageId,
                            MsgType = MessageType.Welcome,
                            Content = "Welcome from server"
                        };
                        
                        // Serialize the message to JSON with custom options
                        var options = new JsonSerializerOptions
                        {
                            Converters = { new JsonStringEnumConverter() }
                        };
                        string welcomeJson = JsonSerializer.Serialize(welcomeMessage, options);
                        byte[] sendBuffer = Encoding.UTF8.GetBytes(welcomeJson);
                        
                        // Send Welcome message to the client
                        Console.WriteLine($"SERVER Sending Welcome message: {welcomeJson}");
                        serverSocket.SendTo(sendBuffer, clientEndPoint);
                        
                        Console.WriteLine("SERVER Handshake completed successfully");
                    }
                    else
                    {
                        Console.WriteLine($"SERVER Received unexpected message type: {clientMessage.MsgType}");
                        
                        // Send Error message
                        Message errorMessage = new Message
                        {
                            MsgId = new Random().Next(1, 10000),
                            MsgType = MessageType.Error,
                            Content = "Expected Hello message, received wrong message"
                        };
                        
                        string errorJson = JsonSerializer.Serialize(errorMessage);
                        byte[] sendBuffer = Encoding.UTF8.GetBytes(errorJson);
                        
                        serverSocket.SendTo(sendBuffer, clientEndPoint);
                    }
                }
                else
                {
                    Console.WriteLine("SERVER Received invalid message format");
                }
                
                // TODO:[Receive and print DNSLookup]

                // TODO:[Query the DNSRecord in Json file]

                // TODO:[If found Send DNSLookupReply containing the DNSRecord]

                // TODO:[If not found Send Error]


                // TODO:[Receive Ack about correct DNSLookupReply from the client]


                // TODO:[If no further requests receieved send End to the client]
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SERVER Error: {ex.Message}");
        }

    }


}