using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnoDatos;

namespace InterfazUno
{
    // VENTANA DEL HISTORIAL DE PARTIDAS GANADAS
    // No sabe de dónde salen los datos: Form2 le dice cómo cargarlos
    // (pedirlos al servidor en modo red o leer MySQL directamente en modo local).
    public class FormHistorial : Form
    {
        // Mismos colores que el tablero: negro translúcido de las etiquetas, dorado del turno y azul UNO
        static readonly Color NegroTranslucido = Color.FromArgb(170, 0, 0, 0);
        static readonly Color Celda = Color.FromArgb(58, 8, 6);
        static readonly Color CeldaAlterna = Color.FromArgb(78, 14, 10);
        static readonly Color Encabezado = Color.FromArgb(20, 20, 20);
        static readonly Color Lineas = Color.FromArgb(110, 25, 20);
        static readonly Color AzulUno = Color.FromArgb(30, 136, 229);
        static readonly Color AmarilloAviso = Color.FromArgb(255, 213, 79);

        readonly Func<Task<HistorialDatos>> cargar;
        readonly Func<string> origen;
        readonly Func<string> miNombre;

        readonly DataGridView gridJugadores, gridRecientes;
        readonly Label lblOrigen, lblLider, lblEstado;
        readonly Button btnActualizar;
        readonly PictureBox logo;
        readonly Font fuenteNegrita = new Font("Arial", 10.5f, FontStyle.Bold);
        Icon icono;
        bool cargando;

        public FormHistorial(Func<Task<HistorialDatos>> cargar, Func<string> origen, Func<string> miNombre)
        {
            this.cargar = cargar;
            this.origen = origen;
            this.miNombre = miNombre;

            Text = "UNO - Historial de partidas";
            ClientSize = new Size(1100, 690);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(150, 15, 10); // por si falta la imagen de fondo
            BackgroundImage = CargarImagen("juego_fondo.jpg", new Size(1920, 1080));
            BackgroundImageLayout = ImageLayout.Stretch;

            string rutaIcono = Path.Combine(Application.StartupPath, "Recursos", "logoUno.ico");
            if (File.Exists(rutaIcono))
            {
                icono = new Icon(rutaIcono);
                Icon = icono;
            }

            logo = new PictureBox
            {
                Location = new Point(30, 16),
                Size = new Size(170, 96),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = CargarImagen("uno_logo.png", new Size(384, 216))
            };

            Label titulo = new Label
            {
                Text = "HISTORIAL DE PARTIDAS",
                Location = new Point(215, 24),
                Size = new Size(860, 50),
                Font = new Font("Arial", 28, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            lblOrigen = new Label
            {
                Location = new Point(219, 78),
                Size = new Size(860, 26),
                Font = new Font("Arial", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 230, 230),
                BackColor = Color.Transparent
            };

            lblLider = new Label
            {
                Location = new Point(30, 124),
                Size = new Size(1040, 44),
                Font = new Font("Arial", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Gold,
                ForeColor = Color.Black,
                Visible = false
            };

            gridJugadores = CrearTabla();
            AgregarColumna(gridJugadores, "#", 7, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridJugadores, "Jugador", 30, DataGridViewContentAlignment.MiddleLeft);
            AgregarColumna(gridJugadores, "Ganadas", 14, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridJugadores, "Jugadas", 14, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridJugadores, "% Victorias", 15, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridJugadores, "Última victoria", 24, DataGridViewContentAlignment.MiddleCenter);

            gridRecientes = CrearTabla();
            AgregarColumna(gridRecientes, "Partida", 22, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridRecientes, "Fecha", 42, DataGridViewContentAlignment.MiddleCenter);
            AgregarColumna(gridRecientes, "Ganador", 36, DataGridViewContentAlignment.MiddleLeft);

            Panel panelJugadores = CrearPanel("Partidas ganadas por jugador", new Point(30, 182), new Size(650, 412), gridJugadores);
            Panel panelRecientes = CrearPanel("Últimas partidas", new Point(700, 182), new Size(370, 412), gridRecientes);

            lblEstado = new Label
            {
                Location = new Point(30, 612),
                Size = new Size(690, 56),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = NegroTranslucido,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            btnActualizar = Boton("Actualizar", new Point(740, 612), new Size(150, 56), AzulUno);
            btnActualizar.Click += async (s, e) => await RecargarAsync();

            Button btnCerrar = Boton("Cerrar", new Point(910, 612), new Size(160, 56), Color.Red);
            btnCerrar.Click += (s, e) => Close();
            CancelButton = btnCerrar;

            Controls.AddRange(new Control[]
            {
                logo, titulo, lblOrigen, lblLider, panelJugadores, panelRecientes, lblEstado, btnActualizar, btnCerrar
            });

            FormClosed += (s, e) =>
            {
                BackgroundImage?.Dispose();
                logo.Image?.Dispose();
                icono?.Dispose();
                fuenteNegrita.Dispose();
            };
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await RecargarAsync();
        }

        // Vuelve a pedir los datos (botón Actualizar o cuando se pulsa "Historial" con la ventana abierta)
        public async Task RecargarAsync()
        {
            if (cargando || IsDisposed)
                return;

            cargando = true;
            btnActualizar.Enabled = false;
            lblEstado.ForeColor = Color.White;
            lblEstado.Text = "Cargando historial...";
            try
            {
                lblOrigen.Text = origen?.Invoke() ?? "";
                HistorialDatos datos = await cargar();
                if (!IsDisposed)
                    Mostrar(datos);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    lblEstado.ForeColor = AmarilloAviso;
                    lblEstado.Text = "No se pudo obtener el historial. " + ex.Message;
                }
            }
            finally
            {
                cargando = false;
                if (!IsDisposed)
                    btnActualizar.Enabled = true;
            }
        }

        private void Mostrar(HistorialDatos datos)
        {
            string yo = miNombre?.Invoke();
            int maximo = datos.Jugadores.Count == 0 ? 0 : datos.Jugadores.Max(j => j.Ganadas);

            gridJugadores.Rows.Clear();
            foreach (EstadisticaJugador j in datos.Jugadores)
            {
                // Los empatados comparten lugar
                int lugar = 1 + datos.Jugadores.Count(otro => otro.Ganadas > j.Ganadas);
                bool esYo = yo != null && string.Equals(j.Nombre, yo, StringComparison.OrdinalIgnoreCase);

                int indice = gridJugadores.Rows.Add(
                    lugar,
                    j.Nombre + (esYo ? "  (tú)" : ""),
                    j.Ganadas,
                    j.Jugadas,
                    j.PorcentajeVictorias + "%",
                    TextoFecha(j.UltimaVictoria));

                DataGridViewRow fila = gridJugadores.Rows[indice];
                if (maximo > 0 && j.Ganadas == maximo)
                    Pintar(fila, Color.Gold, Color.Black);
                if (esYo)
                    fila.DefaultCellStyle.Font = fuenteNegrita;
            }

            gridRecientes.Rows.Clear();
            foreach (PartidaGanada p in datos.Recientes)
                gridRecientes.Rows.Add("#" + p.Id, TextoFecha(p.Fecha), p.Ganador);

            gridJugadores.ClearSelection();
            gridRecientes.ClearSelection();

            var lideres = datos.Jugadores.Where(j => maximo > 0 && j.Ganadas == maximo).Select(j => j.Nombre).ToList();
            lblLider.Visible = lideres.Count > 0;
            if (lideres.Count == 1)
                lblLider.Text = "Líder: " + lideres[0] + " con " + Ganadas(maximo);
            else if (lideres.Count > 1)
                lblLider.Text = "Empate en primer lugar: " + string.Join(", ", lideres) + " con " + Ganadas(maximo);

            int total = datos.Jugadores.Sum(j => j.Ganadas);
            lblEstado.ForeColor = Color.White;
            lblEstado.Text = datos.Jugadores.Count == 0
                ? "Todavía no hay partidas registradas. Cuando alguien gane una ronda aparecerá aquí."
                : "Actualizado a las " + DateTime.Now.ToString("HH:mm:ss") + "   |   " + Ganadas(total) + " en total";
        }

        private static string Ganadas(int n)
        {
            return n == 1 ? "1 partida ganada" : n + " partidas ganadas";
        }

        private static string TextoFecha(DateTime? fecha)
        {
            return fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        }

        private static void Pintar(DataGridViewRow fila, Color fondo, Color texto)
        {
            fila.DefaultCellStyle.BackColor = fondo;
            fila.DefaultCellStyle.SelectionBackColor = fondo;
            fila.DefaultCellStyle.ForeColor = texto;
            fila.DefaultCellStyle.SelectionForeColor = texto;
        }

        // Tabla oscura de solo lectura; la selección usa los mismos colores para que no se note
        private static DataGridView CrearTabla()
        {
            var g = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Celda,
                GridColor = Lineas,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
                ScrollBars = ScrollBars.Vertical,
                TabStop = false
            };
            g.RowTemplate.Height = 34;

            g.ColumnHeadersDefaultCellStyle.BackColor = Encabezado;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gold;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Encabezado;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Gold;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 10.5f, FontStyle.Bold);

            g.DefaultCellStyle.BackColor = Celda;
            g.DefaultCellStyle.ForeColor = Color.White;
            g.DefaultCellStyle.SelectionBackColor = Celda;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Font = new Font("Arial", 10.5f);

            g.AlternatingRowsDefaultCellStyle.BackColor = CeldaAlterna;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = CeldaAlterna;
            return g;
        }

        private static void AgregarColumna(DataGridView g, string titulo, float peso, DataGridViewContentAlignment alineacion)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = titulo,
                FillWeight = peso,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            columna.DefaultCellStyle.Alignment = alineacion;
            columna.HeaderCell.Style.Alignment = alineacion;
            if (alineacion == DataGridViewContentAlignment.MiddleLeft)
                columna.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            g.Columns.Add(columna);
        }

