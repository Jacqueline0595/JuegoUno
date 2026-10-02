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
    {   //comentario para que aparezca el form2
        string[] nombres = { "Jugador 1", "Jugador 2", "Jugador 3", "Jugador 4" };
        Label[] lblNombres = new Label[4];
        List<Label>[] manos = new List<Label>[4];   

        List<string> usados;
        string[] archivos;
        string ruta_d = "C:\\Users\\jacqu\\source\\repos\\JuegoUno\\InterfazUno\\bin\\Debug\\Cartas\\";
        string texto_elim = "C:\\Users\\jacqu\\source\\repos\\JuegoUno\\InterfazUno\\bin\\Debug\\Cartas\\";
        Size tam = new Size(100, 150);
        Size tam_2 = new Size(150, 100);
        Random ran = new Random();

        Label lblBaraja;  
        Label cartaMesa;   
        Label lblInfo;
        Button btnReinicio;
        int partida = 0;
        int repartidor;
        int turno;
        int direccion = 1;

        public Form2()
        {
            InitializeComponent();
            this.Icon = new Icon("Recursos\\logoUno.ico");
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
            this.Text = "Uno";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimizeBox = false;
            this.CenterToScreen();
            Image fondo = CargarImagen("juego_fondo.jpg", new Size(736, 368), "Recursos");
            this.BackgroundImage = fondo;
            this.BackgroundImageLayout = ImageLayout.Stretch;
            archivos = Directory.GetFiles(ruta_d);
            CrearNombres();
            CrearBaraja();
            CrearBotonReinicio();
            await IniciarJuego();
        }

        private Image CargarImagen(string nombreArchivo, Size tamaño, String ruta_d)
        {
            string ruta = Path.Combine(Application.StartupPath, ruta_d, nombreArchivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"No se encontró la imagen: {ruta}");
            Image original = Image.FromFile(ruta);
            return new Bitmap(original, tamaño);
        }
        private void CrearBotonReinicio()
        {
            btnReinicio = new Button();
            btnReinicio.Text = "Reiniciar";
            btnReinicio.Size = new Size(120, 36);
            btnReinicio.Location = new Point(10, 10);
            btnReinicio.Font = new Font("Arial", 11, FontStyle.Bold);
            btnReinicio.FlatStyle = FlatStyle.Flat;
            btnReinicio.BackColor = Color.Red;
            btnReinicio.ForeColor = Color.Black;
            btnReinicio.Cursor = Cursors.Hand;
            btnReinicio.Click += btnReinicio_Click;
            this.Controls.Add(btnReinicio);
        }
        private async void btnReinicio_Click(object sender, EventArgs e)
        {
            LimpiarManos();
            if (cartaMesa != null)
            {
                this.Controls.Remove(cartaMesa);
                cartaMesa.Dispose();
                cartaMesa = null;
            }
            Resaltar(-1);
            await IniciarJuego();
        }
        private void CrearNombres()
        {
            Point[] pos = { new Point(610, 195), new Point(1295, 100), new Point(610, 855), new Point(45, 100) };
            for (int i = 0; i < 4; i++)
            {
                lblNombres[i] = new Label();
                lblNombres[i].Text = nombres[i];
                lblNombres[i].Size = new Size(160, 34);
                lblNombres[i].Location = pos[i];
                lblNombres[i].TextAlign = ContentAlignment.MiddleCenter;
                lblNombres[i].Font = new Font("Arial", 12, FontStyle.Bold);
                lblNombres[i].ForeColor = Color.White;
                lblNombres[i].BackColor = Color.FromArgb(170, 0, 0, 0);
                this.Controls.Add(lblNombres[i]);
                manos[i] = new List<Label>();
            }

            lblInfo = new Label();
            lblInfo.Size = new Size(400, 32);
            lblInfo.Location = new Point(490, 540);
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            lblInfo.Font = new Font("Arial", 12, FontStyle.Bold);
            lblInfo.ForeColor = Color.White;
            lblInfo.BackColor = Color.FromArgb(170, 0, 0, 0);
            this.Controls.Add(lblInfo);
        }

        private void CrearBaraja()
        {
            lblBaraja = new Label();
            lblBaraja.Size = tam;
            lblBaraja.Location = new Point(580, 370);
            lblBaraja.Image = CargarImagen("carta_uno.png", tam, ruta_d);
            lblBaraja.Cursor = Cursors.Hand;
            lblBaraja.Click += Baraja_Click;
            this.Controls.Add(lblBaraja);
        }

        private void Resaltar(int jugador)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == jugador)
                {
                    lblNombres[i].BackColor = Color.Gold;
                    lblNombres[i].ForeColor = Color.Black;
                }
                else
                {
                    lblNombres[i].BackColor = Color.FromArgb(170, 0, 0, 0);
                    lblNombres[i].ForeColor = Color.White;
                }
            }
        }

        private async Task IniciarJuego()
        {
            int id = ++partida;
            usados = new List<string>() { "carta_vacia.png", "carta_uno.png", "carta_vacia.png", "carta_uno.png" };
            lblInfo.Text = "Cada jugador elige una carta";
            await Task.Delay(800);
            if (id != partida) 
                return;
            await ElegirRepartidor(id);
            if (id != partida) 
                return;
            await RepartirCartas(id);
        }

        private async Task ElegirRepartidor(int id)
        {
            while (true)
            {
                List<string> temp = new List<string>() { "carta_vacia.png", "carta_uno.png", "carta_vacia.png", "carta_uno.png" };
                int[] valores = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    Resaltar(i);
                    lblInfo.Text = nombres[i] + " Elige una carta";
                    string nombre = CartaAlAzar(temp);
                    CrearCarta(nombre, i);
                    valores[i] = ValorCarta(nombre);
                    await Task.Delay(800);
                    if (id != partida) 
                        return;
                }
                int max = valores.Max();
                int cuantos = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (valores[i] == max)
                        cuantos++;
                }
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
            int cont = (repartidor + 1) % 4;
            int imagenes = 0;
            while (imagenes < 28)
            {
                string nombre = CartaAlAzar(usados);
                CrearCarta(nombre, cont);
                cont = (cont + 1) % 4;
                imagenes++;
                await Task.Delay(50);
                if (id != partida)
                    return;
            }
            string centro = CartaAlAzar(usados);
            while (centro.StartsWith("mas_cuatro"))
            {
                usados.Remove(centro);
                centro = CartaAlAzar(usados);
            }
            cartaMesa = new Label();
            cartaMesa.Size = tam;
            cartaMesa.Location = new Point(700, 370);
            cartaMesa.Image = CargarImagen(centro, tam, ruta_d);
            cartaMesa.Tag = centro;
            this.Controls.Add(cartaMesa);
            direccion = 1;
            turno = (repartidor + 1) % 4;
            string msg = "Repartio " + nombres[repartidor];
            if (centro.StartsWith("reverse"))
            {
                direccion = -1;
                turno = repartidor;
            }
            if (centro.StartsWith("bloqueo"))
            {
                msg += ". " + nombres[turno] + " Fue bloqueado ";
                turno = (repartidor + 1) % 4;
            }
            if(centro.StartsWith("mas_dos"))
            {
                msg += ". " + nombres[turno] + " Toma dos cartas ";
                turno = (repartidor + 1) % 4;

            }
            if(centro.StartsWith("cambiar_color"))
            {
               msg += ". " + nombres[turno] + " Cambia el color ";
               turno = (repartidor + 1) % 4;
            }
            Resaltar(turno);
            lblInfo.Text = msg + ". Empieza " + nombres[turno];
        }
        private string CartaAlAzar(List<string> lista)
        {
            while (true)
            {
                string select = archivos[ran.Next(archivos.Length)];
                string nombre = Path.GetFileName(select);
                if (!Comprueba_usados(lista, nombre))
                {
                    lista.Add(nombre);
                    return nombre;
                }
            }
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
        private Label CrearCarta(string nombre, int jugador)
        {
            Label img = new Label();
            Image carta = CargarImagen(nombre, tam, ruta_d);
            int sumador = manos[jugador].Count * 80;
            switch (jugador)
            {
                case 0:
                    img.Location = new Point(400 + sumador, 40);
                    img.Size = tam;
                    break;
                case 1:
                    img.Location = new Point(1300, 150 + sumador);
                    carta.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    img.Size = tam_2;
                    break;
                case 2:
                    img.Location = new Point(400 + sumador, 700);
                    img.Size = tam;
                    break;
                case 3:
                    img.Location = new Point(50, 150 + sumador);
                    carta.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    img.Size = tam_2;
                    break;
            }
            img.Image = carta;
            img.ImageAlign = ContentAlignment.TopLeft;
            img.Tag = nombre;
            img.Name = jugador.ToString();
            img.MouseEnter += img_MouseEnter;
            img.MouseLeave += img_MouseLeave;
            this.Controls.Add(img);
            manos[jugador].Add(img);
            return img;
        }

        private void LimpiarManos()
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (Label l in manos[i])
                {
                    this.Controls.Remove(l);
                    l.Dispose();
                }
                manos[i].Clear();
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
           
        }
    }
}