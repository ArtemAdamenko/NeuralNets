using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using AForge.Controls;
using AForge.Neuro;
using AForge;


namespace Neural
{
    public partial class FormDrawNeurons : Form
    {
        //Neural Net options
        private double[,] data = new double[0, 0];
        private InputLayer _inputLayer;
        private HiddenLayer[] _hiddenLayers;
        private OutputLayer _output;
        private double[][][] tempWeights;

        //draw options
        private SolidBrush _myBrush = new SolidBrush(Color.Blue);
        private SolidBrush _offBrush = new SolidBrush(Color.Gray);
        private Pen _myPen = new Pen(Color.Black);

        //App options
        Network network = null;
        Thread Worker;

        public FormDrawNeurons()
        {
            InitializeComponent();
        }

        /**
         * Нарисовать связующие линнии между слоями
         * */
        private static void drawLines(Graphics gr, Pen myPen, System.Drawing.Point[] layer1, System.Drawing.Point[] layer2)
        {
            for (int i = 0; i < layer1.Length; i++)
            {
                for (int j = 0; j < layer2.Length; j++)
                {
                    gr.DrawLine(myPen, layer1[i], layer2[j]);
                }
            }
        }

        public void draw()
        {
            Bitmap bmp;
            Graphics formGraphics;

            // Create a bitmap the size of the form.
            //first layer does not contains in network.Layers, because +1
            bmp = new Bitmap(700, 530);

            formGraphics = Graphics.FromImage(bmp);
            for (int i = 0; i < this._hiddenLayers.Length; i++)
            {
                this._hiddenLayers[i].drawHiddenLayer(200 * (i + 1), formGraphics);
            }

            this._inputLayer.drawInputLayer(0, formGraphics);

            drawLines(formGraphics, this._myPen, this._inputLayer.getRightPoint(), this._hiddenLayers[0].getLeftPoints());

            for (int i = 0; i < this._hiddenLayers.Length - 1; i++)
            {
                drawLines(formGraphics, this._myPen, this._hiddenLayers[i].getRightPoints(), this._hiddenLayers[i + 1].getLeftPoints());
            }

            this._output.drawOutputLayer(network.Layers.Length * 200, formGraphics);

            HiddenLayer.RollbackNumberLayers();
            drawLines(formGraphics, this._myPen, this._hiddenLayers[this._hiddenLayers.Length - 1].getRightPoints(), this._output.getLeftPoints());

            
            pictureBox1.Image = bmp;
        }


        /*
         * Заполняет таблицу значениями 
         * слоев нейронов и весов соответственно
         */
        private void setNeuronsDataGrid() 
        { 
            for (int i = 0; i < network.Layers.Length-1; i++)
            {
                for (int j = 0; j < network.Layers[i].Neurons.Length; j++)
                {
                                                                               //в массиве весов нейрона, в данном случае всегда 1 вес
                    this.dataGridView1.Rows.Add((i+1).ToString(), j.ToString(), network.Layers[i].Neurons[j].Weights[0].ToString());
                }
            }

        }

        //При загрузке отрисовываем топологию
        private void FormDrawNeurons_Load_1(object sender, EventArgs e)
        {
           // draw();
        }

        //Запуск сети для проверки
        private void button1_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.dataGridView1.RowCount; i++)
            {
                int layer = Int32.Parse(dataGridView1.Rows[i].Cells[0].Value.ToString()) - 1;
                int neuron = Int32.Parse(dataGridView1.Rows[i].Cells[1].Value.ToString());

                //если стоит галочка, значит отключаем нейрон и записываем его вес во временной массив,
                //чтобы потом можно было обратно включить нейрон
                if (this.dataGridView1.Rows[i].Cells[2].Value == "T")
                {
                    tempWeights[layer][neuron][0] = network.Layers[layer].Neurons[neuron].Weights[0];
                    network.Layers[layer].Neurons[neuron].Weights[0] = 0.0;
                    this._hiddenLayers[layer].setOffNeuron(neuron);
                }
                else {
                    //если галочка не стоит, и вес этого нейрона записан в временном массиве,
                    //значит он был отключен, а сейчас его нужно включить
                    if (tempWeights[layer][neuron][0] != 0.0)
                    {
                        network.Layers[layer].Neurons[neuron].Weights[0] = tempWeights[layer][neuron][0];
                        tempWeights[layer][neuron][0] = 0.0;
                        this._hiddenLayers[layer].setOnNeuron(neuron);
                    }
                }
            }

            draw();

            double[,] tempData = new double[data.GetLength(0) + 1, 2];
            Array.Copy(data, 0, tempData, 0, data.GetLength(0) * 2);

           
        }

        /**
         * Загрузка нейронной сети
         * */
        private void LoadNetToolStripMenuItem_Click(object sender, EventArgs e)
        {

            Worker = new Thread(LoadNet);
            Worker.SetApartmentState(ApartmentState.STA);
            Worker.Start();
            
        }

        private void LoadNet()
        {
            // Initialize the OpenFileDialog to look for text files.
            openFileDialog1.Filter = "Bin Files|*.bin";

            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    network = Network.Load(openFileDialog1.FileName);
                }
                catch (IOException)
                {
                    throw new IOException("Ошибка загрузки нейронной сети");
                }
                finally
                {
                    this.Invoke(new Action(InitWork));
                    this.Invoke(new Action(draw));
                    Worker.Abort();
                }
            }
        }


        /**
         * Инициализация компонентов для работы
         * */
        private void InitWork()
        {
            this.setNeuronsDataGrid();
            //input
            InputLayer input = new InputLayer(new double[1] { 1.0 });
            this._inputLayer = input;

            //hidden layers
            this._hiddenLayers = new HiddenLayer[network.Layers.Length - 1];

            for (int i = 0; i < network.Layers.Length - 1; i++)
            {
                int layerNeuronsCnt = network.Layers[i].Neurons.Length;
                HiddenLayer hidden = new HiddenLayer(layerNeuronsCnt);
                this._hiddenLayers[i] = hidden;
            }

            tempWeights = new double[network.Layers.Length][][];
            for (int i = 0; i < network.Layers.Length; i++)
            {
                tempWeights[i] = new double[network.Layers[i].Neurons.Length][];
                for (int j = 0; j < network.Layers[i].Neurons.Length; j++)
                {
                    tempWeights[i][j] = new double[1];
                }
            }

            //output
            //OutputLayer output = new OutputLayer(1);
            this._output = new OutputLayer(1);
        }


    }
}
