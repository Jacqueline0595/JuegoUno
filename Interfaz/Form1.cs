using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InterfazUno
{
    public partial class Form1 : Form
    {
        Font letra = new Font("Arial", 14, FontStyle.Bold);
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "Uno";
            Image imgFondo = CargarImagen("r.jpg", new Size(2200, 1440), "Recursos");
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimizeBox = false;
            this.CenterToScreen();
            this.BackgroundImage = imgFondo;

            this.BackgroundImageLayout = ImageLayout.Stretch;
            
            Button play = new Button();
            play.Text = "Jugar";
            play.Font = new Font("Cabin Bold", 60, FontStyle.Bold);
            play.ForeColor = Color.White;
            play.BackColor = Color.Red;
            play.Size = new Size(350, 125);
            play.Location = new Point((this.Width - play.Width) / 2, 750);
            play.FlatAppearance.BorderSize = 4;
            play.FlatAppearance.BorderColor = Color.White;
            play.FlatStyle = FlatStyle.Flat;
            play.Cursor = Cursors.Hand;
            play.Click += Play_Click;
            this.Controls.Add(play);
        }

        private Image CargarImagen(string nombreArchivo, Size tamaño, String ruta_d)
        {
            string ruta = Path.Combine(Application.StartupPath, ruta_d, nombreArchivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"No se encontró la imagen: {ruta}");
            Image original = Image.FromFile(ruta);
            return new Bitmap(original, tamaño);
        }

        private void Play_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2();
            form2.Show();
            this.Hide();
            form2.FormClosed += (s, args) => this.Close();
        }
    }
}
