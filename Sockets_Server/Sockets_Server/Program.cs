using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace Sockets_Server
{
    class MainClass
    {
        public static void StartServer(string server, int port)
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
                socket.Listen(10);

                Socket[] clientSockets = new Socket[10];

                Console.WriteLine("> WAITING FOR A CONNECTION...");

                Thread connectionsThread = new Thread(() => WaitForConnections(clientSockets, socket));
                connectionsThread.Start();

                while (true)
                {
                    string input = Console.ReadLine();

                    if (input == "/encerrar")
                    {
                        foreach (Socket client in clientSockets)
                        {
                            if (client == null) continue;
                            byte[] msg = Encoding.ASCII.GetBytes("/encerrar<EOF>");
                            client.Send(msg);
                            client.Shutdown(SocketShutdown.Both);
                            client.Close();
                        }
                        break;
                    }
                }
                connectionsThread.Abort();
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

        static void WaitForConnections(Socket[] clientSockets, Socket serverSocket)
        {
            for (int i = 0; i < clientSockets.Length; i++)
            {
                if (clientSockets[i] != null) continue;
                clientSockets[i] = serverSocket.Accept(); // para o código até receber uma conexão. É uma referência para o cliente
                Connection connection = new Connection(clientSockets[i]);
            }
        }

        public static void Main(string[] args)
        {
            StartServer("localhost", 8080);
        }
    }

    class Connection
    {
        Socket m_ClientSocket;

        public Connection(Socket clientSocket)
        {
            m_ClientSocket = clientSocket;
            Console.WriteLine("> {0} CONNECTED.", clientSocket.RemoteEndPoint); // EndPoint é o endereço do cliente, ou seja, de quem conectou conosco
            StartThread();
        }

        public void StartThread()
        {
            Thread thread = new Thread(StartClientConnection);
            thread.Start(m_ClientSocket);
        }

        void StartClientConnection(object clientSock)
        {
            Socket handler = clientSock as Socket;

            byte[] buffer = new byte[1024];

            while (true) // enquanto conectado com o cliente
            {
                string data = null;
                // Recebe as mensagens até encontrar <EOF>
                while (true)
                {
                    int bytesReceived = handler.Receive(buffer);
                    data += Encoding.ASCII.GetString(buffer, 0, bytesReceived);

                    if (data.IndexOf("<EOF>") > -1) //EOF: End Of File
                    {
                        data = data.Remove(data.Length - 5);
                        break;
                    }
                }

                if (data == "/sair")
                {
                    if (handler.Connected)
                    {
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                    Console.WriteLine("> CLIENT DISCONNECTED.");
                    m_ClientSocket = null;
                    break;
                }

                Console.WriteLine("[CLIENT] {0}", data);

                // envia mensagem ao cliente
                byte[] message = Encoding.ASCII.GetBytes("Size: " + data.Length + " characters; " + data.ToUpper() + "<EOF>");
                handler.Send(message);
            }
        }
    }
}
