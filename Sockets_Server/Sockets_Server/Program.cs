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
        Log.Yellow(new string[]{
                    "Press 1 to start a Server",
                    "Press 2 to start a Client",
                    "Press anything else to exit..." });
        char input = Console.ReadKey().KeyChar;
        Console.WriteLine();
        if (input == '1')
        {
            Log.Yellow(new string[] { "Inform a host/IP..." });
            string ip = Console.ReadLine();

            Log.Yellow(new string[] { "Inform a port..." });
            string port = Console.ReadLine();

            int nport = 0;

            try
            {
                nport = int.Parse(port);
            }
            catch (Exception e)
            {
                Log.Red(new string[] { "Exiting the application..." });
            }

            Log.Yellow(new string[] { "Inform the client capacity..." });
            string capacity = Console.ReadLine();
            uint ncapacity = 0;

            try
            {
                ncapacity = uint.Parse(capacity);
            }
            catch (Exception e)
            {
                Log.Red(new string[] { e.ToString(), "Exiting the application..." });
                Environment.Exit(0);
            }

            _ = new Server(ip, nport, uint.Parse(capacity));
        }
        else if (input == '2')
        {
            Log.Yellow(new string[] { "Inform a host/IP..." });
            string ip = Console.ReadLine();

            Log.Yellow(new string[] { "Inform a port..." });
            string port = Console.ReadLine();
            int nport = 0;

            try
            {
                nport = int.Parse(port);
            }
            catch (Exception e)
            {
                Log.Red(new string[] { e.ToString(), "Exiting the application..." });
                Environment.Exit(0);
            }

            Log.Yellow(new string[] { "Inform yout name..." });
            string name = Console.ReadLine();

            _ = new Client(ip, nport, name);
        }
        else
        {
            Log.Yellow(new string[] { "Exiting the application..." });
            Environment.Exit(0);
        }
    }

    public static void Main(string[] args)
    {
        Menu();
    }
}

