// HISTORIAL DE PARTIDAS GANADAS (código compartido por ServidorUno e InterfazUno)
//
// Se guarda en la misma base MySQL que usa la API de Python (db.py / schema.sql: uno_clase),
// reutilizando las tablas usuarios, partidas y participantes; no hace falta migrar nada.
// La conexión se configura con las mismas variables de entorno que db.py:
//   UNO_DB_HOST (127.0.0.1), UNO_DB_PORT (3306), UNO_DB_USER (uno_app),
//   UNO_DB_PASSWORD (vacía) y UNO_DB_NAME (uno_clase).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

namespace UnoDatos
{
    public class EstadisticaJugador
    {
        public string Nombre { get; set; }
        public int Ganadas { get; set; }
        public int Jugadas { get; set; }
        public DateTime? UltimaVictoria { get; set; }

        public int PorcentajeVictorias
        {
            get { return Jugadas == 0 ? 0 : (int)Math.Round(100.0 * Ganadas / Jugadas); }
        }
    }

    public class PartidaGanada
    {
        public int Id { get; set; }
        public DateTime? Fecha { get; set; }
        public string Ganador { get; set; }
    }

    public class HistorialDatos
    {
        public List<EstadisticaJugador> Jugadores { get; } = new List<EstadisticaJugador>();
        public List<PartidaGanada> Recientes { get; } = new List<PartidaGanada>();
    }

    // ACCESO DIRECTO A MYSQL
    // El servidor lo usa para responder a los clientes en red; el cliente lo usa en modo local.
    public static class HistorialDb
    {
        static string Variable(string nombre, string porDefecto)
        {
            string valor = Environment.GetEnvironmentVariable(nombre);
            return string.IsNullOrEmpty(valor) ? porDefecto : valor;
        }

        static MySqlConnectionStringBuilder Configuracion()
        {
            uint puerto;
            if (!uint.TryParse(Variable("UNO_DB_PORT", "3306"), out puerto))
                puerto = 3306;

            return new MySqlConnectionStringBuilder
            {
                Server = Variable("UNO_DB_HOST", "127.0.0.1"),
                Port = puerto,
                UserID = Variable("UNO_DB_USER", "uno_app"),
                Password = Variable("UNO_DB_PASSWORD", ""),
                Database = Variable("UNO_DB_NAME", "uno_clase"),
                CharacterSet = "utf8mb4",
                ConnectionTimeout = 5,
                AllowPublicKeyRetrieval = true
            };
        }

        // Texto para mostrar a qué base se conecta (sin la contraseña)
        public static string Descripcion
        {
            get
            {
                var c = Configuracion();
                return c.UserID + "@" + c.Server + ":" + c.Port + "/" + c.Database;
            }
        }

        static MySqlConnection Abrir()
        {
            var conexion = new MySqlConnection(Configuracion().ConnectionString);
            try
            {
                conexion.Open();
                return conexion;
            }
            catch (MySqlException ex)
            {
                conexion.Dispose();
                throw new InvalidOperationException(
                    "No se pudo conectar a MySQL (" + Descripcion + "): " + ex.Message, ex);
            }
            catch
            {
                conexion.Dispose();
                throw;
            }
        }

        public static void ProbarConexion()
        {
            using (Abrir()) { }
        }

        // Guarda una partida terminada: da de alta a los jugadores que no existan en `usuarios`,
        // inserta la partida con su ganador y a cada jugador como participante (todo o nada).
        public static int RegistrarPartida(IList<string> jugadores, int ganador)
        {
            if (jugadores == null || jugadores.Count < 2)
                throw new ArgumentException("Se necesitan al menos 2 jugadores.");
            if (ganador < 0 || ganador >= jugadores.Count)
                throw new ArgumentOutOfRangeException(nameof(ganador));

            List<string> nombres = jugadores.Select(n => (n ?? "").Trim()).ToList();
            if (nombres.Any(n => n.Length == 0 || n.Length > 50))
                throw new ArgumentException("Hay nombres de jugador vacíos o de más de 50 letras.");
            if (nombres.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nombres.Count)
                throw new ArgumentException("Los nombres de los jugadores se repiten.");

            using (MySqlConnection conexion = Abrir())
            using (MySqlTransaction tx = conexion.BeginTransaction())
            {
                List<int> ids = nombres.Select(n => IdUsuario(conexion, tx, n)).ToList();

                long partidaId;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO partidas (estado, terminada_en, ganador_id) " +
                    "VALUES ('terminada', CURRENT_TIMESTAMP, @ganador)", conexion, tx))
                {
                    cmd.Parameters.AddWithValue("@ganador", ids[ganador]);
                    cmd.ExecuteNonQuery();
                    partidaId = cmd.LastInsertedId;
                }

