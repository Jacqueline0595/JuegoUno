using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InterfazUno
{
    public partial class Form2 : Form
    {
        const int PUNTOS_META = 200;
        const int PUERTO_SERVIDOR = 8001;

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
        // Posición de la etiqueta de nombre de cada asiento en pantalla: 0 = arriba, 1 = derecha, 2 = abajo, 3 = izquierda
        readonly Point[] puntosNombres = { new Point(610, 195), new Point(1295, 100), new Point(610, 855), new Point(45, 100) };

        readonly Label[] lblNombres = new Label[4];
        readonly List<Label>[] manos = Enumerable.Range(0, 4).Select(i => new List<Label>()).ToArray();
        readonly int[] puntajes = new int[4];
        readonly Random ran = new Random();

        Label lblBaraja, cartaMesa, lblInfo;
        Button btnReinicio, btnUno, btnConectar;

        List<string> usados;
        string[] archivos;

        string colorActivo;
        int partida, repartidor, turno, direccion = 1;
        bool puedeJugar, torneoTerminado, cerrado;

        string ruta_d = Path.Combine(Application.StartupPath, "Cartas") + "\\";

        
        // VARIABLES DE ARQUITECTURA CLIENTE-SERVIDOR (SOCKETS TCP)
        
        Socket socketCliente;
        Thread hiloEscucha;
        bool modoRed = true;
        int miJugador = -1; // 0 = Jugador 1, 1 = Jugador 2, 2 = Jugador 3, 3 = Jugador 4
        int jugadoresConectados = 0;
        bool preGritoUno = false;

        public Form2()
        {
            InitializeComponent();

            Icon = new Icon(Path.Combine(Application.StartupPath, "Recursos", "logoUno.ico"));
            FormClosed += (s, e) =>
            {
                cerrado = true;
                partida++;
                CerrarSocket();
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
                Text = "UNO - Cliente en Red";
                WindowState = FormWindowState.Maximized;
                AutoScroll = true;
                AutoScrollMinSize = new Size(1450, 950);
                BackgroundImage = CargarImagen("juego_fondo.jpg", new Size(1920, 1080), "Recursos");
                BackgroundImageLayout = ImageLayout.Stretch;

                archivos = Directory.GetFiles(Path.Combine(Application.StartupPath, "Cartas"))
                    .Select(f => Path.GetFileName(f))
                    .Where(f => f != "carta_vacia.png" && f != "carta_uno.png")
                    .ToArray();

                CrearTablero();

                // Solicitar IP del Servidor al iniciar
                if (ConfigurarYConectarSocket())
                {
                    lblInfo.Text = "Conectado al servidor. Esperando asignación de jugador e inicio de partida...";
                }
                else
                {
                    // Si el usuario eligió probar en Modo Local (1 sola PC)
                    modoRed = false;
                    Text = "UNO - Modo Local (1 PC)";
                    await ReiniciarAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "No se pudo abrir el tablero");
                Close();
            }
        }

        
        // CONFIGURACIÓN Y CONEXIÓN DEL CLIENTE DE SOCKETS TCP
        
        private bool ConfigurarYConectarSocket()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Conexión al Servidor UNO (TCP Puerto 8001)";
                dlg.Size = new Size(420, 230);
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                Label lblIp = new Label
                {
                    Text = "Dirección IP del Servidor (LAN):",
                    Location = new Point(25, 25),
                    Size = new Size(350, 25),
                    Font = new Font("Arial", 11, FontStyle.Bold)
                };

                TextBox txtIp = new TextBox
                {
                    Text = "127.0.0.1",
                    Location = new Point(25, 55),
                    Size = new Size(350, 30),
                    Font = new Font("Arial", 12)
                };

                Button btnOk = new Button
                {
                    Text = "Conectar en Red",
                    Location = new Point(25, 110),
                    Size = new Size(170, 45),
                    BackColor = Color.FromArgb(67, 160, 71),
                    ForeColor = Color.White,
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };

                Button btnLocal = new Button
                {
                    Text = "Modo Local (1 PC)",
                    Location = new Point(205, 110),
                    Size = new Size(170, 45),
                    BackColor = Color.FromArgb(30, 136, 229),
                    ForeColor = Color.White,
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };

                bool conectado = false;

                btnOk.Click += (s, e) =>
                {
                    try
                    {
                        CerrarSocket();
                        IPAddress ip = IPAddress.Parse(txtIp.Text.Trim());
                        IPEndPoint endPoint = new IPEndPoint(ip, PUERTO_SERVIDOR);

                        // Creación del Socket TCP orientado a conexión (Stream)
                        socketCliente = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socketCliente.Connect(endPoint);

                        modoRed = true;
                        conectado = true;

                        // Iniciamos el hilo receptor en segundo plano (IsBackground = true)
                        hiloEscucha = new Thread(EscucharServidor)
                        {
                            IsBackground = true
                        };
                        hiloEscucha.Start();

                        dlg.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(dlg,
                            "No se pudo conectar al servidor en " + txtIp.Text + ":" + PUERTO_SERVIDOR +
                            "\n\nDetalle: " + ex.Message +
                            "\n\nVerifica que el Servidor esté corriendo y el Firewall permita el puerto 8001.",
                            "Error de Conexión TCP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                btnLocal.Click += (s, e) =>
                {
                    modoRed = false;
                    conectado = false;
                    dlg.DialogResult = DialogResult.Cancel;
                };

                dlg.Controls.AddRange(new Control[] { lblIp, txtIp, btnOk, btnLocal });
                dlg.ShowDialog(this);
                return conectado;
            }
        }

        // Serializa un comando de texto plano a bytes UTF-8 con delimitador '\n' y lo envía al servidor
        private void EnviarTramaServidor(string trama)
        {
            if (!modoRed || socketCliente == null || !socketCliente.Connected)
                return;

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(trama + "\n");
                socketCliente.Send(buffer);
            }
            catch (Exception ex)
            {
                EjecutarEnUI(() =>
                {
                    lblInfo.Text = "Error al enviar datos al servidor: " + ex.Message;
                });
            }
        }

        
        // RECEPCIÓN MULTIHILO Y SEGURIDAD DE HILOS (INVOKE / BEGININVOKE)
        private void EscucharServidor()
        {
            StringBuilder acumulador = new StringBuilder();
            byte[] buffer = new byte[4096];

            try
            {
                while (!cerrado && socketCliente != null && socketCliente.Connected)
                {
                    int bytesRecibidos = socketCliente.Receive(buffer);
                    if (bytesRecibidos == 0)
                        break;

                    acumulador.Append(Encoding.UTF8.GetString(buffer, 0, bytesRecibidos));
                    string datos = acumulador.ToString();

                    int posSalto;
                    while ((posSalto = datos.IndexOf('\n')) >= 0)
                    {
                        string trama = datos.Substring(0, posSalto).Trim();
                        datos = datos.Substring(posSalto + 1);

                        if (!string.IsNullOrEmpty(trama))
                        {
                            string copiaTrama = trama;
                            
                            EjecutarEnUI(() => ProcesarTramaRed(copiaTrama));
                        }
                    }

                    acumulador.Clear();
                    acumulador.Append(datos);
                }
            }
            catch
            {
                if (!cerrado)
                {
                    EjecutarEnUI(() =>
                    {
                        lblInfo.Text = "Desconectado del servidor. Usa el botón 'IP Servidor' para reconectar.";
                    });
                }
            }
        }

        
        private void EjecutarEnUI(Action accion)
        {
            if (cerrado || IsDisposed)
                return;

            if (InvokeRequired)
                BeginInvoke(accion);
            else
                accion();
        }

        private void CerrarSocket()
        {
            try
            {
                if (socketCliente != null)
                {
                    if (socketCliente.Connected)
                        socketCliente.Shutdown(SocketShutdown.Both);
                    socketCliente.Close();
                    socketCliente = null;
                }
            }
            catch { }
        }

        
        // PROCESAMIENTO DEL PROTOCOLO DE SINCRONIZACIÓN EN TIEMPO REAL
        
        private async void ProcesarTramaRed(string trama)
        {
            if (cerrado)
                return;

            string[] partes = trama.Split('|');
            string comando = partes[0];

            switch (comando)
            {
                case "ASIGNAR":
                    // Formato: ASIGNAR|idJugador
                    miJugador = int.Parse(partes[1]);
                    Text = $"UNO en Red — Soy {nombres[miJugador]}";
                    AplicarPerspectiva();
                    ActualizarNombres();
                    lblInfo.Text = $"Conectado como {nombres[miJugador]}.\nPulsa 'Nueva partida' cuando estén listos.";
                    break;

                case "CONECTADOS":
                    // Formato: CONECTADOS|cantidad
                    jugadoresConectados = int.Parse(partes[1]);
                    if (cartaMesa.Tag == null)
                    {
                        lblInfo.Text = $"Soy {(miJugador >= 0 ? nombres[miJugador] : "Espectador")} | " +
                                       $"Jugadores conectados: {jugadoresConectados}/4.\nPulsa 'Nueva partida' para repartir.";
                    }
                    break;

                case "ERROR":
                    MessageBox.Show(this, partes[1], "Aviso del Servidor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;

                case "INICIAR":
                    // Formato: INICIAR|repartidor|cartaCentro|colorInicial|mano0;mano1;mano2;mano3|cartasExtraMasDos
                    AplicarInicioRed(
                        int.Parse(partes[1]),
                        partes[2],
                        partes[3],
                        partes[4],
                        partes.Length > 5 ? partes[5] : ""
                    );
                    break;

                case "JUGAR":
                    
                    await AplicarJugadaRedAsync(
                        int.Parse(partes[1]),
                        int.Parse(partes[2]),
                        partes[3],
                        partes[4],
                        partes[5],
                        partes.Length > 6 ? partes[6] : "",
                        partes.Length > 7 ? partes[7] : ""
                    );
                    break;

                case "ROBAR":
                    
                    AplicarRoboRed(int.Parse(partes[1]), partes[2]);
                    break;

                case "PASAR":
                    
                    SiguienteTurno();
                    break;

                case "UNO":
                    
                    int jUno = int.Parse(partes[1]);
                    lblInfo.Text = $"¡{nombres[jUno]} ha gritado ¡UNO! a tiempo!\n" + lblInfo.Text;
                    break;
            }
        }

        // Construye y envía el estado inicial de la ronda al servidor para los 4 clientes
        private void SolicitarInicioPartidaRed()
        {
            List<string> mazoSimulado = new List<string>(reservadas);
            int rep = ran.Next(0, 4);

            List<string>[] manosIniciales = Enumerable.Range(0, 4).Select(x => new List<string>()).ToArray();
            int jug = (rep + 1) % 4;
            for (int i = 0; i < 28; i++)
            {
                manosIniciales[jug].Add(CartaAlAzar(mazoSimulado));
                jug = (jug + 1) % 4;
            }

            string centro = CartaAlAzar(mazoSimulado);
            int intentos = 0;
            while (TipoDe(centro) == "mas_cuatro" && intentos++ < 50)
            {
                mazoSimulado.Remove(centro);
                centro = CartaAlAzar(mazoSimulado);
            }
            if (TipoDe(centro) == "mas_cuatro")
            {
                mazoSimulado.Remove(centro);
                centro = archivos.First(a => TipoDe(a) != "mas_cuatro" && !Comprueba_usados(mazoSimulado, a));
                mazoSimulado.Add(centro);
            }

            string colorIni = ColorDe(centro);
            if (TipoDe(centro) == "cambiar_color")
            {
                colorIni = ElegirColor() ?? "rojo";
            }

            // Si la primera carta del centro es un +2, pre-generamos las 2 cartas para que las 4 PCs coincidan
            string extraMasDos = "";
            if (TipoDe(centro) == "mas_dos")
            {
                extraMasDos = CartaAlAzar(mazoSimulado) + "," + CartaAlAzar(mazoSimulado);
            }

            string manosSerializadas = string.Join(";", manosIniciales.Select(m => string.Join(",", m)));
            EnviarTramaServidor($"INICIAR|{rep}|{centro}|{colorIni}|{manosSerializadas}|{extraMasDos}");
        }

        // Sincroniza el tablero en las 4 computadoras al recibir INICIAR
        private void AplicarInicioRed(int rep, string centro, string colorIni, string manosStr, string extraMasDos)
        {
            partida++;
            torneoTerminado = false;
            preGritoUno = false;
            repartidor = rep;
            usados = new List<string>(reservadas);

            LimpiarManos();

            string[] gruposManos = manosStr.Split(';');
            for (int j = 0; j < 4; j++)
            {
                string[] cartasJugador = gruposManos[j].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string c in cartasJugador)
                {
                    usados.Add(c);
                    CrearCarta(c, j);
                }
            }

            usados.Add(centro);
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = CargarImagen(centro, tam);
            cartaMesa.Tag = centro;
            colorActivo = colorIni;

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
                    foreach (string cExtra in extraMasDos.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        usados.Add(cExtra);
                        CrearCarta(cExtra, turno);
                    }
                    turno = Siguiente(turno);
                    break;
                case "cambiar_color":
                    msg += ". Color inicial: " + colorActivo;
                    break;
            }

            puedeJugar = true;
            Resaltar(turno);
            RefrescarColores();
            ActualizarNombres();
            lblInfo.Text = msg + ". Empieza " + nombres[turno] +
                           (turno == miJugador ? " (¡ES TU TURNO!)" : "");
        }

        // Aplica en tiempo real la jugada de cualquier jugador en las 4 pantallas
        private async Task AplicarJugadaRedAsync(
            int jugador, int indiceCarta, string nombre, string nuevoColor,
            string gritoUno, string castigoUno, string cartasEfecto)
        {
            if (manos[jugador].Count == 0)
                return;

            
            Label carta = (indiceCarta >= 0 && indiceCarta < manos[jugador].Count)
                ? manos[jugador][indiceCarta]
                : manos[jugador].FirstOrDefault(l => l.Tag.ToString() == nombre) ?? manos[jugador][0];

            // Cargamos siempre la cara real de la carta al caer en el centro de la mesa
            Image imagenMesa = CargarImagen(nombre, tam);
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = imagenMesa;
            cartaMesa.Tag = nombre;

            carta.Image?.Dispose();
            carta.Image = null;
            manos[jugador].Remove(carta);
            Controls.Remove(carta);
            carta.Dispose();

            Reorganizar(jugador);
            ActualizarNombres();

            string avisoExtra = "";

            // Verificación de regla "¡UNO!" cuando queda 1 sola carta
            if (manos[jugador].Count == 1)
            {
                if (gritoUno == "NO")
                {
                    avisoExtra = $"¡{nombres[jugador]} no gritó UNO! Recibe +2 cartas de castigo. ";
                    foreach (string cCastigo in castigoUno.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        usados.Add(cCastigo);
                        CrearCarta(cCastigo, jugador);
                    }
                }
                else
                {
                    avisoExtra = $"¡{nombres[jugador]} cantó ¡UNO! con éxito! ";
                }
            }

            
            if (manos[jugador].Count == 0)
            {
                bool continuarTorneo = RegistrarPuntos(jugador);
                if (continuarTorneo)
                {
                    
                    if (jugador == miJugador)
                    {
                        await Task.Delay(1200);
                        SolicitarInicioPartidaRed();
                    }
                }
                else
                {
                    torneoTerminado = true;
                }
                return;
            }

            colorActivo = nuevoColor;
            int siguiente = Siguiente(turno);
            string[] listaEfecto = cartasEfecto.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            switch (TipoDe(nombre))
            {
                case "bloqueo":
                    avisoExtra += $"¡Bloqueo! {nombres[siguiente]} pierde turno.";
                    turno = siguiente;
                    break;
                case "mas_dos":
                    avisoExtra += $"¡Carta +2! {nombres[siguiente]} recibe 2 cartas y pierde turno.";
                    foreach (string ce in listaEfecto)
                    {
                        usados.Add(ce);
                        CrearCarta(ce, siguiente);
                    }
                    turno = siguiente;
                    break;
                case "reverse":
                    direccion = -direccion;
                    avisoExtra += "¡Reversa! Cambia el sentido del giro.";
                    break;
                case "cambiar_color":
                    avisoExtra += $"Cambio de color a {colorActivo.ToUpper()}.";
                    break;
                case "mas_cuatro":
                    avisoExtra += $"¡Comodín +4! {nombres[siguiente]} recibe 4 cartas y el color es {colorActivo.ToUpper()}.";
                    foreach (string ce in listaEfecto)
                    {
                        usados.Add(ce);
                        CrearCarta(ce, siguiente);
                    }
                    turno = siguiente;
                    break;
            }

            SiguienteTurno(avisoExtra);
        }

        
        private void AplicarRoboRed(int jugador, string nombreCartaNueva)
        {
            usados.Add(nombreCartaNueva);
            Label cartaCreada = CrearCarta(nombreCartaNueva, jugador);
            int indiceCreada = manos[jugador].IndexOf(cartaCreada);

            if (jugador != miJugador)
            {
                lblInfo.Text = $"{nombres[jugador]} robó una carta del mazo...";
                return;
            }

            
            string nombreCartaMesa = cartaMesa.Tag.ToString();
            string visible = nombreCartaNueva.Replace(".png", "");

            if (EsJugadaValida(nombreCartaNueva, nombreCartaMesa))
            {
                DialogResult respuesta = MessageBox.Show(this,
                    "¡Robaste una carta jugable (" + visible + ")!\n\n¿Quieres bajarla a la mesa?\n(Sí = jugarla, No = quedártela y pasar)",
                    "Carta jugable", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (respuesta == DialogResult.Yes)
                {
                    EnviarJugadaAlServidor(indiceCreada, nombreCartaNueva);
                    return;
                }
            }
            else
            {
                MessageBox.Show(this, "Robaste " + visible + ", pero no se puede jugar. Se pasa el turno.", "UNO");
            }

            EnviarTramaServidor($"PASAR|{miJugador}");
        }

        
        private void EnviarJugadaAlServidor(int indiceCarta, string nombre)
        {
            string gritoUno = "SI";
            string castigoUno = "";

            // Si al tirar esta carta me voy a quedar con 1 sola carta
            if (manos[miJugador].Count == 2)
            {
                if (!preGritoUno)
                {
                    DialogResult grito = MessageBox.Show(this,
                        "¡Te queda una sola carta!\n\n¿Presionaste a tiempo para gritar ¡UNO!?",
                        "Regla del UNO", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                    if (grito == DialogResult.No)
                    {
                        gritoUno = "NO";
                        List<string> copiaUsados = new List<string>(usados);
                        castigoUno = CartaAlAzar(copiaUsados) + "," + CartaAlAzar(copiaUsados);
                    }
                }
                preGritoUno = false;
            }

            string nuevoColor = EsComodin(nombre) ? (ElegirColor() ?? "rojo") : ColorDe(nombre);

            string cartasEfecto = "";
            string tipo = TipoDe(nombre);
            if (tipo == "mas_dos" || tipo == "mas_cuatro")
            {
                int cant = tipo == "mas_dos" ? 2 : 4;
                List<string> copiaUsados = new List<string>(usados);
                List<string> generadas = new List<string>();
                for (int i = 0; i < cant; i++)
                    generadas.Add(CartaAlAzar(copiaUsados));
                cartasEfecto = string.Join(",", generadas);
            }

            EnviarTramaServidor($"JUGAR|{miJugador}|{indiceCarta}|{nombre}|{nuevoColor}|{gritoUno}|{castigoUno}|{cartasEfecto}");
        }

        
        // INTERFAZ GRAFICA, CONTROLES Y LOGICA DEL JUEGO
        
        private Image CargarImagen(string archivo, Size size, string carpeta = "Cartas")
        {
            string ruta = Path.Combine(Application.StartupPath, carpeta, archivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException("No se encontró la imagen: " + ruta);
            using (Image original = Image.FromFile(ruta)) return new Bitmap(original, size);
        }

        private Button Boton(string texto, int x, EventHandler accion, Color? colorFondo = null)
        {
            var b = new Button
            {
                Text = texto,
                Location = new Point(x, 10),
                Size = new Size(135, 36),
                BackColor = colorFondo ?? Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.Click += accion;
            Controls.Add(b);
            return b;
        }

        private void CrearTablero()
        {
            for (int i = 0; i < 4; i++)
            {
                lblNombres[i] = new Label
                {
                    Location = puntosNombres[Asiento(i)],
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

            lblBaraja = new Label
            {
                Location = new Point(580, 370),
                Size = tam,
                Image = CargarImagen("carta_uno.png", tam),
                Cursor = Cursors.Hand
            };
            lblBaraja.Click += Baraja_Click;
            Controls.Add(lblBaraja);

            cartaMesa = new Label { Location = new Point(700, 370), Size = tam };
            Controls.Add(cartaMesa);

            btnReinicio = Boton("Nueva partida", 10, async (s, e) => await ReiniciarAsync());
            btnUno = Boton("¡Gritar UNO!", 155, BtnUno_Click, Color.FromArgb(251, 140, 0));
            btnConectar = Boton("IP Servidor", 300, (s, e) => ConfigurarYConectarSocket(), Color.FromArgb(30, 136, 229));
        }

        private void BtnUno_Click(object sender, EventArgs e)
        {
            if (modoRed)
            {
                if (miJugador >= 0 && manos[miJugador].Count <= 2)
                {
                    preGritoUno = true;
                    EnviarTramaServidor($"UNO|{miJugador}");
                }
                else
                {
                    MessageBox.Show(this, "Solo puedes cantar ¡UNO! cuando estás por quedarte con 1 carta.", "UNO");
                }
            }
            else
            {
                preGritoUno = true;
                lblInfo.Text = $"¡{nombres[turno]} ha activado el grito de ¡UNO!!";
            }
        }

        private async Task ReiniciarAsync()
        {
            Array.Clear(puntajes, 0, puntajes.Length);

            if (modoRed)
            {
                if (socketCliente == null || !socketCliente.Connected)
                {
                    MessageBox.Show(this, "Primero conéctate al servidor con el botón 'IP Servidor'.", "Sin conexión");
                    return;
                }
                SolicitarInicioPartidaRed();
                return;
            }

            await IniciarJuego();
        }

        private async void Baraja_Click(object sender, EventArgs e)
        {
            if (modoRed)
            {
                if (!puedeJugar || cartaMesa.Tag == null)
                    return;

                if (turno != miJugador)
                {
                    MessageBox.Show(this, $"¡No es tu turno! Es el turno de {nombres[turno]}.", "Espera tu turno");
                    return;
                }

                List<string> copiaUsados = new List<string>(usados);
                string robada = CartaAlAzar(copiaUsados);
                EnviarTramaServidor($"ROBAR|{miJugador}|{robada}");
            }
            else
            {
                await Bloquear(RobarLocalAsync);
            }
        }

        private async void Carta_Click(object sender, EventArgs e)
        {
            Label carta = (Label)sender;
            int jugador = int.Parse(carta.Name);

            if (modoRed)
            {
                if (!puedeJugar || cartaMesa.Tag == null)
                    return;

                if (jugador != miJugador)
                {
                    MessageBox.Show(this, "No puedes jugar con las cartas de otro jugador.", "UNO en Red");
                    return;
                }

                if (turno != miJugador)
                {
                    MessageBox.Show(this, $"¡No es tu turno! Es el turno de {nombres[turno]}.", "Espera tu turno");
                    return;
                }

                string nombre = carta.Tag.ToString();
                if (!EsJugadaValida(nombre, cartaMesa.Tag.ToString()))
                {
                    MessageBox.Show(this, "Esta carta no coincide con el color activo (" + colorActivo + ") ni con el símbolo en mesa.");
                    return;
                }

                int indice = manos[miJugador].IndexOf(carta);
                EnviarJugadaAlServidor(indice, nombre);
            }
            else
            {
                await Bloquear(() => JugarCartaAsync(carta, jugador));
            }
        }

        
        // MODO LOCAL DE RESPALDO Y FUNCIONES AUXILIARES DEL TABLERO
        
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
            int intentos = 0;
            while (TipoDe(centro) == "mas_cuatro" && intentos++ < 50)
            {
                usados.Remove(centro);
                centro = CartaAlAzar(usados);
            }
            if (TipoDe(centro) == "mas_cuatro")
            {
                usados.Remove(centro);
                centro = archivos.First(a => TipoDe(a) != "mas_cuatro" && Comprueba_usados(usados, a) == false);
                usados.Add(centro);
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
                    msg += ". " + nombres[turno] + " elige el color inicial";
                    lblInfo.Text = msg;
                    string colorElegido = ElegirColor();
                    if (colorElegido == null)
                        return;
                    colorActivo = colorElegido;
                    msg = "Repartió " + nombres[repartidor] + ". Color inicial: " + colorActivo;
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
            else if (disponibles.Count == 0)
            {
                disponibles = archivos.ToList();
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
            string tipo = TipoDe(carta);
            int maximo;
            if (tipo == "cambiar_color" || tipo == "mas_cuatro")
                maximo = 4;
            else if (tipo == "cero")
                maximo = 1;
            else
                maximo = 2;

            return lista.Count(u => u == carta) >= maximo;
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
            Image imagen = CargarImagen(nombre, tam);
            cartaMesa.Image?.Dispose();
            cartaMesa.Image = imagen;
            cartaMesa.Tag = nombre;
            carta.Image?.Dispose();
            carta.Image = null;
            manos[jugador].Remove(carta);
            Controls.Remove(carta);
            carta.Dispose();
            Reorganizar(jugador);
            ActualizarNombres();

            if (manos[jugador].Count == 1)
            {
                DialogResult grito = preGritoUno ? DialogResult.Yes : MessageBox.Show(this,
                    "¡Al " + nombres[jugador] + " le queda una sola carta!\n\n¿Presionaste el botón a tiempo para gritar ¡UNO!?",
                    "Regla del UNO", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                preGritoUno = false;

                if (grito == DialogResult.No)
                {
                    MessageBox.Show(this, "¡No gritaste ¡UNO! a tiempo! Penalización: Recibes 2 cartas de castigo del mazo.",
                        "Penalización", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DarCartas(jugador, 2);
                }
                else
                {
                    MessageBox.Show(this, "¡Grito válido! El " + nombres[jugador] + " ha cantado ¡UNO! con éxito.",
                        "UNO", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, "El nuevo color en la mesa es: " + colorActivo.ToUpper(),
                        "UNO", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void SiguienteTurno(string prefijoInfo = "")
        {
            turno = Siguiente(turno);
            string lineaExtra = string.IsNullOrEmpty(prefijoInfo) ? "" : prefijoInfo + "\n";
            string indicadorPropio = (modoRed && turno == miJugador) ? " (¡TU TURNO!)" : "";

            lblInfo.Text = lineaExtra + "Turno: " + nombres[turno] + indicadorPropio +
                           "\nColor: " + colorActivo.ToUpper() +
                           " | Dirección: " + (direccion == 1 ? "horaria" : "antihoraria");
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

        private bool EsJugadaValida(string cartaMano, string cartaMesaActual)
        {
            if (EsComodin(cartaMano))
                return true;
            return ColorDe(cartaMano) == colorActivo || TipoDe(cartaMano) == TipoDe(cartaMesaActual);
        }

        private string ElegirColor()
        {
            using (var dialogo = new Form())
            {
                dialogo.AutoScaleDimensions = new SizeF(96F, 96F);
                dialogo.AutoScaleMode = AutoScaleMode.Dpi;
                dialogo.Text = "Elige un color";
                dialogo.Size = new Size(355, 260);
                dialogo.StartPosition = FormStartPosition.CenterParent;
                dialogo.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialogo.MaximizeBox = false;
                dialogo.MinimizeBox = false;
                dialogo.ShowInTaskbar = false;

                Label titulo = new Label
                {
                    Text = "Elige un color",
                    ForeColor = Color.Black,
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(20, 12),
                    Size = new Size(300, 36)
                };
                dialogo.Controls.Add(titulo);

                string elegido = null;
                string[] colores = { "rojo", "amarillo", "verde", "azul" };
                Color[] tonos =
                {
                    Color.FromArgb(229, 57, 53),
                    Color.FromArgb(251, 192, 45),
                    Color.FromArgb(67, 160, 71),
                    Color.FromArgb(30, 136, 229)
                };

                for (int i = 0; i < 4; i++)
                {
                    string color = colores[i];
                    Button boton = new Button
                    {
                        BackColor = tonos[i],
                        FlatStyle = FlatStyle.Flat,
                        Size = new Size(140, 65),
                        Location = new Point(20 + (i % 2) * 160, 60 + (i / 2) * 75),
                        Cursor = Cursors.Hand,
                        Text = ""
                    };
                    boton.FlatAppearance.BorderSize = 0;
                    boton.Click += (s, e) => { elegido = color; dialogo.DialogResult = DialogResult.OK; };
                    dialogo.Controls.Add(boton);
                }

                dialogo.FormClosing += (s, e) =>
                {
                    if (elegido == null && e.CloseReason == CloseReason.UserClosing)
                    {
                        e.Cancel = true;
                        MessageBox.Show(dialogo, "Debes elegir un color para continuar.",
                            "Color obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                dialogo.ControlBox = false;
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
                    + " puntos y ganó todo el juego.", "¡Ganador absoluto!", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                case "cero": return 0;
                case "uno": return 1;
                case "dos": return 2;
                case "tres": return 3;
                case "cuatro": return 4;
                case "cinco": return 5;
                case "seis": return 6;
                case "siete": return 7;
                case "ocho": return 8;
                case "nueve": return 9;
                default: return 0;
            }
        }

        private void ActualizarNombres()
        {
            if (cerrado)
                return;
            for (int i = 0; i < 4; i++)
            {
                string marcaYo = (modoRed && i == miJugador) ? " [TÚ]" : "";
                lblNombres[i].Text = nombres[i] + marcaYo + " (" + manos[i].Count + ")\n" + puntajes[i] + " pts";
            }
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

        // En Modo Red oculta las cartas de los oponentes mostrando 'carta_uno.png'
        private Label CrearEtiqueta(object tag, string archivoReal, int jugador, bool aColor)
        {
            bool mostrarFrente = !modoRed || jugador == miJugador;
            string archivoVisual = mostrarFrente ? archivoReal : "carta_uno.png";

            Image imagen = CargarImagen(archivoVisual, tam);
            if (!aColor)
                imagen = ConvertirBlancoNegro(imagen);
            RotarSegunAsiento(imagen, jugador);

            var l = new Label
            {
                Image = imagen,
                Tag = tag,
                Name = jugador.ToString(),
                Cursor = mostrarFrente ? Cursors.Hand : Cursors.Default
            };

            if (mostrarFrente)
            {
                l.MouseEnter += (s, e) => Levantar(l, jugador);
                l.MouseLeave += (s, e) => Reorganizar(jugador);
            }
            l.Click += Carta_Click;

            manos[jugador].Add(l);
            Controls.Add(l);
            Reorganizar(jugador);
            ActualizarNombres();
            return l;
        }

        private Label CrearCarta(string nombre, int jugador)
        {
            return CrearEtiqueta(nombre, nombre, jugador, true);
        }

        private void Reorganizar(int jugador)
        {
            int n = manos[jugador].Count;
            int paso = Math.Min(45, 650 / Math.Max(1, n - 1));
            for (int i = 0; i < n; i++)
            {
                Label l = manos[jugador][i];
                int d = i * paso;
                l.ImageAlign = ContentAlignment.TopLeft;
                switch (Asiento(jugador))
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
            int asiento = Asiento(jugador);
            Size cambio = levante[asiento];
            if (cambio.Width < 0 || cambio.Height < 0)
                l.Location += cambio;
            l.Size += new Size(Math.Abs(cambio.Width), Math.Abs(cambio.Height));
            l.ImageAlign = alineaLevante[asiento];
            l.BringToFront();
        }

        // ROTACIÓN DE PERSPECTIVA: en Modo Red cada cliente se ve a sí mismo abajo.
        // Convierte el número de jugador del servidor (0-3) en el asiento de pantalla
        // (0 = arriba, 1 = derecha, 2 = abajo, 3 = izquierda) conservando el orden de turnos.
        private int Asiento(int jugador)
        {
            if (!modoRed || miJugador < 0)
                return jugador;
            return (jugador - miJugador + 6) % 4;
        }

        private void RotarSegunAsiento(Image imagen, int jugador)
        {
            int asiento = Asiento(jugador);
            if (asiento == 1)
                imagen.RotateFlip(RotateFlipType.Rotate270FlipNone);
            else if (asiento == 3)
                imagen.RotateFlip(RotateFlipType.Rotate90FlipNone);
        }

        // Recoloca nombres y manos cuando el servidor asigna (o cambia) el número de jugador
        private void AplicarPerspectiva()
        {
            for (int i = 0; i < 4; i++)
            {
                lblNombres[i].Location = puntosNombres[Asiento(i)];
                Reorganizar(i);
            }
            RefrescarColores();
        }

        private void RefrescarColores()
        {
            for (int j = 0; j < 4; j++)
            {
                bool mostrarFrente = !modoRed || j == miJugador;
                foreach (Label l in manos[j])
                {
                    string archivoVisual = mostrarFrente ? l.Tag.ToString() : "carta_uno.png";
                    Image nueva = CargarImagen(archivoVisual, tam);
                    if (j != turno)
                        nueva = ConvertirBlancoNegro(nueva);
                    RotarSegunAsiento(nueva, j);
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
    }
}