        // Recuadro negro translúcido con título, igual que las etiquetas del tablero
        private static Panel CrearPanel(string titulo, Point ubicacion, Size tamaño, DataGridView tabla)
        {
            var panel = new Panel { Location = ubicacion, Size = tamaño, BackColor = NegroTranslucido };

            var lbl = new Label
            {
                Text = titulo,
                Location = new Point(16, 10),
                Size = new Size(tamaño.Width - 32, 32),
                Font = new Font("Arial", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            tabla.Location = new Point(16, 50);
            tabla.Size = new Size(tamaño.Width - 32, tamaño.Height - 66);

            panel.Controls.Add(lbl);
            panel.Controls.Add(tabla);
            return panel;
        }

        // Mismo estilo que el botón "Jugar" del menú: plano, con borde blanco
        private static Button Boton(string texto, Point ubicacion, Size tamaño, Color fondo)
        {
            var b = new Button
            {
                Text = texto,
                Location = ubicacion,
                Size = tamaño,
                BackColor = fondo,
                ForeColor = Color.White,
                Font = new Font("Arial", 13, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 3;
            b.FlatAppearance.BorderColor = Color.White;
            return b;
        }

        private static Image CargarImagen(string archivo, Size tamaño)
        {
            string ruta = Path.Combine(Application.StartupPath, "Recursos", archivo);
            if (!File.Exists(ruta))
                return null;
            using (Image original = Image.FromFile(ruta))
                return new Bitmap(original, tamaño);
        }
    }
}
