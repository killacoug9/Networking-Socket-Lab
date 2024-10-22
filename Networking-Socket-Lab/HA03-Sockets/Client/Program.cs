namespace Client;

internal class Program
{
    static void Main(string[] args)
    {
        // Initialize the client with server IP and port
        //Client client = new Client("10.216.191.148", 8080);
        Client client = new Client("10.216.100.226", 8080);

        // Connect to the server
        client.ConnectAsync().Wait();
    }
}
