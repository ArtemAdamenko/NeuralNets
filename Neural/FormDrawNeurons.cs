﻿using System;
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
        private double[,] data = null;
        private InputLayer _inputLayer;
        private HiddenLayer[] _hiddenLayers;
        private OutputLayer _output;
        private double[][][][] tempWeights;

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

            Layer firstHiddenLayer = network.Layers[0];

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
            for (int i = 0; i < network.Layers.Length; i++)
            {
                for (int j = 0; j < network.Layers[i].Neurons.Length; j++)
                {
                    for (int k = 0; k < network.Layers[i].Neurons[j].Weights.Length; k++ )

                        this.dataGridView1.Rows.Add(i.ToString(), j.ToString(), k.ToString(), network.Layers[i].Neurons[j].Weights[k].ToString(), "F");
                }
            }

        }

        //Запуск сети для проверки
        private void CheckNeurons()
        {
            for (int i = 0; i < this.dataGridView1.RowCount; i++)
            {
                int layer = Int32.Parse(dataGridView1.Rows[i].Cells[0].Value.ToString());
                int neuron = Int32.Parse(dataGridView1.Rows[i].Cells[1].Value.ToString());
                int weight = Int32.Parse(dataGridView1.Rows[i].Cells[2].Value.ToString());

                //если стоит галочка и до этого момента нейрон не отключался, значит отключаем нейрон и записываем его вес во временный массив,
                //чтобы потом можно было обратно включить нейрон
                if ((this.dataGridView1.Rows[i].Cells[4].Value.ToString() == "T".ToString()) &&
                    network.Layers[layer].Neurons[neuron].Weights[weight] != 0.0)
                {
                    tempWeights[layer][neuron][weight][0] = network.Layers[layer].Neurons[neuron].Weights[weight];
                    network.Layers[layer].Neurons[neuron].Weights[weight] = 0.0;
                    //this._hiddenLayers[layer].setOffNeuron(neuron);
                    this.dataGridView1.Rows[i].Cells[3].Value = 0.0.ToString();
                }
                else {
                    //если галочка не стоит, и вес этого нейрона записан в временном массиве,
                    //значит он был отключен, а сейчас его нужно включить
                    if ((this.dataGridView1.Rows[i].Cells[4].Value.ToString() == "F".ToString()) && tempWeights[layer][neuron][weight][0] != 0.0)
                    {
                        network.Layers[layer].Neurons[neuron].Weights[weight] = tempWeights[layer][neuron][weight][0];
                        tempWeights[layer][neuron][weight][0] = 0.0;
                        //this._hiddenLayers[layer].setOnNeuron(neuron);
                        this.dataGridView1.Rows[i].Cells[3].Value = network.Layers[layer].Neurons[neuron].Weights[weight].ToString();
                    }
                }
            }

            draw();
        }

        /**
         * Запуск потока загрузки нейронной сети
         * */
        private void LoadNetToolStripMenuItem_Click(object sender, EventArgs e)
        {

            Worker = new Thread(LoadNet);
            Worker.SetApartmentState(ApartmentState.STA);
            Worker.Start();
            
        }

        /**
         * Загрузка нейронной сети
         * */
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

            tempWeights = new double[network.Layers.Length][][][];
            for (int i = 0; i < network.Layers.Length; i++)
            {
                tempWeights[i] = new double[network.Layers[i].Neurons.Length][][];
                for (int j = 0; j < network.Layers[i].Neurons.Length; j++)
                {
                    tempWeights[i][j] = new double[network.Layers[i].Neurons[j].Weights.Length][];
                    for (int k = 0; k < network.Layers[i].Neurons[j].Weights.Length; k++)
                    {
                        tempWeights[i][j][k] = new double[1];
                        tempWeights[i][j][k][0] = network.Layers[i].Neurons[j].Weights[k];
                    }
                }
            }

            this._output = new OutputLayer(1);
        }

        /**
         * Загрузка выборки
         * */
        private void loadTestData()
        {

            // show file selection dialog
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                StreamReader reader = null;

                try
                {
                    // open selected file
                    reader = File.OpenText(openFileDialog2.FileName);

                    //get count values
                    String line;
                    int rowCount = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        rowCount++;
                    }
                    double[,] tempData = new double[rowCount, 3];

                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    line = "";
                    int i = 0;


                    // read the data
                    while ((i < rowCount) && ((line = reader.ReadLine()) != null))
                    {
                        string[] strs = line.Split('-');
                        // parse input and output values for learning
                        tempData[i, 0] = double.Parse(strs[0]);
                        tempData[i, 1] = double.Parse(strs[1]);
                        tempData[i, 2] = double.Parse(strs[2]);

                        i++;
                    }

                    // allocate and set data
                    data = new double[i, 3];
                    Array.Copy(tempData, 0, data, 0, i * 3);
                }
                catch (Exception)
                {
                    MessageBox.Show("Failed reading the file", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    // close file
                    if (reader != null)
                        reader.Close();
                    Worker.Abort();
                }
            }
        }

        /**
         * Вызов потока загрузки выборки
         * */
        private void LoadDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Worker = new Thread(loadTestData);
            Worker.SetApartmentState(ApartmentState.STA);
            Worker.Start();
        }

        /**
         * Процент ошибки при тестировании на выбранной выборке
         * */
        private void testNetButton_Click(object sender, EventArgs e)
        {
            this.CheckNeurons();
            double testError = 0.0;
            for (int i = 0; i < data.GetLength(0); i++)
            {
                testError += Math.Abs(network.Compute(new double[2] { data[i, 0], data[i, 1] })[0] - data[i,2]);
            }
            this.testErrorLabel.Text = (testError/data.GetLength(0)).ToString("F10");
        }


    }
}
