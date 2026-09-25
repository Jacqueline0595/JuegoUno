using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InterfazUno
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            this.Text = "Uno";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimizeBox = false;
            this.CenterToScreen();
            Image fondo = CargarImagen("juego_fondo.jpg", new Size(736, 368), "Recursos");
            this.BackgroundImage = fondo;
            this.BackgroundImageLayout = ImageLayout.Stretch;
            RepartirCartas();

        }
        private Image CargarImagen(string nombreArchivo, Size tamaño, String ruta_d)
        {
            string ruta = Path.Combine(Application.StartupPath, ruta_d, nombreArchivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"No se encontró la imagen: {ruta}");
            Image original = Image.FromFile(ruta);
            return new Bitmap(original, tamaño);
        }
        private void RepartirCartas()
        {
            string[] usados = new string[];
            string[] archivos = Directory.GetFiles("C:\Users\aslan\source\repos\InterfazUno\InterfazUno\bin\Debug\Cartas");
            if (archivos.Length > 0) {
                int ran_indice = Random.Shared.Next(archivos.Length);
                string select = archivos[ran_indice];
                Console.WriteLine(select);
                /*Label imagen = new Label();
                imagen.Image = prueba;
                this.Controls.Add(imagen);*/
            }
            else
            {
                Console.WriteLine("La carpeta esta vacia");
            }

        }
    }
}
