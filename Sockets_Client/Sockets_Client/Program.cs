using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;

// para abrir um novo Visual Studio...
// open -n /Applications/Visual\ Studio.app
namespace Sockets_Client
{
    class MainClass
    {
        public static void StartClient(string server, int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAdress = ipHostInfo.AddressList[0];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAdress, port);

            Socket socket = new Socket(ipAdress.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);

            try
            {
                socket.Connect(ipEndPoint);
                Console.WriteLine("> CONNECTED TO {0}", socket.RemoteEndPoint);

                Thread thread = new Thread(() => ReceiveMessages(socket));
                thread.Start();

                while (true)
                {
                    string strmsg = null;
                    // send messages
                    strmsg = Console.ReadLine(); // string message
                    byte[] message = Encoding.ASCII.GetBytes(strmsg + "<EOF>");
                    socket.Send(message);

                    if (strmsg == "/sair")
                    {
                        if(socket.Connected)
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

        static void ReceiveMessages(Socket socket)
        {
            byte[] buffer = new byte[1024];

            // receive messages
            string data = "";
            while (true)
            {
                int bytesReceived = socket.Receive(buffer);
                data += Encoding.ASCII.GetString(buffer, 0, bytesReceived);

                if (data.IndexOf("<EOF>") > -1) //EOF: End Of File. Poderia colocar qualquer coisa como final de msg
                {
                    if (data == "/encerrar<EOF>")
                    {
                        Console.WriteLine("> ENCERRANDO SERVIDOR...");
                        if (socket.Connected)
                        {
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Close();
                        }
                    }
                    break;
                }

                Console.WriteLine("[SERVER] {0}", data.Remove(data.Length - 5));
            }

        }

        public static void Main(string[] args)
        {
            StartClient("localhost", 8080);
        }
    }
}
