using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace InterfazUno
{
    public static class UnoApi
    {
        private static readonly HttpClient Cliente = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("UNO_API_URL") ?? "http://127.0.0.1:8000/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static async Task<T> SolicitarAsync<T>(string ruta, bool guardar = false)
        {
            using (HttpResponseMessage respuesta = guardar
                ? await Cliente.PostAsync(ruta, null) : await Cliente.GetAsync(ruta))
            {
                string json = await respuesta.Content.ReadAsStringAsync();
                var lector = new JavaScriptSerializer();
                if (!respuesta.IsSuccessStatusCode)
                {
                    string detalle = json;
                    try { detalle = lector.Deserialize<ErrorApi>(json).detail; } catch { }
                    throw new HttpRequestException("API " + (int)respuesta.StatusCode + ": " + detalle);
                }
                return lector.Deserialize<T>(json);
            }
        }

        public static Task<Usuario[]> UsuariosAsync() { return SolicitarAsync<Usuario[]>("usuarios"); }
        public static Task<Usuario> CrearUsuarioAsync(string nombre)
        { return SolicitarAsync<Usuario>("usuarios?nombre=" + Uri.EscapeDataString(nombre), true); }
        public static Task<EstadoPartida> CrearPartidaAsync(params int[] ids)
        { return SolicitarAsync<EstadoPartida>("partidas?" + string.Join("&", ids.Select(id => "jugadores=" + id)), true); }
        public static Task<EstadoPartida> EstadoAsync(int id)
        { return SolicitarAsync<EstadoPartida>("partidas/" + id); }
        public static Task<EstadoPartida> AccionAsync(EstadoPartida e, string accion, int? cartaId = null, string color = null)
        {
            string ruta = "partidas/" + e.id + "/" + accion + "?version=" + e.version
                + "&usuario_id=" + e.jugadores[e.turno].id;
            if (cartaId.HasValue) ruta += "&carta_id=" + cartaId.Value;
            if (color != null) ruta += "&color=" + Uri.EscapeDataString(color);
            return SolicitarAsync<EstadoPartida>(ruta, true);
        }
    }
    public class ErrorApi { public string detail { get; set; } }
    public class Usuario
    {
        public int id { get; set; }
        public string nombre { get; set; }
    }
    public class Carta
    {
        public int carta_id { get; set; }
        public string color { get; set; }
        public string valor { get; set; }
        public string zona { get; set; }
        public int? propietario_id { get; set; }
        public int orden { get; set; }
        public string ArchivoImagen()
        {
            switch (valor)
            {
                case "salto": return "bloqueo_" + color + ".png";
                case "reversa": return "reverse_" + color + ".png";
                case "+2": return "mas_dos_" + color + ".png";
                case "+4": return "mas_cuatro_color.png";
                case "comodin": return "cambiar_color.png";
                default: return valor + "_" + color + ".png";
            }
        }
    }
    public class EstadoPartida
    {
        public int id { get; set; }
        public string estado { get; set; }
        public int? ganador_id { get; set; }
        public int turno { get; set; }
        public int direccion { get; set; }
        public string color_activo { get; set; }
        public int repartidor { get; set; }
        public int? robada_id { get; set; }
        public int version { get; set; }
        public Usuario[] jugadores { get; set; }
        public Carta[] cartas { get; set; }
        public Carta[] sorteo { get; set; }
    }
}
