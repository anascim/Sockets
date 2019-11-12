using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

class MainClass
{
    public static void Menu()
    {
        Console.WriteLine("Press 1 to start a Server");
        Console.WriteLine("Press 2 to start a Client");
        Console.WriteLine("Press anything else to exit...");
        char input = Console.ReadKey().KeyChar;
        Console.WriteLine();
        if (input == '1')
        {
            Console.WriteLine("Inform a host/IP...");
            string ip = Console.ReadLine();

            Console.WriteLine("Inform a port...");
            string port = Console.ReadLine();
            int nport = 0;

            try
            {
                nport = int.Parse(port);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Exiting the application...");
                Environment.Exit(0);
            }

            Console.WriteLine("Inform the client capacity...");
            string capacity = Console.ReadLine();
            uint ncapacity = 0;

            try
            {
                ncapacity = uint.Parse(capacity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Exiting the application...");
                Environment.Exit(0);
            }

            _ = new Server(ip, nport, uint.Parse(capacity));
        }
        else if (input == '2')
        {
            Console.WriteLine("Inform a host/IP...");
            string ip = Console.ReadLine();

            Console.WriteLine("Inform a port...");
            string port = Console.ReadLine();
            int nport = 0;

            try
            {
                nport = int.Parse(port);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Exiting the application...");
                Environment.Exit(0);
            }

            Console.WriteLine("Inform yout name...");
            string name = Console.ReadLine();

            _ = new Client(ip, nport, name);
        }
        else
        {
            Console.WriteLine("Exiting Application...");
            Environment.Exit(0);
        }
    }

    public static void Main(string[] args)
    {
        Menu();
    }
}

class Server
{
    string ip;
    int port;
    List<Connection> connections;
    uint maxSize;
    WaitHandle waitHandle;

    public Server(string ip, int port, uint clientsN)
    {
        this.ip = ip;
        this.port = port;
        this.maxSize = clientsN;
        this.connections = new List<Connection>();
        StartServer(ip, port, clientsN);
    }