                for (int asiento = 0; asiento < ids.Count; asiento++)
                {
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO participantes (partida_id, usuario_id, asiento) " +
                        "VALUES (@partida, @usuario, @asiento)", conexion, tx))
                    {
                        cmd.Parameters.AddWithValue("@partida", partidaId);
                        cmd.Parameters.AddWithValue("@usuario", ids[asiento]);
                        cmd.Parameters.AddWithValue("@asiento", asiento);
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return (int)partidaId;
            }
        }

        static int IdUsuario(MySqlConnection conexion, MySqlTransaction tx, string nombre)
        {
            const string buscar = "SELECT id FROM usuarios WHERE nombre = @nombre";
            object id = Ejecutar(conexion, tx, buscar, nombre, true);
            if (id == null || id == DBNull.Value)
            {
                Ejecutar(conexion, tx, "INSERT IGNORE INTO usuarios (nombre) VALUES (@nombre)", nombre, false);
                id = Ejecutar(conexion, tx, buscar, nombre, true);
            }
            return Convert.ToInt32(id);
        }

        static object Ejecutar(MySqlConnection conexion, MySqlTransaction tx, string sql, string nombre, bool escalar)
        {
            using (var cmd = new MySqlCommand(sql, conexion, tx))
            {
                cmd.Parameters.AddWithValue("@nombre", nombre);
                if (escalar)
                    return cmd.ExecuteScalar();
                cmd.ExecuteNonQuery();
                return null;
            }
        }

        // Victorias por jugador (solo quienes ya jugaron alguna partida terminada)
        // y las últimas partidas con ganador.
        public static HistorialDatos Consultar(int limiteRecientes = 20)
        {
            const string sqlJugadores = @"
SELECT t.nombre, t.ganadas, t.jugadas, t.ultima
FROM (
    SELECT u.nombre,
           (SELECT COUNT(*) FROM partidas p
             WHERE p.ganador_id = u.id AND p.estado = 'terminada') AS ganadas,
           (SELECT COUNT(*) FROM participantes pa JOIN partidas p ON p.id = pa.partida_id
             WHERE pa.usuario_id = u.id AND p.estado = 'terminada') AS jugadas,
           (SELECT MAX(p.terminada_en) FROM partidas p
             WHERE p.ganador_id = u.id AND p.estado = 'terminada') AS ultima
    FROM usuarios u
) t
WHERE t.jugadas > 0 OR t.ganadas > 0
ORDER BY t.ganadas DESC, t.jugadas ASC, t.nombre";

            const string sqlRecientes = @"
SELECT p.id, p.terminada_en, u.nombre AS ganador
FROM partidas p
JOIN usuarios u ON u.id = p.ganador_id
WHERE p.estado = 'terminada'
ORDER BY p.terminada_en DESC, p.id DESC
LIMIT @limite";

            var datos = new HistorialDatos();
            using (MySqlConnection conexion = Abrir())
            {
                using (var cmd = new MySqlCommand(sqlJugadores, conexion))
                using (MySqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        datos.Jugadores.Add(new EstadisticaJugador
                        {
                            Nombre = r.GetString(0),
                            Ganadas = Convert.ToInt32(r.GetValue(1)),
                            Jugadas = Convert.ToInt32(r.GetValue(2)),
                            UltimaVictoria = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3)
                        });
                    }
                }

                using (var cmd = new MySqlCommand(sqlRecientes, conexion))
                {
                    cmd.Parameters.AddWithValue("@limite", limiteRecientes);
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            datos.Recientes.Add(new PartidaGanada
                            {
                                Id = Convert.ToInt32(r.GetValue(0)),
                                Fecha = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1),
                                Ganador = r.GetString(2)
                            });
                        }
                    }
                }
            }
            return datos;
        }
    }

    // TRAMAS DE TEXTO DEL HISTORIAL (una línea terminada en '\n', campos separados por '|')
    //   Cliente -> Servidor: HISTORIAL                              pide el historial
    //   Cliente -> Servidor: RESULTADO|ganador|nombre1,nombre2,...  guarda una partida ganada
    //   Servidor -> Cliente: HISTORIAL|OK|jugadores|recientes       solo a quien lo pidió
    //   Servidor -> Cliente: HISTORIAL|ERROR|mensaje
    // Los registros van separados por ';' y sus campos por ','. Cada texto pasa por
    // Uri.EscapeDataString para que un nombre con ',', ';' o '|' no rompa la trama.
    public static class TramaHistorial
    {
        public const string Comando = "HISTORIAL";
        public const string ComandoResultado = "RESULTADO";
        const string FormatoFecha = "yyyyMMddHHmmss";

        public static string Respuesta(HistorialDatos datos)
        {
            string jugadores = string.Join(";", datos.Jugadores.Select(j => string.Join(",",
                Texto(j.Nombre), j.Ganadas.ToString(CultureInfo.InvariantCulture),
                j.Jugadas.ToString(CultureInfo.InvariantCulture), Fecha(j.UltimaVictoria))));

            string recientes = string.Join(";", datos.Recientes.Select(p => string.Join(",",
                p.Id.ToString(CultureInfo.InvariantCulture), Fecha(p.Fecha), Texto(p.Ganador))));

            return Comando + "|OK|" + jugadores + "|" + recientes;
        }

        public static string Error(string mensaje)
        {
            return Comando + "|ERROR|" + Texto(mensaje);
        }

        // Convierte la respuesta del servidor; si el servidor reportó un error lo lanza como excepción
        public static HistorialDatos LeerRespuesta(string trama)
        {
            string[] partes = trama.Split('|');
            if (partes.Length < 2 || partes[0] != Comando)
                throw new FormatException("El servidor no envió el historial. Revisa que ServidorUno esté actualizado.");

            if (partes[1] == "ERROR")
                throw new InvalidOperationException(partes.Length > 2
                    ? Uri.UnescapeDataString(partes[2]) : "Error desconocido en el servidor.");

            if (partes[1] != "OK" || partes.Length < 4)
                throw new FormatException("La respuesta del historial llegó incompleta.");

            var datos = new HistorialDatos();
            foreach (string[] c in Registros(partes[2], 4))
            {
                datos.Jugadores.Add(new EstadisticaJugador
                {
                    Nombre = Uri.UnescapeDataString(c[0]),
                    Ganadas = int.Parse(c[1], CultureInfo.InvariantCulture),
                    Jugadas = int.Parse(c[2], CultureInfo.InvariantCulture),
                    UltimaVictoria = LeerFecha(c[3])
                });
            }
            foreach (string[] c in Registros(partes[3], 3))
            {
                datos.Recientes.Add(new PartidaGanada
                {
                    Id = int.Parse(c[0], CultureInfo.InvariantCulture),
                    Fecha = LeerFecha(c[1]),
                    Ganador = Uri.UnescapeDataString(c[2])
                });
            }
            return datos;
        }

        public static string NuevoResultado(int ganador, IEnumerable<string> jugadores)
        {
            return ComandoResultado + "|" + ganador.ToString(CultureInfo.InvariantCulture) + "|" +
                   string.Join(",", jugadores.Select(Texto));
        }

        public static bool LeerResultado(string trama, out int ganador, out string[] jugadores)
        {
            jugadores = null;
            string[] partes = trama.Split('|');
            if (partes.Length < 3 || partes[0] != ComandoResultado ||
                !int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ganador))
            {
                ganador = -1;
                return false;
            }

            jugadores = partes[2].Split(',').Select(Uri.UnescapeDataString).ToArray();
            return ganador >= 0 && ganador < jugadores.Length;
        }

        static IEnumerable<string[]> Registros(string texto, int campos)
        {
            return texto.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Split(','))
                        .Where(c => c.Length >= campos);
        }

        static string Texto(string valor)
        {
            return Uri.EscapeDataString(valor ?? "");
        }

        static string Fecha(DateTime? fecha)
        {
            return fecha.HasValue ? fecha.Value.ToString(FormatoFecha, CultureInfo.InvariantCulture) : "";
        }

        static DateTime? LeerFecha(string texto)
        {
            DateTime fecha;
            return DateTime.TryParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out fecha) ? fecha : (DateTime?)null;
        }
    }
}