static class Log
{
    public static void Yellow(string[] messages)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        foreach (string m in messages)
        {
            Console.WriteLine(m);
        }
        Console.ResetColor();
    }

    public static void Red(string[] messages)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        foreach (string m in messages)
        {
            Console.WriteLine(m);
        }
        Console.ResetColor();
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

            Log.Yellow(new string[] { "> WAITING FOR A CONNECTION..." });

            socket.BeginAccept(AcceptClient, socket);

            // main thread: receive Server input
            while (true)
            {
                string input = Console.ReadLine();

                if (input.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    if (input == "/encerrar")
                    {
                        SendToAll("/encerrar<EOF>");
                        foreach (Connection client in connections)
                        {
                            client.socket.Shutdown(SocketShutdown.Both);
                            client.socket.Close();
                        }
                        break;
                    }
                    else if (input == "/clientes")
                    {
                        foreach (var c in connections)
                        {
                            Console.WriteLine(c.name);
                        }
                    }
                    else if (input.Contains("/desconectar"))
                    {
                        string clientName = input.Remove(0, 13);
                        bool clientFound = false;
                        foreach (Connection c in connections)
                        {
                            if (c.name == clientName)
                            {
                                Send(c, "[SERVER] Você foi desconectado<EOF>");
                                c.Disconnect();
                                connections.RemoveAt(connections.IndexOf(c));
                                Log.Yellow(new string[] { $"{clientName} foi desconectado!" });
                                clientFound = true;
                                break;
                            }
                        }
                        if (!clientFound)
                        {
                            Log.Red(new string[] { $"Nenhum cliente encontrado com o nome {clientName}." });
                        }
                        else
                        {
                            SendToAll($"{clientName} saiu da conversa.<EOF>");
                        }
                    }
                    else
                    {
                        Log.Red(new string[] { "Comando inexistente." });
                    }
                }
                else if (input.StartsWith("@", StringComparison.OrdinalIgnoreCase))
                {
                    string temp = input.Remove(0, 1);
                    string[] nameAndMsg = temp.Split(new char[] {' '}, 2);
                    string name = nameAndMsg[0];
                    string msg = nameAndMsg[1];
                    bool nameFound = false;
                    foreach (Connection c in connections)
                    {
                        if (c.name == name)
                        {
                            Send(c, "[SERVER] " + msg + "<EOF>");
                            nameFound = true;
                        }
                    }
                    if (!nameFound)
                    {
                        Log.Red(new string[] { "Nenhum cliente encontrado com esse nome." });
                    }
                }
                else
                {
                    SendToAll("[SERVER] " + input + "<EOF>");
                }
                
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();
        Console.ResetColor();
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

    private void Send(Connection connection, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        connection.socket.BeginSend(data, 0, data.Length, 0, SendCallback, connection.socket);
    }

    private void SendCallback(IAsyncResult ar)
    {
        Socket handler = (Socket)ar.AsyncState;
        int sentData = handler.EndSend(ar);
    }

    private void SendToAll(string message)
    {
        foreach (Connection client in connections)
        {
            Send(client, message);
        }
    }

    private void SendToAllButOne(Connection exception, string message)
    {
        foreach (Connection client in connections)
        {
            if (client != exception)
            {
                Send(client, message);
            }
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        Connection connection = (Connection)ar.AsyncState;
        Socket socket = connection.socket;
        int bytesRead = 0;
        try
        {
            bytesRead = socket.EndReceive(ar);
        }
        catch (Exception e)
        {
            Log.Red(new string[] { e.ToString() });
        }

        if (bytesRead > 0)
        {
            connection.message += Encoding.UTF8.GetString(connection.buffer, 0, bytesRead);

            if (connection.message.IndexOf("<EOF>") > -1)
            {
                // received the whole client message
                connection.message = connection.message.Remove(connection.message.Length - 5);
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"[{connection.name}]");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{connection.message}");
                Console.ResetColor();

                if (connection.message.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    if (connection.message.StartsWith("/sair", StringComparison.OrdinalIgnoreCase))
                    {
                        if (connection.socket.Connected)
                        {
                            connection.Disconnect();
                        }
                        Console.WriteLine("> {0} DISCONECTOU.", connection.name);
                        // informa para os outros usuários que o cliente saiu
                        SendToAllButOne(connection, connection.name + " disconectou.<EOF>");
                        connections.Remove(connection);
                        return;
                    }
                    else if (connection.message.StartsWith("/quem", StringComparison.OrdinalIgnoreCase))
                    {
                        string message = "";
                        foreach (Connection c in connections)
                        {
                            message += c.name + "\n";
                        }
                        Send(connection, message + "<EOF>");
                    }
                    else if (connection.message.StartsWith("/nome", StringComparison.OrdinalIgnoreCase))
                    {
                        string oldName = connection.name;
                        string chosenName = connection.message.Remove(0, 6);

                        if (NameIsValid(chosenName))
                        {
                            connection.name = chosenName;
                        }
                        else
                        {
                            Send(connection, "Alguem ja possui esse nome<EOF>");
                        }

                        if (connection.nameChosen)
                        {
                            SendToAllButOne(connection, oldName + " mudou de nome para " + connection.name + "<EOF>");
                        }
                        else
                        {
                            // the user is setting its first name
                            Log.Yellow(new string[] { $"{oldName} entrou na sala como {connection.name}" });
                            Send(connection, $"##servernewname## {connection.name}<EOF>");
                            SendToAllButOne(connection, connection.name + " entrou no chat.<EOF>");
                            connection.nameChosen = true;
                        }
                    }
                    else
                    {
                        Send(connection, "[SERVER] Invalid command!<EOF>");
                    }
                }
                else if (connection.message.StartsWith("@", StringComparison.OrdinalIgnoreCase))
                {
                    string temp = connection.message.Remove(0, 1);
                    string[] nameAndMsg = temp.Split(new char[] { ' ' }, 2);
                    string name = nameAndMsg[0];
                    string msg = nameAndMsg[1];
                    bool nameFound = false;
                    foreach (Connection c in connections)
                    {
                        if (c.name == name)
                        {
                            Send(c, connection.name + ": " + msg + "<EOF>");
                            nameFound = true;
                        }
                    }
                    if (!nameFound)
                    {
                        Send(connection, "[SERVER] Nenhum cliente encontrado com esse nome.");
                    }
                }
                
                else
                {
                    SendToAllButOne(connection, connection.name + ": " + connection.message + "<EOF>");
                }
                connection.message = "";
            }
            socket.BeginReceive(connection.buffer, 0, connection.buffer.Length, 0, ReceiveCallback, connection);
        }
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

    public void Disconnect()
    {
        this.socket.Shutdown(SocketShutdown.Both);
        this.socket.Close();
    }
}

class Client
{
    string server;
    int port;
    readonly string initialName;
    string currentName;
    byte[] buffer;
    string data;
    private static ManualResetEvent connectDone = new ManualResetEvent(false);

    public Client(string server, int port, string name)
    {
        this.server = server;
        this.port = port;
        this.initialName = name;
        this.buffer = new byte[1024];
        this.data = "";
        StartClient(server, port);
    }

    void StartClient(string server, int port)
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
            Send(socket, "/nome " + this.initialName + "<EOF>");
            while (true)
            {
                // send messages
                string input = Console.ReadLine();
                if (input.StartsWith("/eu", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Seu nome é: {this.initialName}");
                    continue;
                }
                if (input.StartsWith("/sair", StringComparison.OrdinalIgnoreCase))
                {
                    if (socket.Connected)
                    {
                        byte[] msg = Encoding.UTF8.GetBytes("/sair<EOF>");
                        socket.Send(msg);
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                    }
                    break;
                }
                else
                {
                    Send(socket, input + "<EOF>");
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
        data += Encoding.UTF8.GetString(buffer, 0, bytesReceived);

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
            else if (data.Contains("##servernewname##"))
            {
                data = data.Remove(0, 18);
                this.currentName = data;
                Console.WriteLine($"Seu nome agora é: {currentName}");
            }
            else
            {
                Console.WriteLine(data);
            }
            data = "";
        }
        client.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, client);
    }

    private void Send(Socket client, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        client.BeginSend(data, 0, data.Length, 0, SendCallback, client);
    }

    private void SendCallback(IAsyncResult ar)
    {
        Socket client = (Socket)ar.AsyncState;
        client.EndSend(ar);
    }
}
