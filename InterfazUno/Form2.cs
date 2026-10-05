using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InterfazUno
{
    public partial class Form2 : Form
    {
        readonly string[] nombres = { "Jugador 1", "Jugador 2", "Jugador 3", "Jugador 4" };
        readonly Label[] lblNombres = new Label[4];
        readonly List<Label>[] manos = Enumerable.Range(0, 4).Select(i => new List<Label>()).ToArray();
        readonly Size tam = new Size(100, 150);
        EstadoPartida estado;
        Label lblBaraja, cartaMesa, lblInfo;
        Button btnReinicio, btnPasar, btnActualizar;
        bool ocupado, cerrado;

        public Form2()
        {
            InitializeComponent();
            Icon = new Icon(Path.Combine(Application.StartupPath, "Recursos", "logoUno.ico"));
            FormClosed += (s, e) =>
            {
                cerrado = true;
                LimpiarManos();
                cartaMesa?.Image?.Dispose();
                lblBaraja?.Image?.Dispose();
                BackgroundImage?.Dispose();
                Icon?.Dispose();
            };
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
            try
            {
                Text = "UNO";
                WindowState = FormWindowState.Maximized;
                // Conservamos el tablero, con desplazamiento en pantallas pequeñas.
                AutoScroll = true;
                AutoScrollMinSize = new Size(1450, 950);
                BackgroundImage = CargarImagen("juego_fondo.jpg", new Size(736, 368), "Recursos");
                BackgroundImageLayout = ImageLayout.Stretch;
                CrearTablero();
                await EjecutarAsync(NuevaPartidaAsync);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir el tablero"); Close(); }
        }

        private Image CargarImagen(string archivo, Size size, string carpeta = "Cartas")
        {
            string ruta = Path.Combine(Application.StartupPath, carpeta, archivo);
            using (Image original = Image.FromFile(ruta)) return new Bitmap(original, size);
        }

        private Button Boton(string texto, int x, EventHandler accion)
        {
            var b = new Button { Text = texto, Location = new Point(x, 10), Size = new Size(130, 36),
                BackColor = Color.Red, ForeColor = Color.White, Cursor = Cursors.Hand };
            b.Click += accion;
            Controls.Add(b);
            return b;
        }

        private void CrearTablero()
        {
            Point[] puntos = { new Point(610, 200), new Point(1280, 100), new Point(610, 865), new Point(25, 100) };
            for (int i = 0; i < 4; i++)
            {
                lblNombres[i] = new Label { Text = nombres[i], Location = puntos[i], Size = new Size(160, 34),
                    TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 12, FontStyle.Bold), ForeColor = Color.White };
                Controls.Add(lblNombres[i]);
            }
            lblInfo = new Label { Location = new Point(380, 540), Size = new Size(680, 90),
                TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(170, 0, 0, 0), ForeColor = Color.White };
            Controls.Add(lblInfo);
            lblBaraja = new Label { Location = new Point(580, 370), Size = tam,
                Image = CargarImagen("carta_uno.png", tam), Cursor = Cursors.Hand };
            lblBaraja.Click += async (s, e) => await EjecutarAsync(() => MoverAsync("robar"));
            Controls.Add(lblBaraja);
            cartaMesa = new Label { Location = new Point(700, 370), Size = tam };
            Controls.Add(cartaMesa);
            btnReinicio = Boton("Nueva partida", 10, async (s, e) => await EjecutarAsync(NuevaPartidaAsync));
            btnPasar = Boton("Pasar", 150, async (s, e) => await EjecutarAsync(() => MoverAsync("pasar")));
            btnActualizar = Boton("Actualizar", 290, async (s, e) => await EjecutarAsync(ActualizarAsync));
        }

        private async Task EjecutarAsync(Func<Task> accion)
        {
            if (ocupado || cerrado) return;
            ocupado = true;
            Habilitar();
            try { await accion(); }
            catch (Exception ex)
            {
                if (cerrado) return;
                // Una respuesta perdida puede haberse guardado. Consultar, nunca repetir el POST automáticamente.
                bool recuperado = false;
                if (estado != null)
                {
                    try { await ActualizarAsync(); recuperado = true; } catch { }
                }
                if (!recuperado) lblInfo.Text = "Sin conexión confirmada. Revisa API/MySQL y pulsa Actualizar.";
                MessageBox.Show(this, ex.Message + "\n\nComprueba que la API esté encendida y la migración SQL aplicada.", "No se completó la operación");
            }
            finally { ocupado = false; if (!cerrado) Habilitar(); }
        }

        private void Habilitar()
        {
            bool jugando = !ocupado && estado != null && estado.estado == "en_curso";
            btnReinicio.Enabled = !ocupado;
            btnActualizar.Enabled = !ocupado && estado != null;
            btnPasar.Enabled = jugando && estado.robada_id.HasValue;
            lblBaraja.Enabled = jugando && !estado.robada_id.HasValue;
            for (int i = 0; i < 4; i++)
                foreach (Label l in manos[i]) l.Enabled = jugando && i == estado.turno;
        }

        private async Task NuevaPartidaAsync()
        {
            var existentes = (await UnoApi.UsuariosAsync()).ToList();
            if (cerrado) return;
            var ids = new List<int>();
            foreach (string nombre in nombres)
            {
                Usuario u = existentes.FirstOrDefault(x => string.Equals(x.nombre, nombre, StringComparison.OrdinalIgnoreCase));
                if (u == null) u = await UnoApi.CrearUsuarioAsync(nombre);
                if (cerrado) return;
                ids.Add(u.id);
            }
            if (estado != null && estado.estado == "en_curso")
            {
                estado = await UnoApi.AccionAsync(estado, "abandonar");
                if (cerrado) return;
            }
            estado = await UnoApi.CrearPartidaAsync(ids.ToArray());
            if (cerrado) return;
            LimpiarManos();
            cartaMesa.Image?.Dispose(); cartaMesa.Image = null;
            for (int i = 0; i < 4; i++)
            {
                lblInfo.Text = nombres[i] + " elige carta para el sorteo";
                CrearCarta(estado.sorteo[i], i);
                await Task.Delay(450);
                if (cerrado) return;
            }
            lblInfo.Text = nombres[estado.repartidor] + " reparte";
            await Task.Delay(800);
            if (cerrado) return;
            LimpiarManos();
            foreach (Carta c in estado.cartas.Where(c => c.zona == "mano").OrderBy(c => c.orden))
            {
                int jugador = Array.FindIndex(estado.jugadores, u => u.id == c.propietario_id);
                CrearCarta(c, jugador);
                await Task.Delay(50);
                if (cerrado) return;
            }
            Dibujar();
        }

        private async Task ActualizarAsync()
        {
            if (estado == null) return;
            estado = await UnoApi.EstadoAsync(estado.id);
            if (!cerrado) Dibujar();
        }

        private async Task MoverAsync(string accion, Carta carta = null)
        {
            if (estado == null || estado.estado != "en_curso") return;
            string color = null;
            if (carta != null && carta.color == "negro")
            {
                color = ElegirColor();
                if (color == null) return;
            }
            estado = await UnoApi.AccionAsync(estado, accion, carta?.carta_id, color);
            if (!cerrado) Dibujar();
        }

        private string ElegirColor()
        {
            using (var dialogo = new Form { Text = "Elige un color", Size = new Size(390, 150),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false })
            {
                string elegido = null;
                string[] colores = { "rojo", "amarillo", "verde", "azul" };
                Color[] tonos = { Color.Red, Color.Gold, Color.LightGreen, Color.LightBlue };
                for (int i = 0; i < 4; i++)
                {
                    string color = colores[i];
                    var b = new Button { Text = color, BackColor = tonos[i], Location = new Point(10+i*90, 30), Size = new Size(85, 40) };
                    b.Click += (s, e) => { elegido = color; dialogo.DialogResult = DialogResult.OK; };
                    dialogo.Controls.Add(b);
                }
                return dialogo.ShowDialog(this) == DialogResult.OK ? elegido : null;
            }
        }

        private void Dibujar()
        {
            LimpiarManos();
            for (int i = 0; i < 4; i++)
            {
                foreach (Carta c in estado.cartas.Where(c => c.zona == "mano" && c.propietario_id == estado.jugadores[i].id)) CrearCarta(c, i);
                lblNombres[i].Text = estado.jugadores[i].nombre + " (" + manos[i].Count + ")";
                lblNombres[i].BackColor = estado.estado == "en_curso" && i == estado.turno ? Color.Gold : Color.Black;
                lblNombres[i].ForeColor = i == estado.turno ? Color.Black : Color.White;
            }
            Carta superior = estado.cartas.Where(c => c.zona == "descarte").OrderBy(c => c.orden).Last();
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = CargarImagen(superior.ArchivoImagen(), tam);
            cartaMesa.Tag = superior;
            if (estado.estado == "terminada")
                lblInfo.Text = estado.ganador_id.HasValue
                    ? "Ganó " + estado.jugadores.First(u => u.id == estado.ganador_id).nombre + ". Resultado guardado."
                    : "Partida cerrada sin ganador.";
            else
                lblInfo.Text = "Partida " + estado.id + " — Turno: " + estado.jugadores[estado.turno].nombre
                    + "\nColor: " + estado.color_activo + " | Dirección: " + (estado.direccion == 1 ? "horaria" : "antihoraria")
                    + " | Mazo: " + estado.cartas.Count(c => c.zona == "mazo")
                    + (estado.robada_id.HasValue ? "\nJuega la carta robada o pulsa Pasar." : "\nPulsa una carta para jugar o el mazo para robar.");
            Habilitar();
        }

        private void CrearCarta(Carta carta, int jugador)
        {
            int total = estado == null ? 7 : Math.Max(7, estado.cartas.Count(c => c.zona == "mano" && c.propietario_id == estado.jugadores[jugador].id));
            int paso = Math.Min(80, 650 / Math.Max(1, total-1));
            int desplazamiento = manos[jugador].Count * paso;
            Image imagen = CargarImagen(carta.ArchivoImagen(), tam);
            var l = new Label { Image = imagen, Tag = carta, Cursor = Cursors.Hand, Size = tam };
            if (jugador == 0) l.Location = new Point(350+desplazamiento, 45);
            if (jugador == 2) l.Location = new Point(350+desplazamiento, 700);
            if (jugador == 1 || jugador == 3)
            {
                imagen.RotateFlip(jugador == 1 ? RotateFlipType.Rotate270FlipNone : RotateFlipType.Rotate90FlipNone);
                l.Size = new Size(150, 100);
                l.Location = new Point(jugador == 1 ? 1280 : 25, 150+desplazamiento);
            }
            // No altera dimensiones acumulativamente al entrar/salir con el ratón.
            l.MouseEnter += (s, e) => l.BorderStyle = BorderStyle.Fixed3D;
            l.MouseLeave += (s, e) => l.BorderStyle = BorderStyle.None;
            l.Click += async (s, e) =>
            {
                if (estado != null && jugador == estado.turno)
                    await EjecutarAsync(() => MoverAsync("jugar", (Carta)l.Tag));
            };
            l.Enabled = false;
            manos[jugador].Add(l);
            Controls.Add(l);
            l.BringToFront();
        }

        private void LimpiarManos()
        {
            foreach (var mano in manos)
            {
                foreach (var l in mano) { Controls.Remove(l); l.Image?.Dispose(); l.Dispose(); }
                mano.Clear();
            }
        }
    }
}
