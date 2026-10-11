using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InterfazUno
{
    public partial class Form2 : Form
    {
        readonly bool USAR_API = false;
        const int PUNTOS_META = 200;

        readonly string[] nombres = { "Jugador 1", "Jugador 2", "Jugador 3", "Jugador 4" };
        readonly string[] coloresUno = { "rojo", "verde", "azul", "amarillo" };
        readonly string[] reservadas = { "carta_vacia.png", "carta_uno.png", "carta_vacia.png", "carta_uno.png" };
        readonly Size tam = new Size(100, 150);
        readonly Size tam_2 = new Size(150, 100);
        readonly Size[] levante = { new Size(0, 30), new Size(-30, 0), new Size(0, -30), new Size(30, 0) };
        readonly ContentAlignment[] alineaLevante =
        {
            ContentAlignment.BottomLeft, ContentAlignment.TopLeft, ContentAlignment.TopLeft, ContentAlignment.TopRight
        };

        readonly Label[] lblNombres = new Label[4];
        readonly List<Label>[] manos = Enumerable.Range(0, 4).Select(i => new List<Label>()).ToArray();
        readonly int[] puntajes = new int[4];
        readonly Random ran = new Random();
        Label lblBaraja, cartaMesa, lblInfo;
        Button btnReinicio, btnPasar, btnActualizar;

        EstadoPartida estado;
        int ultimaRondaPuntuada;
        bool ocupado, cerrado;

        List<string> usados;
        string[] archivos;

        string colorActivo;
        int partida, repartidor, turno, direccion = 1;
        bool puedeJugar, torneoTerminado;

        string ruta_d = Path.Combine(Application.StartupPath, "Cartas") + "\\";
        string texto_elim = Path.Combine(Application.StartupPath, "Cartas") + "\\";

        public Form2()
        {
            InitializeComponent();

            Icon = new Icon(Path.Combine(Application.StartupPath, "Recursos", "logoUno.ico"));
            FormClosed += (s, e) =>
            {
                cerrado = true;
                partida++;
                LimpiarManos();
                cartaMesa?.Image?.Dispose();
                lblBaraja?.Image?.Dispose();
                BackgroundImage?.Dispose();
                Icon?.Dispose();
            };

            this.Icon = new Icon("Recursos\\logoUno.ico");
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
            try
            {
                Text = "UNO";
                WindowState = FormWindowState.Maximized;
                AutoScroll = true;
                AutoScrollMinSize = new Size(1450, 950);
                BackgroundImage = CargarImagen("juego_fondo.jpg", new Size(736, 368), "Recursos");
                BackgroundImageLayout = ImageLayout.Stretch;
                archivos = Directory.GetFiles(Path.Combine(Application.StartupPath, "Cartas"))
                                    .Select(f => Path.GetFileName(f)).ToArray();
                CrearTablero();
                await ReiniciarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "No se pudo abrir el tablero");
                Close();
            }
        }

        private Image CargarImagen(string archivo, Size size, string carpeta = "Cartas")
        {
            string ruta = Path.Combine(Application.StartupPath, carpeta, archivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException("No se encontró la imagen: " + ruta);
            using (Image original = Image.FromFile(ruta)) return new Bitmap(original, size);
        }

        private Button Boton(string texto, int x, EventHandler accion)
        {
            var b = new Button
            {
                Text = texto,
                Location = new Point(x, 10),
                Size = new Size(130, 36),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.Click += accion;
            Controls.Add(b);
            return b;
        }

        private void CrearTablero()
        {
            Point[] puntos = { new Point(610, 195), new Point(1295, 100), new Point(610, 855), new Point(45, 100) };
            for (int i = 0; i < 4; i++)
            {
                lblNombres[i] = new Label
                {
                    Location = puntos[i],
                    Size = new Size(160, 50),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold)
                };
                Controls.Add(lblNombres[i]);
            }
            ActualizarNombres();
            Resaltar(-1);

            lblInfo = new Label
            {
                Location = new Point(380, 540),
                Size = new Size(680, 90),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(170, 0, 0, 0),
                ForeColor = Color.White
            };
            Controls.Add(lblInfo);

            lblBaraja = new Label { Location = new Point(580, 370), Size = tam, Image = CargarImagen("carta_uno.png", tam), Cursor = Cursors.Hand };
            lblBaraja.Click += Baraja_Click;
            Controls.Add(lblBaraja);

            cartaMesa = new Label { Location = new Point(700, 370), Size = tam };
            Controls.Add(cartaMesa);

            btnReinicio = Boton("Nueva partida", 10, async (s, e) => await ReiniciarAsync());
            btnPasar = Boton("Pasar", 150, async (s, e) => await EjecutarAsync(() => MoverAsync("pasar")));
            btnActualizar = Boton("Actualizar", 290, async (s, e) => await EjecutarAsync(ActualizarAsync));
            btnPasar.Visible = USAR_API;
            btnActualizar.Visible = USAR_API;
        }

        private async Task ReiniciarAsync()
        {
            if (USAR_API)
            {
                await EjecutarAsync(() => NuevaPartidaAsync(true));
                return;
            }
            Array.Clear(puntajes, 0, puntajes.Length);
            this.Text = "Uno";
            this.WindowState = FormWindowState.Maximized;
       
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.CenterToScreen();
            Image fondo = CargarImagen("juego_fondo.jpg", new Size(736, 368), "Recursos");
            this.BackgroundImage = fondo;
            this.BackgroundImageLayout = ImageLayout.Stretch;
            archivos = Directory.GetFiles(ruta_d);
            // CrearNombres();
            // CrearBaraja();
            // CrearBotonReinicio();
            await IniciarJuego();
        }

        private async void Baraja_Click(object sender, EventArgs e)
        {
            if (USAR_API) await EjecutarAsync(RobarAsync);
            else await Bloquear(RobarLocalAsync);
        }

        private async void Carta_Click(object sender, EventArgs e)
        {
            Label carta = (Label)sender;
            int jugador = int.Parse(carta.Name);
            if (USAR_API)
            {
                if (estado != null && jugador == estado.turno)
                    await EjecutarAsync(() => MoverAsync("jugar", (Carta)carta.Tag));
            }
            else
            {
                await Bloquear(() => JugarCartaAsync(carta, jugador));
            }
        }

        private async Task EjecutarAsync(Func<Task> accion)
        {
            if (ocupado || cerrado)
                return;
            ocupado = true;
            Habilitar();
            try
            {
                await accion();
                await RevisarFinRondaAsync();
            }
            catch (Exception ex)
            {
                if (cerrado)
                    return;

                bool recuperado = false;
                if (estado != null)
                {
                    try
                    {
                        await ActualizarAsync();
                        recuperado = true;
                    }
                    catch { }
                }
                if (!recuperado)
                    lblInfo.Text = "Sin conexión confirmada. Revisa API/MySQL y pulsa Actualizar.";
                MessageBox.Show(this, ex.Message + "\n\nComprueba que la API esté encendida y la migración SQL aplicada.", "No se completó la operación");
            }
            finally
            {
                ocupado = false;
                if (!cerrado)
                    Habilitar();
            }
        }

        private void Habilitar()
        {
            bool jugando = !ocupado && estado != null && estado.estado == "en_curso";
            btnReinicio.Enabled = !ocupado;
            btnActualizar.Enabled = !ocupado && estado != null;
            btnPasar.Enabled = jugando && estado.robada_id.HasValue;
            lblBaraja.Enabled = jugando && !estado.robada_id.HasValue;
            for (int i = 0; i < 4; i++)
                foreach (Label l in manos[i])
                    l.Enabled = jugando && i == estado.turno;
        }

        private async Task NuevaPartidaAsync(bool reiniciarPuntos = false)
        {
            if (reiniciarPuntos)
                Array.Clear(puntajes, 0, puntajes.Length);

            var existentes = (await UnoApi.UsuariosAsync()).ToList();
            if (cerrado)
                return;
            var ids = new List<int>();
            foreach (string nombre in nombres)
            {
                Usuario u = existentes.FirstOrDefault(x => string.Equals(x.nombre, nombre, StringComparison.OrdinalIgnoreCase));
                if (u == null)
                    u = await UnoApi.CrearUsuarioAsync(nombre);
                if (cerrado)
                    return;
                ids.Add(u.id);
            }

            if (estado != null && estado.estado == "en_curso")
            {
                estado = await UnoApi.AccionAsync(estado, "abandonar");
                if (cerrado)
                    return;
            }
            estado = await UnoApi.CrearPartidaAsync(ids.ToArray());
            if (cerrado)
                return;

            LimpiarManos();
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = null;
            for (int i = 0; i < 4; i++)
            {
                Resaltar(i);
                lblInfo.Text = nombres[i] + " elige una carta";
                CrearCarta(estado.sorteo[i], i);
                await Task.Delay(800);
                if (cerrado)
                    return;
            }
            Resaltar(estado.repartidor);
            lblInfo.Text = nombres[estado.repartidor] + " reparte";
            await Task.Delay(1500);
            if (cerrado)
                return;

            LimpiarManos();
            foreach (Carta c in estado.cartas.Where(c => c.zona == "mano").OrderBy(c => c.orden))
            {
                int jugador = Array.FindIndex(estado.jugadores, u => u.id == c.propietario_id);
                CrearCarta(c, jugador, jugador == estado.turno);
                await Task.Delay(50);
                if (cerrado)
                    return;
            }
            Dibujar();
        }

        private async Task ActualizarAsync()
        {
            if (estado == null)
                return;
            estado = await UnoApi.EstadoAsync(estado.id);
            if (!cerrado)
                Dibujar();
        }

        private async Task MoverAsync(string accion, Carta carta = null)
        {
            if (estado == null || estado.estado != "en_curso")
                return;
            string color = null;
            if (carta != null && carta.color == "negro")
            {
                color = ElegirColor();
                if (color == null)
                    return;
            }
            estado = await UnoApi.AccionAsync(estado, accion, carta?.carta_id, color);
            if (!cerrado)
                Dibujar();
        }

        private async Task RobarAsync()
        {
            await MoverAsync("robar");
            if (cerrado || estado == null || estado.estado != "en_curso" || !estado.robada_id.HasValue)
                return;

            Carta robada = estado.cartas.First(c => c.carta_id == estado.robada_id.Value);
            Carta mesa = estado.cartas.Where(c => c.zona == "descarte").OrderBy(c => c.orden).Last();
            string nombre = robada.ArchivoImagen().Replace(".png", "");

            if (!EsJugadaValida(robada, mesa))
            {
                MessageBox.Show(this, "Robaste " + nombre + ", pero no se puede jugar. Se pasa el turno.", "UNO");
                await MoverAsync("pasar");
                return;
            }

            DialogResult respuesta = MessageBox.Show(this,
                "¡Robaste una carta jugable (" + nombre + ")!\n\n¿Quieres bajarla a la mesa?\n(Sí = jugarla, No = quedártela y pasar)",
                "Carta jugable", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (respuesta == DialogResult.Yes)
                await MoverAsync("jugar", robada);
            else
                await MoverAsync("pasar");
        }

        private bool EsJugadaValida(Carta mano, Carta mesa)
        {
            if (mano.color == "negro" || mano.color == estado.color_activo)
                return true;

            string[] colores = { "rojo", "verde", "azul", "amarillo", "negro" };
            var partesMano = mano.ArchivoImagen().ToLower().Replace(".png", "").Split('_').Except(colores);
            var partesMesa = mesa.ArchivoImagen().ToLower().Replace(".png", "").Split('_').Except(colores);
            return partesMano.Intersect(partesMesa).Any();
        }

        private async Task RevisarFinRondaAsync()
        {
            if (!USAR_API || cerrado || estado == null || estado.estado != "terminada" || !estado.ganador_id.HasValue)
                return;
            if (estado.id == ultimaRondaPuntuada)
                return;
            ultimaRondaPuntuada = estado.id;

            int ganador = Array.FindIndex(estado.jugadores, u => u.id == estado.ganador_id.Value);
            if (ganador < 0)
                return;

            if (RegistrarPuntos(ganador))
                await NuevaPartidaAsync();
        }

        private void Dibujar()
        {
            LimpiarManos();
            for (int i = 0; i < 4; i++)
            {
                foreach (Carta c in estado.cartas.Where(c => c.zona == "mano" && c.propietario_id == estado.jugadores[i].id))
                    CrearCarta(c, i, i == estado.turno);
            }
            ActualizarNombres();
            Resaltar(estado.estado == "en_curso" ? estado.turno : -1);

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

        private async Task IniciarJuego()
        {
            int id = ++partida;
            puedeJugar = false;
            torneoTerminado = false;
            usados = new List<string>(reservadas);
            LimpiarManos();
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = null;
            cartaMesa.Tag = null;
            Resaltar(-1);
            ActualizarNombres();
            lblInfo.Text = "Cada jugador elige una carta";

            await Task.Delay(800);
            if (id != partida)
                return;
            await ElegirRepartidor(id);
            if (id != partida)
                return;
            await RepartirCartas(id);
            if (id != partida)
                return;
            puedeJugar = true;
        }

        private async Task ElegirRepartidor(int id)
        {
            while (true)
            {
                var temp = new List<string>(reservadas);
                int[] valores = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    Resaltar(i);
                    lblInfo.Text = nombres[i] + " elige una carta";
                    string nombre = CartaAlAzar(temp);
                    CrearCarta(nombre, i);
                    valores[i] = ValorCarta(nombre);
                    await Task.Delay(800);
                    if (id != partida)
                        return;
                }
                int max = valores.Max();
                int cuantos = valores.Count(v => v == max);
                repartidor = Array.IndexOf(valores, max);
                if (cuantos == 1)
                {
                    Resaltar(repartidor);
                    lblInfo.Text = nombres[repartidor] + " reparte";
                }
                else
                {
                    Resaltar(-1);
                    lblInfo.Text = "Empate, se repite";
                }
                await Task.Delay(1800);
                if (id != partida)
                    return;
                LimpiarManos();
                if (cuantos == 1)
                    return;
            }
        }

        private async Task RepartirCartas(int id)
        {
            Resaltar(-1);
            lblInfo.Text = nombres[repartidor] + " reparte";
            int jugador = (repartidor + 1) % 4;
            for (int i = 0; i < 28; i++)
            {
                CrearCarta(CartaAlAzar(usados), jugador);
                jugador = (jugador + 1) % 4;
                await Task.Delay(50);
                if (id != partida)
                    return;
            }

            string centro = CartaAlAzar(usados);
            while (TipoDe(centro) == "mas_cuatro")
            {
                usados.Remove(centro);
                centro = CartaAlAzar(usados);
            }
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = CargarImagen(centro, tam);
            cartaMesa.Tag = centro;
            colorActivo = ColorDe(centro);

            direccion = 1;
            turno = (repartidor + 1) % 4;
            string msg = "Repartió " + nombres[repartidor];
            switch (TipoDe(centro))
            {
                case "reverse":
                    direccion = -1;
                    turno = repartidor;
                    msg += ". Se invierte el sentido";
                    break;
                case "bloqueo":
                    msg += ". " + nombres[turno] + " fue bloqueado";
                    turno = Siguiente(turno);
                    break;
                case "mas_dos":
                    msg += ". " + nombres[turno] + " toma dos cartas";
                    DarCartas(turno, 2);
                    turno = Siguiente(turno);
                    break;
                case "cambiar_color":
                    colorActivo = ElegirColor() ?? "rojo";
                    msg += ". " + nombres[turno] + " cambia el color a " + colorActivo;
                    break;
            }
            Resaltar(turno);
            RefrescarColores();
            lblInfo.Text = msg + ". Empieza " + nombres[turno];
        }

        private string CartaAlAzar(List<string> lista)
        {
            List<string> disponibles = Disponibles(lista);
            if (disponibles.Count == 0 && lista == usados)
            {
                Reciclar();
                disponibles = Disponibles(lista);
            }
            string nombre = disponibles[ran.Next(disponibles.Count)];
            lista.Add(nombre);
            return nombre;
        }

        private List<string> Disponibles(List<string> lista)
        {
            return archivos.Where(a => !Comprueba_usados(lista, a)).ToList();
        }

        private bool Comprueba_usados(List<string> lista, string carta)
        {
            int veces = lista.Count(u => u == carta);
            int maximo = carta.StartsWith("cero") ? 1 : 2;
            return veces >= maximo;
        }

        private void Reciclar()
        {
            usados.Clear();
            usados.AddRange(reservadas);
            foreach (var mano in manos)
                foreach (Label l in mano)
                    usados.Add(l.Tag.ToString());
            if (cartaMesa.Tag != null)
                usados.Add(cartaMesa.Tag.ToString());
        }

        private void DarCartas(int jugador, int cantidad)
        {
            for (int i = 0; i < cantidad; i++)
                CrearCarta(CartaAlAzar(usados), jugador);
        }

        private async Task Bloquear(Func<Task> accion)
        {
            if (!puedeJugar)
                return;
            puedeJugar = false;
            int id = partida;
            try
            {
                await accion();
            }
            finally
            {
                if (id == partida && !torneoTerminado && !cerrado)
                    puedeJugar = true;
            }
        }

        private async Task JugarCartaAsync(Label carta, int jugador)
        {
            if (jugador != turno)
            {
                MessageBox.Show(this, "¡No es tu turno!");
                return;
            }
            string nombre = carta.Tag.ToString();
            if (!EsJugadaValida(nombre, cartaMesa.Tag.ToString()))
            {
                MessageBox.Show(this, "Esta carta no coincide en color ni en número.");
                return;
            }
            await BajarCartaAsync(carta, jugador, nombre);
        }

        private async Task RobarLocalAsync()
        {
            string nombreCartaNueva = CartaAlAzar(usados);
            string nombreCartaMesa = cartaMesa.Tag.ToString();
            Label cartaCreada = CrearCarta(nombreCartaNueva, turno);
            string visible = nombreCartaNueva.Replace(".png", "");

            if (EsJugadaValida(nombreCartaNueva, nombreCartaMesa))
            {
                DialogResult respuesta = MessageBox.Show(this,
                    "¡Robaste una carta jugable (" + visible + ")!\n\n¿Quieres bajarla a la mesa?\n(Sí = jugarla, No = quedártela y pasar)",
                    "Carta jugable", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (respuesta == DialogResult.Yes)
                {
                    await BajarCartaAsync(cartaCreada, turno, nombreCartaNueva);
                    return;
                }
            }
            else
            {
                MessageBox.Show(this, "Robaste " + visible + ", pero no se puede jugar. Se pasa el turno.", "UNO");
            }
            SiguienteTurno();
        }

        private async Task BajarCartaAsync(Label carta, int jugador, string nombre)
        {
            Image imagen = carta.Image;
            if (jugador == 1)
                imagen.RotateFlip(RotateFlipType.Rotate90FlipNone);
            else if (jugador == 3)
                imagen.RotateFlip(RotateFlipType.Rotate270FlipNone);
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = imagen;
            cartaMesa.Tag = nombre;
            carta.Image = null;
            manos[jugador].Remove(carta);
            Controls.Remove(carta);
            carta.Dispose();
            Reorganizar(jugador);
            ActualizarNombres();

            if (manos[jugador].Count == 1)
            {
                DialogResult grito = MessageBox.Show(this, "¡Al " + nombres[jugador] + " le queda una sola carta!\n\n¿Presionaste el botón a tiempo para gritar ¡UNO!?",
                    "Regla del UNO", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (grito == DialogResult.No)
                {
                    MessageBox.Show(this, "¡No gritaste ¡UNO! a tiempo! Penalización: Recibes 2 cartas de castigo del mazo.", "Penalización", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DarCartas(jugador, 2);
                }
                else
                {
                    MessageBox.Show(this, "¡Grito válido! El " + nombres[jugador] + " ha cantado ¡UNO! con éxito.", "UNO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            if (manos[jugador].Count == 0)
            {
                if (RegistrarPuntos(jugador))
                    await IniciarJuego();
                else
                    torneoTerminado = true;
                return;
            }

            colorActivo = EsComodin(nombre) ? (ElegirColor() ?? "rojo") : ColorDe(nombre);
            int siguiente = Siguiente(turno);
            switch (TipoDe(nombre))
            {
                case "bloqueo":
                    MessageBox.Show(this, "¡Carta de Bloqueo! " + nombres[siguiente] + " pierde su turno.");
                    turno = siguiente;
                    break;
                case "mas_dos":
                    MessageBox.Show(this, "¡Carta +2! " + nombres[siguiente] + " recibe 2 cartas y pierde su turno.");
                    DarCartas(siguiente, 2);
                    turno = siguiente;
                    break;
                case "reverse":
                    direccion = -direccion;
                    MessageBox.Show(this, "¡Carta Reversa! Se ha cambiado la dirección del juego.");
                    break;
                case "cambiar_color":
                    MessageBox.Show(this, "El nuevo color en la mesa es: " + colorActivo.ToUpper(), "UNO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case "mas_cuatro":
                    MessageBox.Show(this, "¡Comodín +4! " + nombres[siguiente] + " recibe 4 cartas, pierde su turno y el color cambia a " + colorActivo.ToUpper(),
                        "UNO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DarCartas(siguiente, 4);
                    turno = siguiente;
                    break;
            }
            SiguienteTurno();
        }

        private int Siguiente(int jugador)
        {
            return (jugador + direccion + 4) % 4;
        }

        private void SiguienteTurno()
        {
            turno = Siguiente(turno);
            lblInfo.Text = "Turno: " + nombres[turno] + "\nColor: " + colorActivo
                + " | Dirección: " + (direccion == 1 ? "horaria" : "antihoraria");
            Resaltar(turno);
            RefrescarColores();
        }

        private string ColorDe(string nombre)
        {
            nombre = nombre.ToLower();
            return coloresUno.FirstOrDefault(c => nombre.Contains(c));
        }

        private string TipoDe(string nombre)
        {
            string[] quitar = { "rojo", "verde", "azul", "amarillo", "negro", "comodin" };
            string[] partes = Path.GetFileNameWithoutExtension(nombre).ToLower().Split('_');
            return string.Join("_", partes.Except(quitar));
        }

        private bool EsComodin(string nombre)
        {
            string tipo = TipoDe(nombre);
            return tipo == "cambiar_color" || tipo == "mas_cuatro";
        }

        private bool EsJugadaValida(string cartaMano, string cartaMesa)
        {
            if (EsComodin(cartaMano))
                return true;
            return ColorDe(cartaMano) == colorActivo || TipoDe(cartaMano) == TipoDe(cartaMesa);
        }

        private string ElegirColor()
        {
            using (var dialogo = new Form
            {
                Text = "Elige un color",
                Size = new Size(390, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                string elegido = null;
                string[] colores = { "rojo", "amarillo", "verde", "azul" };
                Color[] tonos = { Color.Red, Color.Gold, Color.LightGreen, Color.LightBlue };
                for (int i = 0; i < 4; i++)
                {
                    string color = colores[i];
                    var b = new Button { Text = color, BackColor = tonos[i], Location = new Point(10 + i * 90, 30), Size = new Size(85, 40) };
                    b.Click += (s, e) => { elegido = color; dialogo.DialogResult = DialogResult.OK; };
                    dialogo.Controls.Add(b);
                }
                return dialogo.ShowDialog(this) == DialogResult.OK ? elegido : null;
            }
        }

        private bool RegistrarPuntos(int ganador)
        {
            int puntos = CalcularPuntosRonda();
            puntajes[ganador] += puntos;
            ActualizarNombres();

            if (puntajes[ganador] >= PUNTOS_META)
            {
                MessageBox.Show(this, "¡FIN DEL TORNEO!\n\n" + nombres[ganador] + " alcanzó " + puntajes[ganador]
                    + " puntos y ganó todo el juego ", "¡Ganador absoluto!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Array.Clear(puntajes, 0, puntajes.Length);
                ActualizarNombres();
                Resaltar(-1);
                lblInfo.Text = "Torneo terminado. Ganador: " + nombres[ganador].ToUpper() + "\nPulsa \"Nueva partida\" para empezar otro.";
                return false;
            }

            MessageBox.Show(this, nombres[ganador] + " ganó la ronda.\nAcumula +" + puntos + " puntos.\n\nPreparando la siguiente ronda...",
                "Fin de la ronda", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private IEnumerable<string> NombresEnMano()
        {
            if (USAR_API)
                return estado.cartas.Where(c => c.zona == "mano").Select(c => c.ArchivoImagen());
            return manos.SelectMany(m => m).Select(l => l.Tag.ToString());
        }

        private int CalcularPuntosRonda()
        {
            int total = 0;
            foreach (string carta in NombresEnMano())
            {
                string nombre = carta.ToLower();
                if (nombre.Contains("mas_cuatro") || nombre.Contains("cambiar_color"))
                    total += 50;
                else if (nombre.Contains("bloqueo") || nombre.Contains("reverse") || nombre.Contains("mas_dos"))
                    total += 20;
                else
                    total += ValorCarta(nombre);
            }
            return total;
        }

        private int ValorCarta(string nombre)
        {
            string primero = Path.GetFileName(nombre).Split('_')[0].ToLower();
            int v;
            if (int.TryParse(primero, out v))
                return v;
            switch (primero)
            {
                case "cero":
                    return 0;
                case "uno":
                    return 1;
                case "dos":
                    return 2;
                case "tres":
                    return 3;
                case "cuatro":
                    return 4;
                case "cinco":
                    return 5;
                case "seis":
                    return 6;
                case "siete":
                    return 7;
                case "ocho":
                    return 8;
                case "nueve":
                    return 9;
                default:
                    return 0;
            }
        }

        private void ActualizarNombres()
        {
            if (cerrado)
                return;
            for (int i = 0; i < 4; i++)
                lblNombres[i].Text = nombres[i] + " (" + manos[i].Count + ")\n" + puntajes[i] + " pts";
        }

        private void Resaltar(int jugador)
        {
            for (int i = 0; i < 4; i++)
            {
                bool activo = i == jugador;
                lblNombres[i].BackColor = activo ? Color.Gold : Color.FromArgb(170, 0, 0, 0);
                lblNombres[i].ForeColor = activo ? Color.Black : Color.White;
            }
        }

        private Label CrearEtiqueta(object tag, string archivo, int jugador, bool aColor)
        {
            Image imagen = CargarImagen(archivo, tam);
            if (!aColor)
                imagen = ConvertirBlancoNegro(imagen);
            if (jugador == 1)
                imagen.RotateFlip(RotateFlipType.Rotate270FlipNone);
            else if (jugador == 3)
                imagen.RotateFlip(RotateFlipType.Rotate90FlipNone);

            var l = new Label
            {
                Image = imagen,
                Tag = tag,
                Name = jugador.ToString(),
                Cursor = Cursors.Hand,
                Enabled = !USAR_API
            };
            l.MouseEnter += (s, e) => Levantar(l, jugador);
            l.MouseLeave += (s, e) => Reorganizar(jugador);
            l.Click += Carta_Click;

            manos[jugador].Add(l);
            Controls.Add(l);
            Reorganizar(jugador);
            ActualizarNombres();
            return l;
        }

        private Label CrearCarta(Carta carta, int jugador, bool aColor = true)
        {
            return CrearEtiqueta(carta, carta.ArchivoImagen(), jugador, aColor);
        }

        private Label CrearCarta(string nombre, int jugador)
        {
            return CrearEtiqueta(nombre, nombre, jugador, true);
        }

        private void Reorganizar(int jugador)
        {
            int n = manos[jugador].Count;
            int paso = Math.Min(80, 650 / Math.Max(1, n - 1));
            for (int i = 0; i < n; i++)
            {
                Label l = manos[jugador][i];
                int d = i * paso;
                l.ImageAlign = ContentAlignment.TopLeft;
                switch (jugador)
                {
                    case 0:
                        l.Size = tam;
                        l.Location = new Point(350 + d, 45);
                        break;
                    case 1:
                        l.Size = tam_2;
                        l.Location = new Point(1280, 150 + d);
                        break;
                    case 2:
                        l.Size = tam;
                        l.Location = new Point(350 + d, 700);
                        break;
                    case 3:
                        l.Size = tam_2;
                        l.Location = new Point(25, 150 + d);
                        break;
                }
                l.BringToFront();
            }
        }

        private void Levantar(Label l, int jugador)
        {
            Reorganizar(jugador);
            Size cambio = levante[jugador];
            if (cambio.Width < 0 || cambio.Height < 0)
                l.Location += cambio;
            l.Size += new Size(Math.Abs(cambio.Width), Math.Abs(cambio.Height));
            l.ImageAlign = alineaLevante[jugador];
            l.BringToFront();
        }

        private void RefrescarColores()
        {
            for (int j = 0; j < 4; j++)
            {
                foreach (Label l in manos[j])
                {
                    Image nueva = CargarImagen(l.Tag.ToString(), tam);
                    if (j != turno)
                        nueva = ConvertirBlancoNegro(nueva);
                    if (j == 1)
                        nueva.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    else if (j == 3)
                        nueva.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    l.Image?.Dispose();
                    l.Image = nueva;
                }
            }
        }

        private Image ConvertirBlancoNegro(Image imagen)
        {
            Bitmap nueva = new Bitmap(imagen.Width, imagen.Height);
            using (Graphics g = Graphics.FromImage(nueva))
            using (ImageAttributes atributos = new ImageAttributes())
            {
                atributos.SetColorMatrix(new ColorMatrix(new float[][]
                {
                    new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                    new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                    new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                }));
                g.DrawImage(imagen, new Rectangle(0, 0, imagen.Width, imagen.Height),
                            0, 0, imagen.Width, imagen.Height, GraphicsUnit.Pixel, atributos);
            }
            imagen.Dispose();
            return nueva;
        }

        private void LimpiarManos()
        {
            foreach (var mano in manos)
            {
                foreach (var l in mano)
                {
                    Controls.Remove(l);
                    l.Image?.Dispose();
                    l.Dispose();
                }
                mano.Clear();
            }
        }

        private bool Comprueba_usados(List<string> usados, string carta)
        {
            string cero = "cero";
            int cont = 0;
            for (int i = 0; i < usados.Count; i++)
            {
                if (carta.StartsWith(cero))
                {
                    if (usados[i] == carta)
                    {
                        return true;
                    }
                }
                else
                {
                    if (usados[i] == carta)
                    {
                        cont++;
                    }
                }
            }
            if (cont >= 2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        private void SiguienteTurno()
        {
            turno = (turno + direccion + 4) % 4;
            Resaltar(turno);
        }

        private void img_MouseEnter(object sender, EventArgs e)
        {
            Label img = (Label)sender;
            switch (img.Name)
            {
                case "0":   
                    img.Height += 30;
                    img.ImageAlign = ContentAlignment.BottomLeft;
                    break;
                case "1":  
                    img.Left -= 30;
                    img.Width += 30;
                    break;
                case "2":  
                    img.Top -= 30;
                    img.Height += 30;
                    break;
                case "3":   
                    img.Width += 30;
                    img.ImageAlign = ContentAlignment.TopRight;
                    break;
            }
        }

        private void img_MouseLeave(object sender, EventArgs e)
        {
            Label img = (Label)sender;
            switch (img.Name)
            {
                case "0":
                    img.Height -= 30;
                    img.ImageAlign = ContentAlignment.TopLeft;
                    break;
                case "1":
                    img.Left += 30;
                    img.Width -= 30;
                    break;
                case "2":
                    img.Top += 30;
                    img.Height -= 30;
                    break;
                case "3":
                    img.Width -= 30;
                    img.ImageAlign = ContentAlignment.TopLeft;
                    break;
            }
        }

  
        private void Baraja_Click(object sender, EventArgs e)
        {
            string nombreCartaNueva = CartaAlAzar(usados);
            CrearCarta(nombreCartaNueva, turno);

            SiguienteTurno();
        }
        private bool EsJugadaValida(string cartaMano, string cartaMesa)
        {
            cartaMano = cartaMano.ToLower();
            cartaMesa = cartaMesa.ToLower();

            if (cartaMano.Contains("cambiar_color") || cartaMano.Contains("mas_cuatro") || cartaMano.Contains("wild"))
            {
                return true;
            }

            string[] colores = { "rojo", "verde", "azul", "amarillo" };
            string colorMano = "";
            string colorMesa = "";

            foreach (string c in colores)
            {
                if (cartaMano.Contains(c)) colorMano = c;
                if (cartaMesa.Contains(c)) colorMesa = c;
            }

            if (!string.IsNullOrEmpty(colorMano) && colorMano == colorMesa)
            {
                return true;
            }
           
            string manoLimpia = cartaMano.Replace(".png", "");
            string mesaLimpia = cartaMesa.Replace(".png", "");

            string[] partesMano = manoLimpia.Split('_');
            string[] partesMesa = mesaLimpia.Split('_');

            foreach (string parteM in partesMano)
            {
                if (parteM == "rojo" || parteM == "verde" || parteM == "azul" || parteM == "amarillo") continue;

                foreach (string parteMe in partesMesa)
                {
                    if (parteM == parteMe)
                    {
                        return true;
                    }
                }
            }
            

            return false;
        }


        private void Carta_Click(object sender, EventArgs e)
        {
            Label cartaClickeada = (Label)sender;
            int jugadorCarta = int.Parse(cartaClickeada.Name);

            if (jugadorCarta != turno)
            {
                MessageBox.Show("¡No es tu turno!");
                return;
            }

            string nombreCartaMano = cartaClickeada.Tag.ToString();
            string nombreCartaMesa = cartaMesa.Tag.ToString();

            if (EsJugadaValida(nombreCartaMano, nombreCartaMesa))
            {
                Image imagenFinal = cartaClickeada.Image;

                if (jugadorCarta == 1)
                {
                    imagenFinal.RotateFlip(RotateFlipType.Rotate90FlipNone);
                }
                else if (jugadorCarta == 3)
                {
                    imagenFinal.RotateFlip(RotateFlipType.Rotate270FlipNone);
                }

                cartaMesa.Image = imagenFinal;
                cartaMesa.Tag = nombreCartaMano;

                manos[turno].Remove(cartaClickeada);
                this.Controls.Remove(cartaClickeada);
                cartaClickeada.Dispose();

                if (nombreCartaMano.Contains("bloqueo") || nombreCartaMano.Contains("skip"))
                {
                    MessageBox.Show("¡Carta de Bloqueo! Se salta el turno del siguiente jugador.");
                    SiguienteTurno();
                }

               
                else if (nombreCartaMano.Contains("mas_dos") || nombreCartaMano.Contains("mas2") || nombreCartaMano.Contains("draw2"))
                {
                    int siguienteJugador = (turno + direccion + 4) % 4;

                    MessageBox.Show("¡Carta +2! El Jugador " + (siguienteJugador + 1) + " recibe 2 cartas y pierde su turno.");

                    CrearCarta(CartaAlAzar(usados), siguienteJugador);
                    CrearCarta(CartaAlAzar(usados), siguienteJugador);

                    SiguienteTurno();
                }
              
                else if (nombreCartaMano.Contains("reversa") || nombreCartaMano.Contains("reverse"))
                {
                    direccion = direccion * -1; 
                    MessageBox.Show("¡Carta Reversa! Se ha cambiado la dirección del juego.");
        }

                SiguienteTurno();
            }
            else
            {
                MessageBox.Show("Esta carta no coincide en color ni en número.");
            }
        }
    }
}