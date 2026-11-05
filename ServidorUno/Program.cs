// C# MULTI-THREADED SERVER PARA JUEGO UNO EN RED
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Servers
{
    class Program
    
        static readonly object bloqueoLista = new object();


        static readonly Dictionary<int, Socket> clientesPorId = new Dictionary<int, Socket>();

        
        static string ultimoEstadoInicio = null;

        static void Main(string[] args)
        {
            try
            {
                int puerto = 8001;
                TcpListener myList = new TcpListener(IPAddress.Any, puerto);
                myList.Start();

                Console.WriteLine("====================================================");
                Console.WriteLine($"Servidor UNO TCP corriendo en el puerto {puerto}...");
                Console.WriteLine("Endpoint local: " + myList.LocalEndpoint);
                Console.WriteLine("Esperando la conexión de los 4 jugadores...");
                Console.WriteLine("====================================================");

                while (true)
                {
                    Socket clienteSocket = myList.AcceptSocket();
                    int idAsignado = -1;

                    lock (bloqueoLista)
                    {
                        // Buscamos el primer lugar libre entre 0 y 3 
                        for (int i = 0; i < 4; i++)
                        {
                            if (!clientesPorId.ContainsKey(i) || !clientesPorId[i].Connected)
                            {
                                idAsignado = i;
                                clientesPorId[i] = clienteSocket;
                                break;
                            }
                        }
                    }

                    if (idAsignado == -1)
                    {
                        // Si ya están los 4 lugaresx ocupados, rechazamos la conexión extra
                        EnviarDirecto(clienteSocket, "ERROR|Sala llena (4 jugadores máximo).");
                        clienteSocket.Close();
                        continue;
                    }

                    Console.WriteLine($"\n[CONECTADO] Jugador {idAsignado + 1} (ID {idAsignado}) desde: {clienteSocket.RemoteEndPoint}");

                    int idActual = idAsignado;
                    Thread hiloJugador = new Thread(() => AtenderJugador(clienteSocket, idActual));
                    hiloJugador.IsBackground = true;
                    hiloJugador.Start();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error crítico en el Servidor: " + e.Message);
            }

            Console.ReadLine();
        }

        static void AtenderJugador(Socket socketCliente, int idJugador)
        {
            try
            {
                
                EnviarDirecto(socketCliente, $"ASIGNAR|{idJugador}");

                //  Anunciamos a todos cuántos jugadores hay conectados en la mesa
                int conectados;
                lock (bloqueoLista)
                {
                    conectados = clientesPorId.Count;
                }
                Broadcast($"CONECTADOS|{conectados}");

                // Si ya habia una partida en curso al conectarse, le enviamos el estado inicial
                if (!string.IsNullOrEmpty(ultimoEstadoInicio))
                {
                    EnviarDirecto(socketCliente, ultimoEstadoInicio);
                }

                
                StringBuilder acumulador = new StringBuilder();
                byte[] buffer = new byte[4096];

                while (true)
                {
                    int bytesLeidos = socketCliente.Receive(buffer);
                    if (bytesLeidos == 0)
                        break; 

                    acumulador.Append(Encoding.UTF8.GetString(buffer, 0, bytesLeidos));
                    string contenido = acumulador.ToString();

                    int indiceSalto;
                    while ((indiceSalto = contenido.IndexOf('\n')) >= 0)
                    {
                        string trama = contenido.Substring(0, indiceSalto).Trim();
                        contenido = contenido.Substring(indiceSalto + 1);

                        if (!string.IsNullOrEmpty(trama))
                        {
                            Console.WriteLine($"[TRAMA J{idJugador + 1}] {trama}");

                            if (trama.StartsWith("INICIAR|"))
                            {
                                ultimoEstadoInicio = trama;
                            }

                            // Reenvío general (Broadcast) de la trama a los 4 clientes
                            Broadcast(trama);
                        }
                    }

                    acumulador.Clear();
                    acumulador.Append(contenido);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[DESCONECTADO] Jugador {idJugador + 1} salió ({ex.Message}).");
            }
            finally
            {
                int restantes;
                lock (bloqueoLista)
                {
                    if (clientesPorId.ContainsKey(idJugador) && clientesPorId[idJugador] == socketCliente)
                    {
                        clientesPorId.Remove(idJugador);
                    }
                    restantes = clientesPorId.Count;
                }

                try { socketCliente.Close(); } catch { }
                Broadcast($"CONECTADOS|{restantes}");
            }
        }

        
        static void EnviarDirecto(Socket destino, string trama)
        {
            try
            {
                if (destino != null && destino.Connected)
                {
                    byte[] datos = Encoding.UTF8.GetBytes(trama + "\n");
                    destino.Send(datos);
                }
            }
            catch { }
        }

        // Reenvía la trama en bytes a TODOS los clientes conectados (Broadcast)
        static void Broadcast(string trama)
        {
            byte[] datos = Encoding.UTF8.GetBytes(trama + "\n");

            lock (bloqueoLista)
            {
                foreach (var par in clientesPorId)
                {
                    try
                    {
                        if (par.Value != null && par.Value.Connected)
                        {
                            par.Value.Send(datos);
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
