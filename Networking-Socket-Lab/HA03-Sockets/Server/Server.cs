using System;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Server;

public class Server
{
    static readonly int DEFAULT_PORT = 8080;
    static readonly int MAX_CONNECTIONS = 5;

    private const int BUFFER_SIZE = 4096;
    private static byte[] buffer = new byte[BUFFER_SIZE];

    private Socket _listener;
    private ICollection<Socket> _connectedSockets;
    private int _numConnections;
    private CancellationToken _cancellationToken;

    private bool _isRunning;
    IPAddress _serverAddress;

    public Server()
    {
        this._listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        this._connectedSockets = new List<Socket>();

        NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces(); //get all network interfaces
        IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName()); // get all IP addresses based on the local host name

        foreach (NetworkInterface adapter in adapters) //for each Network interface in addapters
        {
            IPInterfaceProperties properties = adapter.GetIPProperties(); // get the ip properties from the adapter and store them into properties
            foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses) // for each UnicastIPAddressInformation in the IPInterfaceProperties Unicast address( this assocaites the IP address with the correct adapter)
            {
                //if the operationalStatus of the adapter is up and the ip Address family is in the Internwork
                if (_serverAddress == null && (ip.Address.AddressFamily == AddressFamily.InterNetwork)) //test against the name of the adapter you want to get
                {
                    _serverAddress = ip.Address;
                }//end if
            }//end inner for, the UnicastIPAddressInformation for
        }
    }

    public Server(string host, int port) : this()
    {

    }

    public async Task Start()
    {
        await Setup();
        //AcceptNewConnections();
        this._listener.Listen(MAX_CONNECTIONS);

        //this._listener.BeginAccept(new AsyncCallback(Socket_AcceptCallback), null);
        Console.WriteLine(">SERVER LOG: Server started listening. Waiting for connections...");
        Console.WriteLine(">SERVER LOG: Server listening on " + this._listener.LocalEndPoint.ToString());
        
        this._isRunning = true;

        while (this._isRunning)
        {
            Socket clientSocket = await _listener.AcceptAsync();
            Console.WriteLine(">SERVER LOG: Client connected.");

            _ = HandleClientAsync(clientSocket);
        }
    }

    public void Stop()
    {
        this._listener.Close();

        foreach (Socket socket in this._connectedSockets)
        {
            socket.Close();
        }
        this._connectedSockets.Clear();
        this._numConnections = 0;

        this._isRunning = false;
    }

    private async Task Setup()
    {
        //IPHostEntry localhost = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
        IPHostEntry localhost = Dns.GetHostEntryAsync("localhost").Result;
        this._listener.Bind(new IPEndPoint(_serverAddress, DEFAULT_PORT));

    }

    private void Socket_AcceptCallback(IAsyncResult result)
    {
        Socket socket;

        try
        {
            socket = _listener.EndAccept(result);
            _connectedSockets.Add(socket);
            //Console.WriteLine(">SERVER LOG: " + Encoding.UTF8.GetString(buf));
            Console.WriteLine(">SERVER LOG: Accepted Connection");

            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Socket_ReceiveCallback), socket);

            _listener.BeginAccept(new AsyncCallback(Socket_AcceptCallback), null);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private void Socket_ReceiveCallback(IAsyncResult result)
    {
        Socket socket;
        try
        {
            socket = (Socket)result.AsyncState;

            int received = socket.EndReceive(result);

            if (received > 0)
            {
                byte[] data = new byte[received];
                Buffer.BlockCopy(buffer, 0, data, 0, received);
                string message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("FILE:"))
                {
                    // File transfer detected
                    string[] fileInfo = message.Split(':');
                    string fileName = fileInfo[1];
                    int fileSize = int.Parse(fileInfo[2]);

                    Console.WriteLine($">SERVER LOG: Receiving file: {fileName} of size {fileSize} bytes");

                    // Receiving the file
                    byte[] fileData = new byte[fileSize];
                    int bytesRead = socket.Receive(fileData);
                    File.WriteAllBytes(Path.Combine("ReceivedFiles", fileName), fileData);

                    Console.WriteLine($">SERVER LOG: File {fileName} received successfully.");
                    string confirmation = $"File {fileName} received at " + DateTime.UtcNow.ToString() + " UTC.";
                    byte[] confirmationBytes = Encoding.UTF8.GetBytes(confirmation);
                    socket.SendAsync(confirmationBytes);
                }
                else
                {
                    // Normal message handling
                    Console.WriteLine(">SERVER LOG: " + message);

                    string response = "Message received: \"" + message + "\" at " + DateTime.UtcNow.ToString() + " UTC.";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    socket.SendAsync(responseBytes);
                }

                // Continue receiving data
                //socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Socket_ReceiveCallback), socket);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Socket_ReceiveCallback fails with exception: " + e.ToString());
        }
    }

    private async Task HandleClientAsync(Socket clientSocket)
    {
        try
        {
            while (clientSocket.Connected)
            {
                // Receive data from the client
                int bytesReceived = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);

                if (bytesReceived == 0)
                {
                    // Client disconnected
                    Console.WriteLine(">SERVER LOG: Client disconnected gracefully.");
                    break;
                }

                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                if (receivedMessage.StartsWith("FILE`"))
                {
                    await HandleFileTransferAsync(receivedMessage, clientSocket);
                }
                else
                {
                    // Handle regular text messages
                    Console.WriteLine($">SERVER LOG: Client sent \"{receivedMessage}\"");

                    string response = ">SERVER LOG: Client sent message\"" + receivedMessage + "\"";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await clientSocket.SendAsync(responseBytes, SocketFlags.None);
                }
            }
        }
        catch (SocketException ex)
        {
            // Handle socket exceptions (e.g., client forcefully disconnecting)
            Console.WriteLine($"Error communicating with the client: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        finally
        {
            // Ensure the socket is closed after the client disconnects or an error occurs
            Console.WriteLine("Closing client socket.");
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
    }

    private async Task HandleFileTransferAsync(string fileHeader, Socket clientSocket)
    {
        // Parse the file information from the header
        string[] headerParts = fileHeader.Split('`');
        string fileName = headerParts[1];
        int fileSize = int.Parse(headerParts[2]);

        Console.WriteLine($"Receiving file: {fileName}, size: {fileSize} bytes");

        // Prepare to receive the file data
        byte[] fileData = new byte[fileSize];
        int totalBytesReceived = 0;

        while (totalBytesReceived < fileSize)
        {
            int bytesLeft = fileSize - totalBytesReceived;
            int bytesToRead = Math.Min(BUFFER_SIZE, bytesLeft);
            int bytesRead = await clientSocket.ReceiveAsync(new ArraySegment<byte>(fileData, totalBytesReceived, bytesToRead), SocketFlags.None);

            if (bytesRead == 0) break; // If 0 bytes are read, it indicates the connection was closed
            totalBytesReceived += bytesRead;
        }

        // Save the received file
        Directory.CreateDirectory("ReceivedFiles");
        string name = Path.GetFileName(fileName);
        string filePath = Path.Combine("ReceivedFiles", name);
        await File.WriteAllBytesAsync(filePath, fileData);

        Console.WriteLine($">SERVER LOG: Client sent file \"{fileName}\" received and saved.");

        // Send confirmation to the client
        string confirmation = $"File {fileName} received.";
        byte[] confirmationBytes = Encoding.UTF8.GetBytes(confirmation);
        await clientSocket.SendAsync(confirmationBytes, SocketFlags.None);
    }
}
