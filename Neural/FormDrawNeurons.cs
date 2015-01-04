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
        private double[,] data = null;
        int rowCountData = 0;
        int colCountData = 0;

        private double[][][][] tempWeights;

        //draw options
        private SolidBrush _myBrush = new SolidBrush(Color.Blue);
        private Pen _myPen = new Pen(Color.Black);

        //App options
        Network network = null;
        Thread Worker;

        public FormDrawNeurons()
        {
            InitializeComponent();
        }

        public void draw()
        {
            Bitmap bmp;
            Graphics formGraphics;

            // Create a bitmap the size of the form.
            //first layer does not contains in network.Layers, because +1
            bmp = new Bitmap(700, 530);

            formGraphics = Graphics.FromImage(bmp);
            int x = 0;

            //draw input layer
            int cntInput = network.InputsCount;
            _myPen.Width = 3;
            System.Drawing.Point[] fisrtPoints = new System.Drawing.Point[cntInput];

            for (int k = 0; k < cntInput; k++) 
            {
                Rectangle ellipse = new Rectangle(x, 50 * k, 100, 50);
                formGraphics.FillEllipse(this._myBrush, ellipse);
                formGraphics.DrawEllipse(this._myPen, ellipse);
                fisrtPoints[k].X = x + 100;
                fisrtPoints[k].Y = 50 * k + (50 / 2);
            }


            System.Drawing.Point[] tempRightPoints = new System.Drawing.Point[1];
            //draw other layers
            for (int i = 0; i < network.Layers.Length; i++)
            {
                x = 200 * (i+1);

                System.Drawing.Point[] hiddenLeftPoints = new System.Drawing.Point[network.Layers[i].Neurons.Length];
                System.Drawing.Point[] hiddenRightPoints = new System.Drawing.Point[network.Layers[i].Neurons.Length];

                for (int j = 0; j < network.Layers[i].Neurons.Length; j++)
                {
                    _myPen.Color = Color.Black;
                    Rectangle ellipse = new Rectangle(x, 50 * j, 100, 50);
                    formGraphics.FillEllipse(this._myBrush, ellipse);
                    formGraphics.DrawEllipse(this._myPen, ellipse);

                    hiddenLeftPoints[j].X = x;
                    hiddenLeftPoints[j].Y = 50 * j + (50 / 2);

                    hiddenRightPoints[j].X = x + 100;
                    hiddenRightPoints[j].Y = 50 * j + (50 / 2);

                    //if this first hidden layer
                    if (i == 0)
                    {
                        //all neurons n-1 layer
                        for (int b = 0; b < fisrtPoints.Length; b++)
                        {
                            //if weight current neuron == 0.0
                            if (network.Layers[i].Neurons[j].Weights[b] == 0.0)
                                _myPen.Color = Color.Red;
                            else
                                _myPen.Color = Color.Black;
                            formGraphics.DrawLine(_myPen, hiddenLeftPoints[j], fisrtPoints[b]);
                        }
                    }
                    else if (i != 0)
                    {
                        for (int c = 0; c < network.Layers[i-1].Neurons.Length; c++)
                        {
                            //if weight current neuron == 0.0
                            if (network.Layers[i].Neurons[j].Weights[c] == 0.0)
                                _myPen.Color = Color.Red;
                            else
                                _myPen.Color = Color.Black;
                            formGraphics.DrawLine(_myPen, hiddenLeftPoints[j], tempRightPoints[c]);
                        }
                    }
                 }
                //temp mass of right points of current layer for next cycle
                tempRightPoints = new System.Drawing.Point[network.Layers[i].Neurons.Length];
                tempRightPoints = hiddenRightPoints;
            }

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
                    this.dataGridView1.Rows[i].Cells[3].Value = 0.0.ToString();
                }
                else {
                    //если галочка не стоит, и вес этого нейрона записан в временном массиве,
                    //значит он был отключен, а сейчас его нужно включить
                    if ((this.dataGridView1.Rows[i].Cells[4].Value.ToString() == "F".ToString()) && tempWeights[layer][neuron][weight][0] != 0.0)
                    {
                        network.Layers[layer].Neurons[neuron].Weights[weight] = tempWeights[layer][neuron][weight][0];
                        tempWeights[layer][neuron][weight][0] = 0.0;
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

            //init temp weights for remember check off user weights
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

                    //get row count values
                    String line;
                    rowCountData = 0;
                    colCountData = 0;

                    //get input and output count
                    line = reader.ReadLine();
                    rowCountData++;
                    colCountData = line.Split(';').Length;

                    //must be > 1 column in training data
                    if (colCountData == 1)
                        throw new Exception();

                    while ((line = reader.ReadLine()) != null)
                    {
                        rowCountData++;
                    }

                    double[,] tempData = new double[rowCountData, colCountData];

                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    line = "";
                    int i = 0;

                    // read the data
                    while ((i < rowCountData) && ((line = reader.ReadLine()) != null))
                    {
                        string[] strs = line.Split(';');
                        // parse input and output values for learning
                        //gather all values by cols
                        for (int j = 0; j < colCountData; j++)
                        {
                            tempData[i, j] = double.Parse(strs[j]);
                        }

                        i++;
                    }

                    // allocate and set data
                    data = new double[i, colCountData];
                    Array.Copy(tempData, 0, data, 0, i * colCountData);
                    this.testNetButton.Invoke(new Action(() => this.testNetButton.Enabled = true));

                }
                catch (Exception)
                {
                    MessageBox.Show("Ошибка чтения файла", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    // close file
                    if (reader != null)
                        reader.Close();
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
            this.errorTextBox.Text = (testError/data.GetLength(0)).ToString("F10");
        }


    }
}
