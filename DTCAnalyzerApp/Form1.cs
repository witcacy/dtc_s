
using System;
using System.IO;
using System.Windows.Forms;

namespace DTCAnalyzerApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
           
        }

      

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "TRC files (*.trc)|*.trc";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string inputPath = dialog.FileName;
                string htmlPath = DTCInterpreter.ProcesarTRC(inputPath);
                MessageBox.Show(@"Reporte generado:
" + htmlPath, "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start("explorer", htmlPath);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "TRC files (*.trc)|*.trc";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string inputPath = dialog.FileName;
                string htmlPath = DTCInterpreter.GenerarReporteInterpretacionDetallada(inputPath);
                MessageBox.Show(@"Reporte generado:
" + htmlPath, "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start("explorer", htmlPath);
            }
            
        }
    }
}
