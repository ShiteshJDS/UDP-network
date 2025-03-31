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
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void start()
    {
        byte[] buffer = new byte[1024];
        Socket sock;

        try
        {
            Console.WriteLine("========== CLIENT APPLICATION STARTING ==========");
            Thread.Sleep(3500);
            Console.WriteLine();

            Console.WriteLine("CLIENT Starting client application");

            // Create endpoints using settings file
            IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
            IPEndPoint ServerEndpoint = new IPEndPoint(serverIPAddress, setting.ServerPortNumber);

            // Create local endpoint for binding
            IPAddress localIPAddress = IPAddress.Parse(setting.ClientIPAddress);
            IPEndPoint localEndPoint = new IPEndPoint(localIPAddress, 0);

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEP = (EndPoint)sender;
            Thread.Sleep(3500);
            Console.WriteLine();

            // Create socket
            Console.WriteLine("CLIENT: Creating and binding socket...");
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Bind(localEndPoint);
            Thread.Sleep(3500);
            Console.WriteLine();

            // Create Hello message
            Random random = new Random();
            int messageId = random.Next(1, 1000);
            Message helloMessage = new Message
            {
                MsgId = messageId,
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };

            // Serialize message
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            string jsonMessage = JsonSerializer.Serialize(helloMessage, options);
            byte[] msg = Encoding.UTF8.GetBytes(jsonMessage);
            Thread.Sleep(3500);
            Console.WriteLine();

            // Send Hello message to server
            Console.WriteLine("CLIENT: Sending Hello message...");
            Console.WriteLine($"CLIENT: {jsonMessage}");
            sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
            Thread.Sleep(3500);
            Console.WriteLine();

            // Receive Welcome message
            Console.WriteLine("CLIENT: Waiting for Welcome message...");
            int bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
            string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
            Console.WriteLine($"CLIENT Received from server: {receivedJson}");
            Thread.Sleep(3500);
            Console.WriteLine();

            // Deserialize Welcome message
            Message? welcomeMessage = JsonSerializer.Deserialize<Message>(receivedJson, options);

            // Validate Welcome message
            if (welcomeMessage != null && welcomeMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("========== CLIENT HANDSHAKE COMPLETED ==========");
                Console.WriteLine($"CLIENT Server says: {welcomeMessage.Content}");
                Thread.Sleep(3500);
                Console.WriteLine();

                // DNS Lookup domain
                string domain = "www.sample.com";

                // Create a proper DNSRecord object for lookup
                DNSRecord lookupRecord = new DNSRecord
                {
                    Type = "A",  // Could be "A", "MX", etc.
                    Name = domain
                    // Value, TTL, and Priority are not needed for the lookup request
                };

                // Create DNSLookup message with the DNSRecord object
                Message dnsLookupMessage = new Message
                {
                    MsgId = messageId,
                    MsgType = MessageType.DNSLookup,
                    Content = lookupRecord
                };

                // Serialize and send DNSLookup message
                jsonMessage = JsonSerializer.Serialize(dnsLookupMessage, options);
                msg = Encoding.UTF8.GetBytes(jsonMessage);
                Console.WriteLine($"CLIENT DNSLookup message: {jsonMessage}");
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                Thread.Sleep(3500);
                Console.WriteLine();

                // Receive DNSLookupReply
                Console.WriteLine("CLIENT: Waiting for DNS Lookup Reply...");
                bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
                receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Console.WriteLine($"CLIENT Received from server: {receivedJson}");
                Thread.Sleep(3500);
                Console.WriteLine();

                // Deserialize DNSLookupReply
                Message? dnsReplyMessage = JsonSerializer.Deserialize<Message>(receivedJson, options);

                if (dnsReplyMessage != null)
                {
                    if (dnsReplyMessage.MsgType == MessageType.DNSLookupReply)
                    {
                        Console.WriteLine("========== CLIENT DNS LOOKUP SUCCESSFUL ==========");
                        string recordJson = dnsReplyMessage.Content.ToString();
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

                        // Send Acknowledgment
                        messageId = random.Next(1, 1000);
                        Message ackMessage = new Message
                        {
                            MsgId = messageId,
                            MsgType = MessageType.Ack,
                            Content = dnsLookupMessage.MsgId
                        };

                        jsonMessage = JsonSerializer.Serialize(ackMessage, options);
                        msg = Encoding.UTF8.GetBytes(jsonMessage);
                        Console.WriteLine($"CLIENT Acknowledgment message: {jsonMessage}");
                        sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                        Thread.Sleep(3500);
                        Console.WriteLine();

                        // Receive End message
                        Console.WriteLine("CLIENT: Waiting for End message...");
                        bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
                        receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                        Console.WriteLine($"CLIENT Received from server: {receivedJson}");
                        Thread.Sleep(3500);
                        Console.WriteLine();

                        Message? endMessage = JsonSerializer.Deserialize<Message>(receivedJson, options);
                        if (endMessage != null && endMessage.MsgType == MessageType.End)
                        {
                            Console.WriteLine("========== CLIENT SESSION ENDED ==========");
                        }
                    }
                    else if (dnsReplyMessage.MsgType == MessageType.Error)
                    {
                        Console.WriteLine($"CLIENT Error: {dnsReplyMessage.Content}");
                    }
                }
            }
            else
            {
                Console.WriteLine("CLIENT Error: Expected Welcome message, received wrong message");
            }

            sock.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Socket Error. Terminating: {ex.Message}");
        }
    }
}