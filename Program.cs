using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace stream
{
    class Program
    {

        internal static (NetworkStream ClientStream, NetworkStream ServerStream) GetConnectedTcpStreams()
        {
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);

                var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(listener.LocalEndPoint);
                Socket serverSocket = listener.Accept();

                Console.WriteLine("GetConnectedTcpStreams: {0} {1} client {2} server {3}", clientSocket.LocalEndPoint, clientSocket.RemoteEndPoint, clientSocket.Handle, serverSocket.Handle);
   //             serverSocket.NoDelay = true;
   //             clientSocket.NoDelay = true;
                Console.WriteLine("buffers = {0} and {1}", clientSocket.SendBufferSize,  serverSocket.SendBufferSize);

                // NetworkStream streams own socket -> should call Shutdown and write out all data.
                return (new NetworkStream(clientSocket, ownsSocket: true), new NetworkStream(serverSocket, ownsSocket: true));
            }

        }

        public static async Task CopyToAsync_AllDataCopied(int byteCount, bool useAsync, SslStream writeable, SslStream readable)
        {
            //using StreamPair streams = await CreateConnectedStreamsAsync();
            //(Stream writeable, Stream readable) = GetReadWritePair(streams);

            var results = new MemoryStream();
            byte[] dataToCopy = new byte[byteCount];
            //byte[] dataToCopy = RandomNumberGenerator.GetBytes(byteCount);
            //RandomNumberGenerator.GetBytes(dataToCopy);

            Task copyTask;
            if (useAsync)
            {
                copyTask = readable.CopyToAsync(results);
//                copyTask = Task.CompletedTask;
                try
                {
                await writeable.WriteAsync(dataToCopy);
                } catch (Exception ex)
                {
                    Console.WriteLine("Writing task failed {0}!!!!", ex);
                    throw;
                }
                Console.WriteLine("Write os DONE!!!");

            }
            else
            {
                copyTask = Task.Run(() => readable.CopyTo(results));
                writeable.Write(new ReadOnlySpan<byte>(dataToCopy));
            }
try {
            writeable.Dispose();
            } catch (Exception ex)
            {
                Console.WriteLine("Write dispose failed!!! {0}", ex);
                throw;
            }
            await copyTask;

        //    Assert.Equal(dataToCopy, results.ToArray());
            Console.WriteLine("CopyToAsync_AllDataCopied is done");
        }

        static async Task Main(string[] args)
        {
            int count = 1024 * 1024;
            (NetworkStream client, NetworkStream server) = GetConnectedTcpStreams();
            byte[] dataToCopy =  new byte[count];
             bool leaveOpen = false;

            if (args.Length == 0)
            {
                //X509Certificate2? cert = new X509Certificate2("../../../../../cert.pfx",  "password");
                X509Certificate2? cert = new X509Certificate2("cert.pfx",  "password");
                var ssl1 = new SslStream(client, leaveOpen, delegate { return true; });
                var ssl2 = new SslStream(server, leaveOpen, delegate { return true; });

                await new[]
                {
                    ssl2.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls13, false),
                    ssl1.AuthenticateAsClientAsync(cert.GetNameInfo(X509NameType.SimpleName, false), null, SslProtocols.Tls13, false)
                }.WhenAllOrAnyFailed(5000).ConfigureAwait(false);


                await CopyToAsync_AllDataCopied(1024 * 1024, true, ssl1, ssl2);
            }
            else
            {
                byte[] readBufer = new byte[16142];

                ValueTask<int> t =  server.ReadAsync(readBufer, default);
                await client.WriteAsync(dataToCopy, 0, 1);
                await t;


                t =  client.ReadAsync(readBufer, default);
                await server.WriteAsync(dataToCopy, 0, 1);
                await t;

                using (client)
                {
                    // don't read quite yet.
                    await client.WriteAsync(dataToCopy, default);
                    // Should flush data! (not needed for repro)
                    //client.Socket.Shutdown(SocketShutdown.Send);
                    Console.WriteLine("Write is done! {0}", DateTime.Now);
                }

                // Attempt to read from server stream would fail here.
                int bytesRecvd = 0;
                while (bytesRecvd < count)
                {
                    int bytes = await server.ReadAsync(readBufer, default);
                    Console.WriteLine($"Received {bytes} bytes");
                    if (bytes == 0)
                        break;

                    bytesRecvd += bytes;
                }

                Console.WriteLine($"Read is done! Received {bytesRecvd} bytes total {DateTime.Now}");
            }
        }
    }
}
