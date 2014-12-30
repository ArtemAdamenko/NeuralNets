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
using System.Collections;
using AForge.Controls;
using AForge.Neuro;
using AForge.Neuro.Learning;


namespace Neural
{
    public partial class Form1 : Form
    {
        private double[,] data = null;
        int rowCountData = 0;
        int colCountData = 0;

        String selectedItem = "";

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
        IActivationFunction activationFunc;
        BackPropagationLearning teacherBack = null;
        PerceptronLearning teacherPerc = null;

        // Constructor
        public Form1()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // init controls
            UpdateSettings();
            errorChart.AddDataSeries("error", Color.Red, Chart.SeriesType.ConnectedDots, 3);
            errorChart.AddDataSeries("validate", Color.Blue, Chart.SeriesType.ConnectedDots, 2);
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
            this.typeNetBox.Items.Add("Регрессия");
            this.typeNetBox.Items.Add("Классификация");
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

                    //get row count values
                    String line;
                    rowCountData = 0;
                    colCountData = 0;

                    //get input and output count
                    line = reader.ReadLine();
                    rowCountData++;
                    colCountData = line.Split('-').Length;

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
                        string[] strs = line.Split('-');
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
            this.dataGridView1.Rows.Clear();
            int colCountGrid = this.dataGridView1.Columns.Count;
            for (int c = 0; c < colCountGrid; c++)
            {
                this.dataGridView1.Columns.Remove(c.ToString());
            }

            // add new records
            //add columns to grid
            int k = 0;
            for (k = 0; k < colCountData-1; k++)
            {
                this.dataGridView1.Columns.Add(k.ToString(), "Input: " + k.ToString());
            }
            this.dataGridView1.Columns.Add(k.ToString(), "Output");

            //add rows and values
            for (int i = 0; i < rowCountData; i++)
            {
                dataGridView1.Rows.Add();
                for (int j = 0; j < colCountData; j++)
                {   
                        this.dataGridView1.Rows[i].Cells[j].Value = data[i, j];
                }
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

            //get type of neural net
            selectedItem = this.typeNetBox.SelectedItem.ToString();
            if (selectedItem == "Регрессия")
            {
                neuronsAndLayers[1] = 1;
            }
            else if (selectedItem == "Классификация")
            {
                checkForClassification();
                neuronsAndLayers[1] = classesCount;
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

        //проверка входящей выборки на классы
        private void checkForClassification()
        {
            int[] tempClasses = new int[rowCountData];
            // classes count
            classesCount = 0;
            samplesPerClass = new int[10];
            try
            {
                int i = 0;
                for (i = 0; i < data.GetLength(0); i++)
                {
                    //get output as classes
                    tempClasses[i] = int.Parse(data[i, colCountData - 1].ToString());

                    if (tempClasses[i] >= 10)
                        continue;
                    
                    // count the amount of different classes
                    if (tempClasses[i] >= classesCount)
                        classesCount = tempClasses[i] + 1;
                    // count samples per class
                    samplesPerClass[tempClasses[i]]++;
                }
                classes = new int[i];
                Array.Copy(tempClasses, 0, classes, 0, i);
            }
            catch(Exception e) {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void checkForRegression()
        {
 
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

            // create multi-layer neural network

            if (selectedItem == "Классификация")
            {
                activationFunc = new ThresholdFunction();
            }
            else if (selectedItem == "Регрессия")
            {
                activationFunc = new SigmoidFunction();
            }
            
            int k = 0;
            int j = 0;

            for (int i = 1; i < samples; i++)
            {
                //80% training, 20% for validate data(to do 70% lear., 20% validate, 10% test)
                if ((i % 5) == 0) // validate input 20 %
                {                               
                    validateInput[k] = new double[colCountData-1];
                    validateOutput[k] = new double[1];

                    for (int c = 0; c < colCountData - 1; c++)
                    {
                        validateInput[k][c] = data[i, c];
                    }
                    
                    validateOutput[k][0] = data[i, colCountData-1];
                    k++;
                }
                else //forward input 80 %
                {
                    // input data
                    input[j] = new double[colCountData-1];

                    for (int c = 0; c < colCountData - 1; c++)
                    {
                        input[j][c] = data[i, c];
                    }

                    //output data
                    if (selectedItem == "Классификация")
                    {
                        output[j] = new double[classesCount];
                        output[j][classes[j]] = 1;
                    }
                    else if (selectedItem == "Регрессия")
                    {
                        output[j] = new double[1];
                        output[j][0] = data[i, colCountData - 1];
                    }
                    
                    j++;
                }
            }

            network = new ActivationNetwork(activationFunc,
            colCountData-1, neuronsAndLayers);
            // create teacher
            if (selectedItem == "Классификация")
            {
                teacherPerc = new PerceptronLearning(network);
                // set learning rate and momentum
                teacherPerc.LearningRate = learningRate;
            }
            else if (selectedItem == "Регрессия")
            {
                teacherBack = new BackPropagationLearning(network);
                // set learning rate and momentum
                teacherBack.LearningRate = learningRate;
                teacherBack.Momentum = momentum;
            }

            // iterations
            int iteration = 1;
            double error = 0.0;
            double[] validateError = new double[1]{0.0};
            //int j = 0;
            // erros list
            ArrayList errorsList = new ArrayList();
            ArrayList validateList = new ArrayList();
            // loop
            while (!needToStop)
            {

                    // run epoch of learning procedure
                    if (selectedItem == "Классификация")
                    {
                        error = teacherPerc.RunEpoch(input, output);
                    }
                    else if (selectedItem == "Регрессия")
                    {
                        error = teacherBack.RunEpoch(input, output);
                        if (errorsList.Count - 1 >= 1000)
                        {
                            errorsList.RemoveAt(0);
                            
                        }
                        errorsList.Add(error);
                        
                    }
                    //error = teacher.RunEpoch(input, output);
                    validateError[0] = 0.0;
                    for (int count = 0; count < validateInput.GetLength(0)-1; count++)
                    {
                        validateError[0] += network.Compute(validateInput[count])[0] - validateOutput[count][0];
                    }
                    if (validateList.Count - 1 >= 1000)
                    {
                        validateList.RemoveAt(0);

                    }
                    validateList.Add(validateError[0]);

                        // set current iteration's info
                    currentIterationBox.Invoke(new Action<string>((s) => currentIterationBox.Text = s), iteration.ToString());
                    errorPercent.Invoke(new Action<string>((s) => errorPercent.Text = s), error.ToString("F14"));
                    validErrorBox.Invoke(new Action<string>((s) => validErrorBox.Text = s), (validateError[0]/1000).ToString("F14"));
                    // show error's dynamics
                    double[,] errors = new double[errorsList.Count, 2];
                    double[,] valid = new double[validateList.Count, 2];

                    for (int i = 0, n = errorsList.Count; i < n; i++)
                    {
                        errors[i, 0] = i;
                        errors[i, 1] = (double)errorsList[i];
                    }

                    for (int i = 0, n = validateList.Count; i < n; i++)
                    {
                        valid[i, 0] = i;
                        valid[i, 1] = (double)validateList[i];
                    }

                    errorChart.RangeX = new Range(1, errorsList.Count - 1);
                    errorChart.UpdateDataSeries("error", errors);
                    errorChart.UpdateDataSeries("validate", valid);
    
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
