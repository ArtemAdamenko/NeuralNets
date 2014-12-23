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
using AForge;
using AForge.Controls;
using AForge.Neuro;
using AForge.Neuro.Learning;


namespace Neural
{
    public partial class Form1 : Form
    {
        private double[,] data = null;

        private double learningRate = 0.1;
        private double momentum = 0.0;
        private int iterations = 10000;
        private int[] neuronsAndLayers;
        private int[] classes;
        private int classesCount;
        private int[] samplesPerClass;

        private Thread workerThread = null;
        private bool needToStop = false;
        Random rnd = new Random();
        ActivationNetwork network;

        // Constructor
        public Form1()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // init controls
            UpdateSettings();
        }

        // On main form closing
        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // check if worker thread is running
            if ((workerThread != null) && (workerThread.IsAlive))
            {
                needToStop = true;
                workerThread.Join();
            }
        }

        // Update settings controls
        private void UpdateSettings()
        {
            this.learningRateBox.Text = learningRate.ToString();
            this.momentumBox.Text = momentum.ToString();
        }

        // Load data
        private void loadDataButton_Click(object sender, System.EventArgs e)
        {
            // show file selection dialog
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                StreamReader reader = null;
                
                try
                {
                    // open selected file
                    reader = File.OpenText(openFileDialog.FileName);

                    //get count values
                    String line;
                    int rowCount = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        rowCount++;
                    }
                    double[,] tempData = new double[rowCount, 3];
                    int[] tempClasses = new int[rowCount];

                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    line = "";
                    int i = 0;
                    // classes count
                    classesCount = 0;
                    samplesPerClass = new int[10];


                    // read the data
                    while ((i < rowCount) && ((line = reader.ReadLine()) != null))
                    {
                        string[] strs = line.Split('-');
                        // parse input and output values for learning
                        //input
                        tempData[i, 0] = double.Parse(strs[0]);
                        tempData[i, 1] = double.Parse(strs[1]);
                        tempData[i, 2] = double.Parse(strs[2]);
                        //output
                       /* tempClasses[i] = int.Parse(strs[2]);

                        // skip classes over 10, except only first 10 classes
                        if (tempClasses[i] >= 10)
                            continue;

                        // count the amount of different classes
                        if (tempClasses[i] >= classesCount)
                            classesCount = tempClasses[i] + 1;
                        // count samples per class
                        samplesPerClass[tempClasses[i]]++;*/


                        i++;
                    }

                    // allocate and set data
                    data = new double[i, 3];
                    Array.Copy(tempData, 0, data, 0, i * 3);
                    //classes = new int[i];
                   // Array.Copy(tempClasses, 0, classes, 0, i);

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
                }

                // update list and chart
                UpdateDataListView();
                // enable "Start" button
                startButton.Enabled = true;
            }
        }

        // Update data in list view
        private void UpdateDataListView()
        {
            // remove all current records
            dataList.Items.Clear();
            // add new records
            for (int i = 0, n = data.GetLength(0); i < n; i++)
            {
                dataList.Items.Add(data[i, 0].ToString());
                dataList.Items[i].SubItems.Add(data[i, 1].ToString());
                dataList.Items[i].SubItems.Add(data[i, 2].ToString());
                //dataList.Items[i].SubItems.Add(classes[i].ToString());
            }
        }

        // Enable/disale controls
        private void EnableControls(bool enable)
        {
            loadDataButton.Invoke(new Action(() => loadDataButton.Enabled = enable));
            
            learningRateBox.Invoke(new Action(() => learningRateBox.Enabled = enable));
            
            momentumBox.Invoke(new Action(() => momentumBox.Enabled = enable));
            
            neuronsBox.Invoke(new Action(() => neuronsBox.Enabled = enable));

            startButton.Invoke(new Action(() => startButton.Enabled = enable));

            stopButton.Invoke(new Action(() => stopButton.Enabled = !enable));

        }

        // On button "Start"
        private void startButton_Click(object sender, System.EventArgs e)
        {
            // get learning rate
            try
            {
                learningRate = Math.Max(0.00001, Math.Min(1, double.Parse(learningRateBox.Text)));
            }
            catch
            {
                learningRate = 0.1;
            }
            // get momentum
            try
            {
                momentum = Math.Max(0, Math.Min(0.5, double.Parse(momentumBox.Text)));
            }
            catch
            {
                momentum = 0;
            }
            // get neurons count in first layer
            try
            {
                String[] temp = neuronsBox.Text.Split(',');
                neuronsAndLayers = new int[temp.Length];
                for (int i = 0; i < temp.Length; i++)
                {
                    neuronsAndLayers[i] = Math.Max(1, Math.Min(50, int.Parse(temp[i])));
                }
                if (neuronsAndLayers.Length < 2)
                    throw new Exception();
            }
            catch
            {
                neuronsAndLayers = new int[2];
                neuronsAndLayers[0] = 10;
                neuronsAndLayers[1] = 1;
            }

            // update settings controls
            UpdateSettings();

            // disable all settings controls except "Stop" button
            EnableControls(false);

            // run worker thread
            needToStop = false;
            workerThread = new Thread(new ThreadStart(SearchSolution));
            workerThread.Start();
        }

        // On button "Stop"
        private void stopButton_Click(object sender, System.EventArgs e)
        {
            // stop worker thread
            needToStop = true;
        }

        // Worker thread
        void SearchSolution()
        {
            // number of learning samples
            int samples = data.GetLength(0);

            // prepare learning data
            //80% training, 20% for validate data(to do 70% lear., 20% validate, 10% test)
            double[][] input = new double[samples*4/5][];
            double[][] output = new double[samples*4/5][];
            double[][] validateInput = new double[samples / 5][];
            double[][] validateOutput = new double[samples / 5][];

            IActivationFunction n = null;
            // create multi-layer neural network

            n = new ThresholdFunction();
            int k = 0;
            int j = 0;

            for (int i = 1; i < samples; i++)
            {
                //80% training, 20% for validate data(to do 70% lear., 20% validate, 10% test)
                if ((i % 5) == 0)
                {
                    validateInput[k] = new double[2];
                    validateOutput[k] = new double[1];

                    validateInput[k][0] = data[i, 0];
                    validateInput[k][1] = data[i, 1];
                    validateOutput[k][0] = data[i, 2];
                    k++;
                }
                else
                {
                    input[j] = new double[2];
                    //output[i] = new double[classesCount];
                    output[j] = new double[1];

                    input[j][0] = data[i, 0];
                    input[j][1] = data[i, 1];
                    //output[i][classes[i]] = 1;
                    output[j][0] = data[i, 2];
                    j++;
                }
            }

            network = new ActivationNetwork(new  SigmoidFunction(),
            2, neuronsAndLayers);
            // create teacher
            BackPropagationLearning teacher = new BackPropagationLearning(network);
            //PerceptronLearning teacher = new PerceptronLearning(network);

            // set learning rate and momentum
            teacher.LearningRate = learningRate;
            teacher.Momentum = momentum;

            // iterations
            int iteration = 1;
            double error = 0.0;
            double[] validateError = new double[1]{0.0};
            //int j = 0;
            // loop
            while (!needToStop)
            {

                    // run epoch of learning procedure
                    error = teacher.RunEpoch(input, output);
                    validateError[0] = 0.0;
                    for (int count = 0; count < validateInput.GetLength(0)-1; count++)
                    {
                        validateError[0] += network.Compute(validateInput[count])[0] - validateOutput[count][0];
                    }

                        // set current iteration's info
                    currentIterationBox.Invoke(new Action<string>((s) => currentIterationBox.Text = s), iteration.ToString());
                    errorPercent.Invoke(new Action<string>((s) => errorPercent.Text = s), error.ToString("F14"));
                    validErrorBox.Invoke(new Action<string>((s) => validErrorBox.Text = s), (validateError[0]/1000).ToString("F14"));
                    // increase current iteration
                    iteration++;
            }
            // enable settings controls
            EnableControls( true );
        }

        /**
        * Сохранение нейронной сети по указанному пути
        * */
        private void SaveNetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "bin files (*.bin)|*.bin";
            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    && saveFileDialog1.FileName.Length > 0)
            {

                network.Save(saveFileDialog1.FileName);
                MessageBox.Show("Сеть сохранена");
            }
        
        }

        private void TestNetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String[] lines = new String[1];
            double[] res = new double[1];
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\SpreadSheetTest.csv"))
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    res[0] = network.Compute(new double[2] { data[i, 0], data[i, 1] })[0];
                    lines[0] = data[i, 2].ToString() + ";" + res[0].ToString("F8");
                    file.WriteLine(lines[0].ToString());
                }
            MessageBox.Show("Тестирование пройдено");
        }

        private void ViewTopologyNetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new Thread(() => Application.Run(new FormDrawNeurons())).Start();
        }


    }
}
