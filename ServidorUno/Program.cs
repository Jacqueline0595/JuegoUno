// C# MULTI-THREADED SERVER PARA JUEGO UNO EN RED
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnoDatos;

namespace Servers
{
    class Program
    {
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

                // si mysql falla el servidor sigue funcionando, nomas no hay historial
                ProbarBaseDeDatos();

                while (true)
                {
                    Socket clienteSocket = myList.AcceptSocket();
                    try
                    {
                        AceptarJugador(clienteSocket);
                    }
                    catch (Exception ex)
                    {
                        // si un cliente falla que no tumbe a los demas
                        Console.WriteLine("[AVISO] No se pudo atender una conexión nueva: " + ex.Message);
                        try { clienteSocket.Close(); } catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error crítico en el Servidor: " + e.Message);
            }

            Console.ReadLine();
        }

        static void AceptarJugador(Socket clienteSocket)
        {
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
                // Si ya están los 4 lugares ocupados, rechazamos la conexión extra
                EnviarDirecto(clienteSocket, "ERROR|Sala llena (4 jugadores máximo).");
                clienteSocket.Close();
                return;
            }

            Console.WriteLine($"\n[CONECTADO] Jugador {idAsignado + 1} (ID {idAsignado}) desde: {clienteSocket.RemoteEndPoint}");

            int idActual = idAsignado;
            Thread hiloJugador = new Thread(() => AtenderJugador(clienteSocket, idActual));
            hiloJugador.IsBackground = true;
            hiloJugador.Start();
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

                            // Las tramas del historial las atiende el servidor y no se reenvían
                            if (trama == TramaHistorial.Comando)
                            {
                                EnviarHistorial(socketCliente, idJugador);
                                continue;
                            }
                            if (trama.StartsWith(TramaHistorial.ComandoResultado + "|"))
                            {
                                GuardarResultado(trama);
                                continue;
                            }

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

        // HISTORIAL DE PARTIDAS (MySQL)
        // Las consultas corren en otro hilo para no frenar la lectura de las tramas del juego.

        static void ProbarBaseDeDatos()
        {
            Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("[HISTORIAL] Base de datos: " + HistorialDb.Descripcion);
                    HistorialDb.ProbarConexion();
                    Console.WriteLine("[HISTORIAL] Conexión con MySQL correcta.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[HISTORIAL] Aviso: " + MensajeErrorBd(ex));
                    Console.WriteLine("            El juego funciona, pero el historial no estará disponible.");
                }
            });
        }

        // Responde solo al cliente que pidió el historial
        static void EnviarHistorial(Socket destino, int idJugador)
        {
            Task.Run(() =>
            {
                string respuesta;
                try
                {
                    respuesta = TramaHistorial.Respuesta(HistorialDb.Consultar());
                    Console.WriteLine($"[HISTORIAL] Enviado al Jugador {idJugador + 1}.");
                }
                catch (Exception ex)
                {
                    string mensaje = MensajeErrorBd(ex);
                    Console.WriteLine("[HISTORIAL] Error al consultar MySQL: " + mensaje);
                    respuesta = TramaHistorial.Error("No se pudo leer la base de datos del servidor: " + mensaje);
                }
                EnviarDirecto(destino, respuesta);
            });
        }

        // La PC del ganador avisa al terminar una ronda: RESULTADO|ganador|nombre1,nombre2,...
        static void GuardarResultado(string trama)
        {
            int ganador;
            string[] jugadores;
            if (!TramaHistorial.LeerResultado(trama, out ganador, out jugadores))
            {
                Console.WriteLine("[HISTORIAL] Trama RESULTADO inválida, se ignora.");
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    int partidaId = HistorialDb.RegistrarPartida(jugadores, ganador);
                    Console.WriteLine($"[HISTORIAL] Partida #{partidaId} guardada. Ganó {jugadores[ganador]}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[HISTORIAL] No se pudo guardar la partida: " + MensajeErrorBd(ex));
                }
            });
        }

        // si falta MySql.Data el error sale raro, mejor decir que hacer
        static string MensajeErrorBd(Exception ex)
        {
            Exception causa = ex.GetBaseException();
            if (causa is System.IO.FileNotFoundException || causa is System.IO.FileLoadException)
                return "Falta la librería MySql.Data. Compila ServidorUno en Visual Studio " +
                       "(o con 'msbuild /t:Restore;Build') para que se descarguen los paquetes NuGet.";
            return ex.Message;
        }

        // Usa el mismo candado que Broadcast para que dos hilos no escriban a la vez en el mismo socket
        static void EnviarDirecto(Socket destino, string trama)
        {
            try
            {
                byte[] datos = Encoding.UTF8.GetBytes(trama + "\n");
                lock (bloqueoLista)
                {
                    if (destino != null && destino.Connected)
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
