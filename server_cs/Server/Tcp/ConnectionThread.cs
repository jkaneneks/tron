﻿using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

using Newtonsoft.Json;
using Server.Logic.Event;
using Server.Encryption;
using System.Threading;

namespace Server.Tcp
{

    public class ConnectionThread
    {
        public TcpListener threadListener;
        private static int connections = 0;

        public void HandleConnection()
        {
            TcpClient client = threadListener.AcceptTcpClient();
            client.NoDelay = true;
            NetworkStream ns = client.GetStream();
            connections++;
            Console.WriteLine($"New client accepted: {connections} active connections");

            SendPublicParameters(client);

            int bytesToRead = 0, nextReadCount = 0, rc = 0;
            byte[] byteCount = BitConverter.GetBytes(1024);
            byte[] receiveBuffer = new byte[4096];

            try
            {
                string data = "";

                while (true)
                {
                    bytesToRead = BitConverter.ToInt32(byteCount, 0);


                    // Receive the data
                    //Console.WriteLine("TCP Listener: Receiving, reading & displaying the data...");
                    while (bytesToRead > 0)
                    {
                        if (!ns.CanRead)
                            break;

                        // Make sure we don't read beyond what the first message indicates
                        //    This is important if the client is sending multiple "messages" --
                        //    but in this sample it sends only one
                        if (bytesToRead < receiveBuffer.Length)
                            nextReadCount = bytesToRead;
                        else
                            nextReadCount = receiveBuffer.Length;

                        // Read some data
                        rc = ns.Read(receiveBuffer, 0, nextReadCount);

                        // Detect if client disconnected
                        if (client.Client.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] buff = new byte[1];
                            if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
                            {
                                // Client disconnected
                                Console.Error.WriteLineAsync($"Host {client.Client.RemoteEndPoint.ToString()} disconected!");
                                ConnectionLost?.Invoke(new ConnectionLostArguments(client.Client.RemoteEndPoint.ToString(), this));
                                break;
                            }
                        }

                        data += Encoding.UTF8.GetString(receiveBuffer, 0, rc);                        

                        if (data.Contains(Environment.NewLine))
                        {
                            string[] parts = data.Split(Environment.NewLine);

                            data = parts[0];

                            if (TronServer.Encryption.HasClient(client))
                            {
                                data = data.Replace(Environment.NewLine, "");
                                byte[] readTextBytes = Convert.FromBase64String(data);
                                byte[] plainTextBytes = TronServer.Encryption.Decrypt(readTextBytes);

                                data = Encoding.UTF8.GetString(plainTextBytes);
#if DEBUG
                                Console.WriteLine($"Received from {client.Client.RemoteEndPoint}: {data}");
#endif
                                Protocol.Protocol protocol = null;
                                try
                                {
                                    protocol = JsonConvert.DeserializeObject<Protocol.Protocol>(data);
                                }
                                catch (Exception ex)
                                {
                                    protocol = null;
                                    Console.Error.WriteLineAsync($"JSON deserialize ERROR: '{ex.Message}'.");
                                }

                                if (protocol != null)
                                {
                                    ProtocolRecievedArguments protocolRecievedArguments = new ProtocolRecievedArguments(protocol, client);
                                    ProtocolRecieved?.Invoke(protocolRecievedArguments);
                                }
                            }
                            else  // First message always contains the key
                            {
                                TronServer.Encryption.AddClient(client, RSAPublicParamters.FromJson(data));
                            }
                            data = parts[1];
                        }

                        bytesToRead -= rc;
                    }

                    if (rc == 0)
                    {
                        break;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Console.WriteLine("Connection canceled!");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
            finally
            {
                ns.Close();
                client.Close();
                connections--;
                Console.WriteLine($"Client disconnected: {connections} active connections");
            }
        }

        public bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                return false;
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return pingable;
        }

        private void SendPublicParameters(TcpClient client)
        {
            string json = TronServer.Encryption.PublicJavaParamters().ToJson();
            json = string.Concat(json, Environment.NewLine);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            var stream = client.GetStream();
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            stream.Flush();
        }


        public delegate void ProtocolRecievedHandler(ProtocolRecievedArguments protocolRecievedArguments);
        public event ProtocolRecievedHandler ProtocolRecieved;

        public delegate void ConnectionLostHandler(ConnectionLostArguments connectionLostArguments);
        public event ConnectionLostHandler ConnectionLost;

    }
}
