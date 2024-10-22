using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Client;

public class Client
{
    
    private readonly IPAddress _clientAddress;
    private readonly string _serverIP;
    private readonly int _serverPort;
    private readonly int BUFFER_SIZE = 4096;
    private byte[] _buffer;
    private Socket _clientSocket;

    private string _clientMessageHeader;

    public Client(string serverIP, int serverPort)
    {
        NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces(); //get all network interfaces
        IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName()); // get all IP addresses based on the local host name

        foreach (NetworkInterface adapter in adapters) //for each Network interface in addapters
        {
            IPInterfaceProperties properties = adapter.GetIPProperties(); // get the ip properties from the adapter and store them into properties
            foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses) // for each UnicastIPAddressInformation in the IPInterfaceProperties Unicast address( this assocaites the IP address with the correct adapter)
            {
                //if the operationalStatus of the adapter is up and the ip Address family is in the Internwork
                //if ((adapter.Name == "Ethernet 2") && (ip.Address.AddressFamily == AddressFamily.InterNetwork)) //test against the name of the adapter you want to get
                if (this._clientAddress == null && (ip.Address.AddressFamily == AddressFamily.InterNetwork)) //test against the name of the adapter you want to get
                {
                    _clientAddress = ip.Address;
                }//end if
            }//end inner for, the UnicastIPAddressInformation for
        }

        _serverIP = serverIP;
        _serverPort = serverPort;
        _buffer = new byte[BUFFER_SIZE];
    }

    public async Task ConnectAsync()
    {
        try
        {
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
           
            this._clientSocket.Bind(new IPEndPoint(_clientAddress, 0));

            await _clientSocket.ConnectAsync(_serverIP, _serverPort);
            Console.WriteLine("Connected to server.");

            // Handle sending and receiving messages
            await HandleCommunicationAsync();
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Error connecting to server: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private async Task HandleCommunicationAsync()
    {
        try
        {
            while (_clientSocket.Connected)
            {
                Console.Write("Enter message to send (or type 'file' to send a file): ");
                string message = Console.ReadLine();

                if (message == "file")
                {
                    await HandleFileTransferAsync();
                }
                else
                {
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    await _clientSocket.SendAsync(messageBytes, SocketFlags.None);
                    Console.WriteLine("Message sent to server.");

                    // Receive response from the server
                    int bytesReceived = await _clientSocket.ReceiveAsync(_buffer, SocketFlags.None);
                    if (bytesReceived == 0)
                    {
                        Console.WriteLine("Server disconnected.");
                        break;
                    }

                    string serverResponse = Encoding.UTF8.GetString(_buffer, 0, bytesReceived);
                    Console.WriteLine($"Server: {serverResponse}");
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Error communicating with server: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        finally
        {
            // Ensure the socket is closed after communication or an error occurs
            Console.WriteLine("Closing connection to server.");
            _clientSocket.Shutdown(SocketShutdown.Both);
            _clientSocket.Close();
        }
    }

    private async Task HandleFileTransferAsync()
    {
        try
        {
            Console.Write("Enter the file path: ");
            string filePath = Console.ReadLine();

            // Read file into byte array
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string fileTransferMessage = $"FILE`{filePath}`{fileBytes.Length}";
            byte[] fileTransferHeader = Encoding.UTF8.GetBytes(fileTransferMessage);

            // Send file header (filename and size) to server
            await _clientSocket.SendAsync(fileTransferHeader, SocketFlags.None);
            Console.WriteLine("File header sent to server.");

            // Send file content
            await _clientSocket.SendAsync(fileBytes, SocketFlags.None);
            //_clientSocket.SendFileAsync(filePath);
            Console.WriteLine("File sent to server.");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Error transferring file to server: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}

//public class Client
//{
//    private static readonly int BUFFER_SIZE = 4096;
//    private static byte[] _buffer = new byte[BUFFER_SIZE];
//    private static string _serverIP = "127.0.0.1";  // Replace with actual server IP
//    private static int _serverPort = 8080;         // Replace with actual server port

//    public static async Task Execute()
//    {
//        try
//        {
//            using (Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
//            {
//                await clientSocket.ConnectAsync(_serverIP, _serverPort);
//                Console.WriteLine("Connected to server.");

//                // Handle sending and receiving messages
//                await HandleCommunicationAsync(clientSocket);
//            }
//        }
//        catch (SocketException ex)
//        {
//            Console.WriteLine($"Error connecting to server: {ex.Message}");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"An error occurred: {ex.Message}");
//        }
//    }

//    private static async Task HandleCommunicationAsync(Socket clientSocket)
//    {
//        try
//        {
//            while (clientSocket.Connected)
//            {
//                Console.Write("Enter message to send (or type 'file' to send a file): ");
//                string message = Console.ReadLine();

//                if (message == "file")
//                {
//                    await HandleFileTransferAsync(clientSocket);
//                }
//                else
//                {
//                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
//                    await clientSocket.SendAsync(messageBytes, SocketFlags.None);
//                    Console.WriteLine("Message sent to server.");

//                    // Receive response from the server
//                    int bytesReceived = await clientSocket.ReceiveAsync(_buffer, SocketFlags.None);
//                    if (bytesReceived == 0)
//                    {
//                        Console.WriteLine("Server disconnected.");
//                        break;
//                    }

//                    string serverResponse = Encoding.UTF8.GetString(_buffer, 0, bytesReceived);
//                    Console.WriteLine($"Server: {serverResponse}");
//                }
//            }
//        }
//        catch (SocketException ex)
//        {
//            Console.WriteLine($"Error communicating with server: {ex.Message}");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"An error occurred: {ex.Message}");
//        }
//        finally
//        {
//            // Ensure the socket is closed after communication or an error occurs
//            Console.WriteLine("Closing connection to server.");
//            clientSocket.Shutdown(SocketShutdown.Both);
//            clientSocket.Close();
//        }
//    }

//    private static async Task HandleFileTransferAsync(Socket clientSocket)
//    {
//        try
//        {
//            Console.Write("Enter the file path: ");
//            string filePath = Console.ReadLine();

//            // Read file into byte array
//            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
//            string fileTransferMessage = $"FILE:{filePath}:{fileBytes.Length}";
//            byte[] fileTransferHeader = Encoding.UTF8.GetBytes(fileTransferMessage);

//            // Send file header (filename and size) to server
//            await clientSocket.SendAsync(fileTransferHeader, SocketFlags.None);
//            Console.WriteLine("File header sent to server.");

//            // Send file content
//            await clientSocket.SendAsync(fileBytes, SocketFlags.None);
//            Console.WriteLine("File sent to server.");
//        }
//        catch (SocketException ex)
//        {
//            Console.WriteLine($"Error transferring file to server: {ex.Message}");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"An error occurred: {ex.Message}");
//        }
//    }
//}

//internal class Client
//{
//    private const int BUFFER_SIZE = 4096;
//    private static byte[] buffer = new byte[BUFFER_SIZE];

//    public static async void Execute()
//    {
//        try
//        {
//            Console.WriteLine("Client: Starting connection...");

//            // Connect to server
//            TcpClient client = new TcpClient("localhost", 8080);
//            NetworkStream stream = client.GetStream();

//            Console.WriteLine("Client: Connected to server. Type 'file:<filename>' to send a file or type a message.");

//            // Loop to handle communication
//            while (true)
//            {
//                string input = Console.ReadLine();

//                if (input.StartsWith("file:"))
//                {
//                    // Send file
//                    string filePath = input.Substring(5);
//                    if (File.Exists(filePath))
//                    {
//                        byte[] fileData = File.ReadAllBytes(filePath);
//                        string header = $"FILE:{Path.GetFileName(filePath)}:{fileData.Length}";
//                        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
//                        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
//                        await stream.WriteAsync(fileData, 0, fileData.Length);
//                        Console.WriteLine("Client: File sent.");
//                    }
//                    else
//                    {
//                        Console.WriteLine("Client: File not found.");
//                    }
//                }
//                else
//                {
//                    // Send a message
//                    byte[] message = Encoding.UTF8.GetBytes(input);
//                    await stream.WriteAsync(message, 0, message.Length);
//                }

//                // Receive response
//                int bytesRead = await stream.ReadAsync(buffer, 0, BUFFER_SIZE);
//                Console.WriteLine("Server: " + Encoding.UTF8.GetString(buffer, 0, bytesRead));
//            }
//        }
//        catch (Exception e)
//        {
//            Console.WriteLine("Client: Error - " + e.Message);
//        }
//    }
//}

