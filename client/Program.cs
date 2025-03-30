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

    //TODO: [Deserialize Setting.json]
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void start()
    {

        try
        {
            Console.WriteLine("CLIENT Starting client application");
            
            // Create endpoints and socket
            IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
            IPEndPoint serverEndPoint = new IPEndPoint(serverIPAddress, setting.ServerPortNumber);
            
            // Create the UDP socket
            using Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
            // Create a local endpoint for binding
            IPAddress localIPAddress = IPAddress.Parse(setting.ClientIPAddress);
            IPEndPoint localEndPoint = new IPEndPoint(localIPAddress, 0); // Use port 0 to get any available port
            clientSocket.Bind(localEndPoint);
            
            Console.WriteLine($"CLIENT connected to {localEndPoint}, connecting to server at {serverEndPoint}");
            
            // Create and send HELLO message
            Random random = new Random();
            int messageId = random.Next(1, 1000); // Generate random message ID
            
            Message helloMessage = new Message
            {
                MsgId = messageId,
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };
            
            // Serialize the message to JSON
            string jsonMessage = JsonSerializer.Serialize(helloMessage);
            byte[] sendBuffer = Encoding.UTF8.GetBytes(jsonMessage);
            
            // Send the Hello message to the server
            Console.WriteLine($"CLIENT Hello message: {jsonMessage}");
            clientSocket.SendTo(sendBuffer, serverEndPoint);
            
            // Receive and print Welcome message from server
            byte[] receiveBuffer = new byte[1024];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            int bytesReceived = clientSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
            string receivedJson = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
            
            Console.WriteLine($"CLIENT Received from server: {receivedJson}");
            
            // Deserialize the received Welcome message
            Message? welcomeMessage = JsonSerializer.Deserialize<Message>(receivedJson);
            
            // Validate the Welcome message
            if (welcomeMessage != null && welcomeMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("CLIENT Handshake completed");
                Console.WriteLine($"CLIENT Server says: {welcomeMessage.Content}");
            }
            else
            {
                Console.WriteLine("CLIENT Error: Expected Welcome message, recieved wrong message");
            }
            
            // TODO: [Create and send DNSLookup Message]


            //TODO: [Receive and print DNSLookupReply from server]


            //TODO: [Send Acknowledgment to Server]

            // TODO: [Send next DNSLookup to server]
            // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

            //TODO: [Receive and print End from server]
            
            // Close the socket
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT] Error: {ex.Message}");
        }






    }
}