    public void StartServer(string server, int port, uint clientsN)
    {
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAdress = ipHostInfo.AddressList[0];
        IPEndPoint ipEndPoint = new IPEndPoint(ipAdress, port);

        // cria o socket mas ainda não o atribui a um ip/port
        Socket socket = new Socket(ipAdress.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        try
        {
            socket.Bind(ipEndPoint); // liga o socket a um ip/port, representado pela classe IPEndPoint
            socket.Listen((int)clientsN);

            
            Console.WriteLine("> WAITING FOR A CONNECTION...");
            // verificar número de clientes conectados antes do BeginAccept()
            socket.BeginAccept(AcceptClient, socket); // begins listening threads

            // main thread: receive Server input
            while (true)
            {
                string input = Console.ReadLine();

                if (input == "/encerrar")
                {
                    foreach (Connection client in connections)
                    {
                        if (client == null) continue;
                        Send(client.socket, "/encerrar<EOF>");
                        client.socket.Shutdown(SocketShutdown.Both);
                        client.socket.Close();
                    }
                    break;
                }
                else if (input == "/clientes")
                {
                    foreach (var c in connections) {
                        Console.WriteLine(c.name);
                    }
                }
                else if (input.Contains("/desconectar"))
                {
                    string temp = input;
                    temp.Remove(12);
                    Console.WriteLine("TODO: desconectar cliente: " + temp);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        socket.Shutdown(SocketShutdown.Both);
        socket.Close();

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();
    }

    private void AcceptClient(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState; // server socket
        if (connections.Count == maxSize)
        {
            Console.WriteLine("Cannot connect new client - max number of clients reached!");
            socket.BeginAccept(AcceptClient, socket);
            return;
        }
        Socket handler = socket.EndAccept(ar); // client socket
        Connection connection = new Connection(CreateGenericName(), handler);

        handler.BeginReceive(connection.buffer, 0, connection.buffer.Length, 0, ReceiveCallback, connection);

        
        Console.WriteLine("> {0} CONNECTED.", handler.RemoteEndPoint);
        connections.Add(connection);
        socket.BeginAccept(AcceptClient, socket); // continues to listen to other cliets
    }

    private void Send(Socket handler, string message)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        handler.BeginSend(data, 0, data.Length, 0, SendCallback, handler);
    }

    private void SendCallback(IAsyncResult ar)
    {
        Socket handler = (Socket)ar.AsyncState;
        int sentData = handler.EndSend(ar);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        Connection connection = (Connection)ar.AsyncState;
        Socket socket = connection.socket;

        int bytesRead = socket.EndReceive(ar);

        if (bytesRead > 0)
        {
            connection.message += Encoding.ASCII.GetString(connection.buffer, 0, bytesRead);

            if (connection.message.IndexOf("<EOF>") > -1)
            {
                // received the whole client message
                connection.message = connection.message.Remove(connection.message.Length - 5);
                Console.WriteLine($"[{connection.name}] {connection.message}");
                string trimmedMsg = connection.message.Trim();

                if (connection.message.Contains("/sair"))
                {
                    if (connection.socket.Connected)
                    {
                        connection.socket.Shutdown(SocketShutdown.Both);
                        connection.socket.Close();
                    }
                    Console.WriteLine("> {0} DISCONECTOU.", connection.name);
                    // informa para os outros usuários que o cliente saiu
                    foreach (Connection client in connections)
                    {
                        Send(client.socket, connection.name + " DISCONECTOU.<EOF>");
                    }
                    connections.Remove(connection);
                }
                else if (trimmedMsg[0] == '@')
                {
                    // direct message
                }
                else if (connection.message.Contains("/nome"))
                {
                    string oldName = connection.name;
                    string chosenName = connection.message.Remove(0, 5);
                    chosenName.Trim();

                    if (NameIsValid(chosenName))
                    {
                        connection.name = chosenName;
                    }
                    else
                    {
                        Send(connection.socket, "Alguem ja possui esse nome<EOF>");
                    }
                    // check if the user is setting its first name
                    if (connection.name == chosenName)
                    {
                        foreach (Connection client in connections)
                        {
                            Send(client.socket, connection.name + " entrou no chat!<EOF>");
                        }

                        connection.nameChosen = true;
                    }
                    else
                    {
                        foreach (Connection client in connections)
                        {
                            Send(client.socket, oldName + " mudou de nome para " + connection.name + "<EOF>");
                        }
                    }
                }
                else
                {
                    foreach (Connection client in connections)
                    {
                        Send(client.socket, connection.name + " " +connection.message + "<EOF>");
                    }
                }
                connection.message = "";
            }
            else
            {
                // a mensagem não foi recebida por completo... receber o restante da mensagem
                socket.BeginReceive(connection.buffer, 0, connection.buffer.Length, 0, ReceiveCallback, connection);
            }
        }
        socket.BeginReceive(connection.buffer, 0, connection.buffer.Length, 0, ReceiveCallback, connection);
    }

    bool NameIsValid(string name)
    {
        foreach (Connection c in connections)
        {
            if (c.name == name)
            {
                return false;
            }
        }
        return true;
    }

    string CreateGenericName()
    {
        int number = 0;
        string name = "client" + number;
        while (!NameIsValid(name))
        {
            number++;
            name = "client" + number;
        }
        return name;
    }
}

//class Message
//{
//    public byte[] bytes;
//    public Socket socket;

//    public Message(byte[] bytes, Socket socket)
//    {
//        this.bytes = bytes;
//        this.socket = socket;
//    }
//}

class Connection
{
    public string name;
    public Socket socket;
    public byte[] buffer;
    public string message;
    public bool nameChosen;

    public Connection(string name, Socket socket)
    {
        this.name = name;
        this.socket = socket;
        this.buffer = new byte[1024];
        this.message = "";
        this.nameChosen = false;
    }
}

class Client
{
    string server;
    int port;
    string name;
    byte[] buffer;
    string data;
    private static ManualResetEvent connectDone = new ManualResetEvent(false);

    public Client(string server, int port, string name)
    {
        this.server = server;
        this.port = port;
        this.name = name;
        this.buffer = new byte[1024];
        this.data = "";
        StartClient(server, port, name);
    }

    void StartClient(string server, int port, string name)
    {
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAdress = ipHostInfo.AddressList[0];
        IPEndPoint ipEndPoint = new IPEndPoint(ipAdress, port);

        Socket socket = new Socket(ipAdress.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        try
        {
            socket.BeginConnect(ipEndPoint, ConnectCallback, socket);
            connectDone.WaitOne();
            Send(socket, "/nome " + this.name + "<EOF>");
            while (true)
            {
                // send messages
                string message = Console.ReadLine();
                message += "<EOF>";
                Send(socket, message);

                if (message == "/sair")
                {
                    if (socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                    }
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();
    }

    void ConnectCallback(IAsyncResult ar)
    {
        Socket client = (Socket)ar.AsyncState;
        client.EndAccept(ar);
        Console.WriteLine("> CONNECTED TO {0}", client.RemoteEndPoint);
        connectDone.Set();
        client.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, client);
    }

    void ReceiveCallback(IAsyncResult ar)
    {
        Socket client = (Socket)ar.AsyncState;
        int bytesReceived = client.EndReceive(ar);
        data += Encoding.ASCII.GetString(buffer, 0, bytesReceived);

        if (data.IndexOf("<EOF>") > -1) //EOF: End Of File. Poderia colocar qualquer coisa como final de msg
        {
            data = data.Remove(data.Length - 5);
            if (data == "/encerrar")
            {
                Console.WriteLine("> ENCERRANDO SERVIDOR...");
                if (client.Connected)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
            }
            Console.WriteLine("[SERVER] {0}", data);
            data = "";
        }
        client.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, client);
    }

    private void Send(Socket client, string message)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        client.BeginSend(data, 0, data.Length, 0, SendCallback, client);
    }

    private void SendCallback(IAsyncResult ar)
    {
        Socket client = (Socket)ar.AsyncState;
        client.EndSend(ar);
    }
}
