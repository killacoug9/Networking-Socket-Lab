namespace Server;

internal class Program
{
    static void Main(string[] args)
    {
        Server server = new Server();
        server.Start().Wait();


        //ServerGPT serverGPT = new ServerGPT();
        //serverGPT.StartAsync().Wait();
    }
}