using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DAQmxAcq;
using NationalInstruments.DAQmx;
using Steema.TeeChart.Styles;
using System.Windows.Forms;

namespace Bosch
{
    public delegate void AcqCallback(List<List<TAcqData>> Data);
    public delegate void EncoderAcqCallback(List<List<TAcqData>> Data);
    public struct TAcqTestParams
    {
        public string DeviceName;
        public string TestName;
        public double DAQmxAcqSamplingFrequency;
        public double DAQmxAcqTestDuration;
        public TAcqChannels[] AcqChannels;
        public TEncoderChannels[] EncoderChannels;

        public TAcqTestParams(string DeviceName, string TestName, double DAQmxAcqSamplingFrequency, double DAQmxAcqTestDuration, TAcqChannels[] AcqChannels, TEncoderChannels[] EncoderChannels = null)
        {
            this.DeviceName = DeviceName;
            this.TestName = TestName;
            this.DAQmxAcqSamplingFrequency = DAQmxAcqSamplingFrequency;
            this.DAQmxAcqTestDuration = DAQmxAcqTestDuration;
            this.AcqChannels = AcqChannels;
            this.EncoderChannels = EncoderChannels;
        }
    };

    public partial class AcqControl : UserControl
    {

        public string DeviceName;
        public string TestName;
        public int LastPlotCount;
        public int LastMathCount;
        public int LastEncoderCount;
        //public int TotalBufferSize;
        public double DAQmxAcqSamplingFrequency;
        public double DAQmxAcqTestDuration;
        public List<TAIChannels> AIChannels;
        public List<TCIChannels> CIChannels;
        public List<TMathChannels> MathChannels;
        public List<TAcqChannels> Channels;
        public TAcqChannels[] AcqChannels;
        public TEncoderChannels[] EncoderChannels;
        public TDAQmxAcq DAQmxAcq = null;
        //contains the plot data for all channels
        FastLine[] ChartSeries = new FastLine[20];
        //Encoder Plot Data
        FastLine EncoderSeries = new FastLine();
        List<TLimitsMarker> LimitSeries = new List<TLimitsMarker>();
        List<TReferenceMarker> ReferenceSeries = new List<TReferenceMarker>();
        Points DataPoints = new Points();
        DAQCallback AnalogCallback = null;
        EncoderCallback CallbackEncoder = null;
        private AcqCallback callback;
        private EncoderAcqCallback Ecallback;
        public bool bAcq;
        public bool bRunning;
        public bool bReset;
        public List<List<TAcqData>> ListDataAcq = new List<List<TAcqData>>();
        public List<List<TAcqData>> ListDataMath = new List<List<TAcqData>>();
        public List<List<TAcqData>> ListDataAcqMA = new List<List<TAcqData>>();
        //encoder Output Data
        public List<List<TAcqData>> ListEncoderAcqData = new List<List<TAcqData>>();
        //List<List<double>> ListData = new List<List<double>>();
        //List<List<double>> ListDataMA = new List<List<double>>();
        List<List<double>> ListDataPoint = new List<List<double>>();

        TAcqTestParams AcqTestParams;


        public AcqControl()
        {
            InitializeComponent();
        }

        public bool InitDAQ(TAcqTestParams AcqTestParams, SampleQuantityMode mode = SampleQuantityMode.ContinuousSamples)
        {
            this.AcqTestParams = AcqTestParams;
            AcqGroupBox.Text = AcqTestParams.TestName;
            this.DeviceName = AcqTestParams.DeviceName;
            this.TestName = AcqTestParams.TestName;
            this.DAQmxAcqSamplingFrequency = AcqTestParams.DAQmxAcqSamplingFrequency;
            this.DAQmxAcqTestDuration = AcqTestParams.DAQmxAcqTestDuration;
            this.AcqChannels = AcqTestParams.AcqChannels;
            this.EncoderChannels = AcqTestParams.EncoderChannels;
            this.CIChannels = new List<TCIChannels>();
            this.AIChannels = new List<TAIChannels>();
            this.Channels = new List<TAcqChannels>();
            this.MathChannels = new List<TMathChannels>();
            for (int i = 0; i < AcqTestParams.AcqChannels.Length; i++)
            {
                if (!(AcqChannels[i].TAIChannel.AIChannel.Contains('+') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('-') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('/') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('*')))
                {
                    AIChannels.Add(AcqChannels[i].TAIChannel);
                    Channels.Add(AcqChannels[i]);
                }
            }

            if (AcqTestParams.EncoderChannels != null)
            {
                for (int i = 0; i < AcqTestParams.EncoderChannels.Length; i++)
                {
                    CIChannels.Add(EncoderChannels[i].TCIChannel);
                }
            }

            for (int i = 0; i < AcqTestParams.AcqChannels.Length; i++)
            {
                if (AcqChannels[i].TAIChannel.AIChannel.Contains('+') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('-') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('/') ||
                    AcqChannels[i].TAIChannel.AIChannel.Contains('*'))
                {
                    TMathChannels.MathFunction Function;
                    AcqChannels[i].TAIChannel.AIChannel = AcqChannels[i].TAIChannel.AIChannel.Replace(" ", string.Empty);
                    if (AcqChannels[i].TAIChannel.AIChannel.Contains('+'))
                    {
                        Function = TMathChannels.MathFunction.Addition;
                    }
                    else if (AcqChannels[i].TAIChannel.AIChannel.Contains('-'))
                    {
                        Function = TMathChannels.MathFunction.Subtraction;
                    }
                    else if (AcqChannels[i].TAIChannel.AIChannel.Contains('*'))
                    {
                        Function = TMathChannels.MathFunction.Multiplication;
                    }
                    else
                    {
                        Function = TMathChannels.MathFunction.Division;
                    }

                    string[] channels = AcqChannels[i].TAIChannel.AIChannel.Split('+', '-', '/', '*');
                    TMathChannels MathChannel = new TMathChannels(AcqChannels[i].TAIChannel.AIChannel,
                                                                  AcqChannels[i].TAIChannel.AIChannelName,
                                                                  Function,
                                                                  GetChannelIndex(channels[0]),
                                                                  GetChannelIndex(channels[1]));
                    MathChannels.Add(MathChannel);
                    Channels.Add(AcqChannels[i]);
                }
            }

            //this.AIChannels[0] = AcqChannels[0].TAIChannel;

            bAcq = false;
            bRunning = false;
            bReset = false;

            DAQmxAcq = new TDAQmxAcq(DeviceName, AIChannels, CIChannels, DAQmxAcqSamplingFrequency, DAQmxAcqTestDuration);


            AnalogCallback = AnalogDataReceived;
            CallbackEncoder = EncoderDataReceived;
            this.tChart1.Zoom.Animated = true;
            for (int i = 0; i < AIChannels.Count; i++)
            {
                ChartSeries[i] = new Steema.TeeChart.Styles.FastLine();
                tChart1.Series.Add(ChartSeries[i]);
                ChartSeries[i].ShowInLegend = true;
                ChartSeries[i].Title = Channels[i].name;
                ChartSeries[i].Color = Channels[i].colour;
                ChartSeries[i].Visible = Channels[i].visible;
                ListDataAcq.Add(new List<TAcqData>());
                ChartSeries[i].VertAxis = Channels[i].YAxisValue;
                dataGridViewValues.Rows.Add("■", Channels[i].name, 0 + Channels[i].units);
                dataGridViewValues[0, i].Style.ForeColor = Channels[i].colour;
            }

            for (int i = 0; i < MathChannels.Count; i++)
            {
                ChartSeries[i + AIChannels.Count] = new Steema.TeeChart.Styles.FastLine();
                tChart1.Series.Add(ChartSeries[i + AIChannels.Count]);
                ChartSeries[i + AIChannels.Count].ShowInLegend = true;
                ChartSeries[i + AIChannels.Count].Visible = Channels[i + AIChannels.Count].visible;
                ChartSeries[i + AIChannels.Count].Title = Channels[i + AIChannels.Count].name;
                ChartSeries[i + AIChannels.Count].Color = Channels[i + AIChannels.Count].colour;
                ListDataAcq.Add(new List<TAcqData>());
                ChartSeries[i + AIChannels.Count].VertAxis = Channels[i + AIChannels.Count].YAxisValue;
                dataGridViewValues.Rows.Add("■", Channels[i + AIChannels.Count].name, 0 + Channels[i + AIChannels.Count].units);
                dataGridViewValues[0, i + AIChannels.Count].Style.ForeColor = Channels[i + AIChannels.Count].colour;
            }

            for (int i = 0; i < AIChannels.Count; i++)
            {
                ChartSeries[i + AIChannels.Count + MathChannels.Count] = new Steema.TeeChart.Styles.FastLine();
                tChart1.Series.Add(ChartSeries[i + AIChannels.Count + MathChannels.Count]);
                ChartSeries[i + AIChannels.Count + MathChannels.Count].ShowInLegend = true;
                ChartSeries[i + AIChannels.Count + MathChannels.Count].Color = Color.FromArgb(Channels[i].colour.ToArgb() ^ 0xffffff);
                ChartSeries[i + AIChannels.Count + MathChannels.Count].Title = Channels[i].name + " MA (" + Channels[i].MovingAverageWindow + ")";
                ChartSeries[i + AIChannels.Count + MathChannels.Count].Visible = Channels[i].visible;
                ChartSeries[i + AIChannels.Count + MathChannels.Count].VertAxis = Channels[i].YAxisValue;
                ListDataAcqMA.Add(new List<TAcqData>());
            }

            for (int i = 0; i < MathChannels.Count; i++)
            {
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count] = new Steema.TeeChart.Styles.FastLine();
                tChart1.Series.Add(ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count]);
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].ShowInLegend = true;
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].Visible = Channels[i + AIChannels.Count].visible;
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].Title = Channels[i + AIChannels.Count].name;
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].Color = Color.FromArgb(Channels[i + AIChannels.Count].colour.ToArgb() ^ 0xffffff);
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].Title = Channels[i + AIChannels.Count].name + " MA (" + Channels[i + AIChannels.Count].MovingAverageWindow + ")";
                ChartSeries[i + (AIChannels.Count * 2) + MathChannels.Count].VertAxis = Channels[i + AIChannels.Count].YAxisValue;
                ListDataAcqMA.Add(new List<TAcqData>());
            }

            for (int i = 0; i < CIChannels.Count; i++)
            {
                EncoderSeries = new Steema.TeeChart.Styles.FastLine();
                tChart1.Series.Add(EncoderSeries);
                EncoderSeries.ShowInLegend = true;
                EncoderSeries.Visible = true;
                EncoderSeries.Color = EncoderChannels[i].colour;
                EncoderSeries.Title = EncoderChannels[i].name;
                EncoderSeries.VertAxis = EncoderChannels[i].YAxisValue;
                ListEncoderAcqData.Add(new List<TAcqData>());
            }

            DataPoints = new Steema.TeeChart.Styles.Points();
            DataPoints.GetSeriesMark += DataPoints_GetSeriesMark;
            DataPoints.Visible = true;
            tChart1.Series.Add(DataPoints);
            DataPoints.ShowInLegend = true;
            DataPoints.Title = "Data Points";
            DataPoints.VertAxis = VerticalAxis.Both;
            ListDataPoint.Add(new List<double>());

            if (DAQmxAcq.InitDAQ(mode) == false)
            {
                MessageBox.Show("Failed to initialise Dev:" + AcqTestParams.DeviceName + " DAQmxAcq");
                return false;
            }
            return true;
        }

        public bool InitDAC(string DACChannel, string DACChannelName, double min, double max, double SamplingRate, int n, SampleQuantityMode mode = SampleQuantityMode.ContinuousSamples)
        {
            if (DAQmxAcq.InitDAC(DACChannel, DACChannelName, min, max, SamplingRate, n, mode) == false)
            {
                MessageBox.Show("Failed to initialise DAC Dev:" + DACChannel + " DAQmxAcq");
                return false;
            }
            return true;
        }


        public bool StartAcq(AcqCallback callback, EncoderAcqCallback Ecallback = null)
        {
            this.callback = callback;
            this.Ecallback = Ecallback;
            bReset = false;
            for (int i = 0; i < ListDataAcq.Count; i++)
            {
                ListDataAcq[i].Clear();
                ListDataAcqMA[i].Clear();
            }
            if (EncoderChannels != null)
            {
                ListEncoderAcqData[0].Clear();

            }
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                LineSeries.line.Clear();
                LineSeries.line.Add(0, LineSeries.value);
            }
            LastPlotCount = 0;
            LastMathCount = 0;
            LastEncoderCount = 0;
            bAcq = false;

            for (int i = 0; i < ListDataAcq.Count * 2; i++)
            {
                ChartSeries[i].Clear();
            }
            EncoderSeries.Clear();
            DataPoints.Clear();

            if (AcqTestParams.EncoderChannels != null)
            {
                if (!DAQmxAcq.StartDAQ(AnalogCallback, CallbackEncoder))
                {
                    MessageBox.Show("Failed to start Dev:" + DeviceName + " Acq");
                    return false;
                }
            }
            else
            {
                if (!DAQmxAcq.StartDAQ(AnalogCallback))
                {
                    MessageBox.Show("Failed to start Dev:" + DeviceName + " Acq");
                    return false;
                }
            }
            bRunning = true;
            return true;
        }

        public bool StartWriteandAcq(double[] data, AcqCallback callback, DAQmxAcq.readyDelegate callbackDac)
        {
            this.callback = callback;
            bReset = false;
            for (int i = 0; i < ListDataAcq.Count; i++)
            {
                ListDataAcq[i].Clear();
                ListDataAcqMA[i].Clear();
            }
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                LineSeries.line.Clear();
                LineSeries.line.Add(0, LineSeries.value);
            }
            LastPlotCount = 0;
            LastMathCount = 0;

            bAcq = false;

            for (int i = 0; i < ListDataAcq.Count * 2; i++)
            {
                ChartSeries[i].Clear();
            }
            DataPoints.Clear();

            if (!DAQmxAcq.WriteWithAcq(data, AnalogCallback, callbackDac))
            {
                MessageBox.Show("Failed to start Dev:" + DeviceName + " Acq");
                return false;
            }
            bRunning = true;
            return true;
        }

        public void AddReferenceLine(string title, Color colour, VerticalAxis axis, bool bVisible = false)
        {
            FastLine line = new Steema.TeeChart.Styles.FastLine();
            TReferenceMarker Referenceline = new TReferenceMarker(line);
            ReferenceSeries.Add(Referenceline);
            tChart1.Series.Add(ReferenceSeries[ReferenceSeries.Count - 1].line);
            ReferenceSeries[ReferenceSeries.Count - 1].line.ShowInLegend = true;
            ReferenceSeries[ReferenceSeries.Count - 1].line.Title = title;
            ReferenceSeries[ReferenceSeries.Count - 1].line.Color = colour;
            ReferenceSeries[ReferenceSeries.Count - 1].line.VertAxis = axis;
            ReferenceSeries[ReferenceSeries.Count - 1].line.Visible = bVisible;
        }

        public void UpdateReferenceLine(string title, bool bVisible = false)
        {
            foreach (TReferenceMarker LineSeries in ReferenceSeries)
            {
                if (LineSeries.line.Title == title)
                {
                    LineSeries.line.Visible = bVisible;
                }
            }
        }

        public void AddXYReferenceLine(string title, double x, double y)
        {
            foreach (TReferenceMarker LineSeries in ReferenceSeries)
            {
                if (LineSeries.line.Title == title)
                {
                    LineSeries.line.Add(x, y);
                }
            }
        }

        public void ClearReferenceLine(string title)
        {
            foreach (TReferenceMarker LineSeries in ReferenceSeries)
            {
                if (LineSeries.line.Title == title)
                {
                    LineSeries.line.Clear();
                }
            }
        }

        public void AddLimitLine(string title, Color colour, VerticalAxis axis, double value, bool bVisible = false)
        {
            checkBoxLimitsVisible.Visible = true;
            checkBoxLimitsVisible.Checked = false;
            FastLine mark = new Steema.TeeChart.Styles.FastLine();
            TLimitsMarker LimitLine = new TLimitsMarker(mark, value);
            LimitSeries.Add(LimitLine);
            tChart1.Series.Add(LimitSeries[LimitSeries.Count - 1].line);
            LimitSeries[LimitSeries.Count - 1].line.ShowInLegend = false;
            LimitSeries[LimitSeries.Count - 1].line.Title = title;
            LimitSeries[LimitSeries.Count - 1].line.Color = colour;
            LimitSeries[LimitSeries.Count - 1].line.VertAxis = axis;
            LimitSeries[LimitSeries.Count - 1].line.Visible = bVisible;
        }

        public void UpdateLimitLine(string title, double value, bool bVisible = false)
        {
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                if (LineSeries.line.Title == title)
                {
                    LineSeries.line.Visible = bVisible;
                    LineSeries.value = value;
                }
            }
        }

        public bool StartAcq()
        {
            for (int i = 0; i < ListDataAcq.Count; i++)
            {
                ListDataAcq[i].Clear();
                ListDataAcqMA[i].Clear();
            }
            LastPlotCount = 0;
            LastMathCount = 0;
            LastEncoderCount = 0;
            bAcq = false;

            for (int i = 0; i < ListDataAcq.Count * 2; i++)
            {
                ChartSeries[i].Clear();
            }
            EncoderSeries.Clear();
            DataPoints.Clear();
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                LineSeries.line.Clear();
                LineSeries.line.Add(0, LineSeries.value);
            }
            if (AcqTestParams.EncoderChannels != null)
            {
                if (!DAQmxAcq.StartDAQ(AnalogCallback, CallbackEncoder))
                {
                    MessageBox.Show("Failed to start Dev:" + DeviceName + " Acq");
                    return false;
                }
            }
            else
            {
                if (!DAQmxAcq.StartDAQ(AnalogCallback))
                {
                    MessageBox.Show("Failed to start Dev:" + DeviceName + " Acq");
                    return false;
                }
            }
            bRunning = true;
            return true;
        }

        public void ResetDAQPoint()
        {
            bReset = true;
        }

        public void StopAcq()
        {
            if (bRunning)
            {
                toolStripStatus.Text = "idle";
                DAQmxAcq.StopDAQ();
                if (bAcq)
                {
                    for (int i = 0; i < ListDataAcq.Count; i++)
                    {
                        //if (AcqChannels[i].MovingAverageWindow != 0)
                        //{
                        MovingAverage(AcqChannels[i].MovingAverageWindow, ListDataAcq[0].Count - 1, i);
                        PlotMovingAverage(i);
                        //}
                    }
                    if (callback != null)
                    {
                        callback(ListDataAcqMA);
                    }
                    if (Ecallback != null)
                    {
                        Ecallback(ListEncoderAcqData);
                    }

                }
                bRunning = false;
            }
        }

        public void SetAxisTitle(string leftAxis, string bottomAxis, string rightAxis = null)
        {
            tChart1.Axes.Bottom.Labels.Text = bottomAxis;
            tChart1.Axes.Left.Labels.Text = leftAxis;
            tChart1.Axes.Right.Labels.Text = rightAxis;
        }

        public void PlotData(int channel)
        {
            for (int j = 0; j < ListDataAcq[channel].Count - 1; j++)
            {
                ChartSeries[channel].Add(ListDataAcq[channel][j].value);
            }
            LastPlotCount = ListDataAcq[channel].Count - 1;
        }

        public void PlotMovingAverage(int channel)
        {
            for (int j = 0; j < LastPlotCount; j++)
            {
                ChartSeries[channel + ListDataAcq.Count].Add(ListDataAcqMA[channel][j].value);
            }
        }

        public void PlotDataPoint(double x, double y, string text = null)
        {
            DataPoints.Add(x, y, text);
            DataPoints.Marks.Style = MarksStyles.Label;
        }

        public void PlotDataPoint(double x, double y, Color colour, string text = null)
        {
            DataPoints.Add(x, y, text, colour);
            DataPoints.Marks.Style = MarksStyles.Label;
        }

        public void PlotDataPoint(double y, string text = null)
        {
            DataPoints.Add(LastPlotCount, y, text);
            DataPoints.Marks.Style = MarksStyles.Label;
        }

        public void PlotDataPoint(double y, Color colour, string text = null)
        {
            DataPoints.Add(LastPlotCount, y, text, colour);
            DataPoints.Marks.Style = MarksStyles.Label;
        }

        private void DataPoints_GetSeriesMark(Steema.TeeChart.Styles.Series series, Steema.TeeChart.Styles.GetSeriesMarkEventArgs e)
        {
            if (e.ValueIndex > 0)
            {
                if (DataPoints.YValues[e.ValueIndex] > DataPoints.YValues[e.ValueIndex - 1])
                {
                    e.MarkText = e.MarkText + " (Up)";
                }
                else if (DataPoints.YValues[e.ValueIndex] < DataPoints.YValues[e.ValueIndex - 1])
                {
                    e.MarkText = e.MarkText + " (Down)";
                }
                else
                {
                    e.MarkText = e.MarkText + " (No Change)";
                }
            }
        }


        private void AnalogDataReceived(double[,] data)
        {
            DateTime timestamp = DateTime.Now;
            DateTime Starttimestamp = DateTime.Now;
            //int s = (int)TStations.StationCapacitorCheck;

            for (int j = 0; j < data.GetLength(0); j++)
            {
                timestamp = Starttimestamp;
                for (int k = 0; k < data.GetLength(1); k++)
                {
                    //double tempdouble = (0.0);
                    data[j, k] *= AIChannels[j].Scale;
                    //ChartSeries[j].Add(data[j, k]);

                    TAcqData dataAcq = new TAcqData(data[j, k], timestamp);
                    timestamp = timestamp.AddMilliseconds(DAQmxAcqSamplingFrequency / 1000);
                    ListDataAcq[j].Add(dataAcq);
                }
            }

            for (int i = AIChannels.Count; i < ListDataAcq.Count; i++)
            {
                int k = i - AIChannels.Count;
                for (int j = 0; j < data.GetLength(1); j++)
                {

                    if (MathChannels[k].MathConfiguration == TMathChannels.MathFunction.Addition)
                    {
                        ListDataAcq[i].Add(new TAcqData(data[MathChannels[k].Value1channelID, j] + data[MathChannels[k].Value2channelID, j], DateTime.Now));
                    }
                    else if (MathChannels[k].MathConfiguration == TMathChannels.MathFunction.Subtraction)
                    {
                        ListDataAcq[i].Add(new TAcqData(data[MathChannels[k].Value1channelID, j] - data[MathChannels[k].Value2channelID, j], DateTime.Now));
                    }
                    else if (MathChannels[k].MathConfiguration == TMathChannels.MathFunction.Division)
                    {
                        ListDataAcq[i].Add(new TAcqData(data[MathChannels[k].Value1channelID, j] / data[MathChannels[k].Value2channelID, j], DateTime.Now));
                    }
                    else
                    {
                        ListDataAcq[i].Add(new TAcqData(data[MathChannels[k].Value1channelID, j] * data[MathChannels[k].Value2channelID, j], DateTime.Now));
                    }
                }
            }
            bAcq = true;

            if (!timer1.Enabled)
            {
                toolStripStatus.Text = "sampling...";
                timer1.Enabled = true;
            }
            //bTestReady[s] = true;
        }

        //new event handler for encoder data

        private void EncoderDataReceived(double[,] data)
        {
            DateTime timestamp = DateTime.Now;
            DateTime Starttimestamp = DateTime.Now;
            //add timestamp to the acquired data baes on sampling frequency
            for (int j = 0; j < data.GetLength(0); j++)
            {
                timestamp = Starttimestamp;
                for (int k = 0; k < data.GetLength(1); k++)
                {
                    //no scale is required for encoder
                    //data[j, k] *= AIChannels[j].Scale;
                    //ChartSeries[j].Add(data[j, k]);
                    timestamp = timestamp.AddMilliseconds(DAQmxAcqSamplingFrequency / 1000);
                    ListEncoderAcqData[j].Add(new TAcqData(data[j, k], timestamp));
                    EncoderSeries.Add(LastEncoderCount + k, ListEncoderAcqData[0][LastEncoderCount + k].value);
                }
                LastEncoderCount += data.GetLength(1);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //loop all over the acq channels to plot them
            for (int i = 0; i < ListDataAcq.Count; i++)
            {
                if (!checkBoxDisablePlot.Checked)
                {
                    //plot data point by point starting from last plot count
                    for (int j = LastPlotCount; j < ListDataAcq[i].Count - 1; j++)
                    {
                        ChartSeries[i].Add(j, ListDataAcq[i][j].value);
                    }
                }
                if (ListDataAcq[i].Count > 0) dataGridViewValues[2, i].Value = ListDataAcq[i][ListDataAcq[0].Count - 1].value.ToString("#.### ") + Channels[i].units;
            }
            //plot encoder Data
            //if (EncoderChannels.Length > 0)
            //{
            //    for (int i = LastEncoderCount; i < ListEncoderAcqData.Count; i++)
            //    {
            //        EncoderSeries.Add(i, ListEncoderAcqData[0][i].value);
            //    }
            //}
            if (!checkBoxDisablePlot.Checked)
            {
                LastPlotCount = ListDataAcq[0].Count;
                //LastEncoderCount = ListEncoderAcqData[0].Count;
            }

            if (bRunning == false)
            {
                timer1.Enabled = false;
            }

            if (checkBoxLimitsVisible.Checked == true)
            {
                foreach (TLimitsMarker LineSeries in LimitSeries)
                {
                    LineSeries.line.Add(LastPlotCount, LineSeries.value);
                }
            }

            if (bReset == true)
            {
                bReset = false;
                for (int i = 0; i < ListDataAcq.Count; i++)
                {
                    ListDataAcq[i].Clear();
                    ListDataAcqMA[i].Clear();
                }
                ListEncoderAcqData[0].Clear();
                LastPlotCount = 0;
                LastMathCount = 0;

                for (int i = 0; i < ListDataAcq.Count * 2; i++)
                {
                    ChartSeries[i].Clear();
                }
                EncoderSeries.Clear();
                DataPoints.Clear();
                foreach (TLimitsMarker LineSeries in LimitSeries)
                {
                    LineSeries.line.Clear();
                    LineSeries.line.Add(0, LineSeries.value);
                }
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            // timer1.Interval = trackBar1.Value;
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void MovingAverage(int window, int Length, int channel)
        {
            int x;
            int y;

            double[] output = new double[Length];
            int midwindow = (window - 1) / 2;

            if (Length < window)
            {
                return;
            }

            if (window == 0)
            {
                for (x = 0; x <= Length; x++)
                {
                    TAcqData Data = new TAcqData(ListDataAcq[channel][x].value, ListDataAcq[channel][x].AcqTime);
                    ListDataAcqMA[channel].Add(Data);
                }
                return;
            }

            for (x = 0; x <= Length; x++)
            {
                TAcqData Data = new TAcqData(0, ListDataAcq[channel][0].AcqTime);
                ListDataAcqMA[channel].Add(Data);
            }

            for (x = 0; x < midwindow; x++)
            {
                y = 2 * x;
                while (y >= 0)
                {
                    ListDataAcqMA[channel][x].value += ListDataAcq[channel][y].value;
                    y--;
                }
                ListDataAcqMA[channel][x].value /= (2 * x + 1);
                ListDataAcqMA[channel][x].AcqTime = ListDataAcq[channel][x].AcqTime;
            }

            for (x = (Length - midwindow - 1); x < (int)Length; x++)
            {
                y = (Length - 1) - (Length - x - 1) * 2;
                while (y < (int)Length)
                {
                    ListDataAcqMA[channel][x].value += ListDataAcq[channel][y].value;
                    y++;
                }
                ListDataAcqMA[channel][x].value /= ((double)Length - x - 1) * 2 + 1;
                ListDataAcqMA[channel][x].AcqTime = ListDataAcq[channel][x].AcqTime;
            }

            for (x = midwindow; x < (int)(Length - midwindow - 1); x++)
            {
                for (y = (x - midwindow); y < (x + midwindow); y++)
                {
                    ListDataAcqMA[channel][x].value += ListDataAcq[channel][y].value;
                }
                ListDataAcqMA[channel][x].value /= window - 1;
                ListDataAcqMA[channel][x].AcqTime = ListDataAcq[channel][x].AcqTime;
            }
        }

        public bool SaveData(string filename)
        {
            string csvRow;

            if (ListDataAcq[0].Count == 0)
            {
                MessageBox.Show("No Data to Save !");
                return false;
            }
            else
            {

                try
                {
                    using (var stream = File.CreateText(filename))
                    {
                        csvRow = "Date Time , Index ,";
                        for (int i = 0; i < ListDataAcq.Count; i++)
                        {
                            csvRow += Channels[i].name + ",";
                        }
                        if (ListDataAcqMA != null)
                        {
                            for (int i = 0; i < ListDataAcq.Count; i++)
                            {
                                csvRow += Channels[i].name + " MA (" + Channels[i].MovingAverageWindow + "),";
                            }
                        }

                        stream.WriteLine(csvRow);
                        for (int k = 0; k < ListDataAcq[0].Count - 1; k++)
                        {
                            csvRow = ListDataAcq[0][k].AcqTime.ToString("mm.ss.ffff") + ", " + k.ToString() + ",";
                            for (int j = 0; j < ListDataAcq.Count; j++)
                            {
                                csvRow += ListDataAcq[j][k].value.ToString() + ",";
                            }
                            if (ListDataAcqMA != null)
                            {
                                for (int j = 0; j < ListDataAcq.Count; j++)
                                {
                                    csvRow += ListDataAcqMA[j][k].value.ToString() + ",";
                                }
                            }
                            stream.WriteLine(csvRow);
                        }
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                    return false;
                }
            }
            return true;
        }

        public bool LoadData(string filename)
        {
            try
            {
                for (int i = 0; i < ListDataAcq.Count; i++)
                {
                    ListDataAcq[i].Clear();
                    ListDataAcqMA[i].Clear();
                }
                foreach (TLimitsMarker LineSeries in LimitSeries)
                {
                    LineSeries.line.Clear();
                }
                LastPlotCount = 0;
                LastMathCount = 0;
                using (var stream = File.OpenText(filename))
                {
                    IList<string> names = stream.ReadLine().Split(',').ToList<string>();
                    for (int i = 0; i < ListDataAcq.Count; i++)
                    {
                        if (Channels[i].name != names[i + 2])
                        {
                            MessageBox.Show("Incompatible CSV Headers");
                            return false;
                        }
                    }

                    while ((names = stream.ReadLine().Split(',').ToList<string>()) != null)
                    {
                        int index = 0;
                        index = Convert.ToInt32(names[1]);
                        for (int j = 0; j < ListDataAcq.Count; j++)
                        {
                            TAcqData dataAcq = new TAcqData(Convert.ToDouble(names[j + 2]), DateTime.Now);
                            ListDataAcq[j].Add(dataAcq);
                        }
                        if (ListDataAcqMA != null)
                        {
                            for (int j = 0; j < ListDataAcq.Count; j++)
                            {
                                TAcqData dataAcq = new TAcqData(Convert.ToDouble(names[j + 2 + ListDataAcq.Count]), DateTime.Now);
                                ListDataAcqMA[j].Add(dataAcq);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (exception.HResult != -2147467261)
                {
                    MessageBox.Show(exception.Message);
                    return false;
                }
            }
            for (int i = 0; i < ListDataAcq.Count * 2; i++)
            {
                ChartSeries[i].Clear();
            }
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                LineSeries.line.Clear();
            }
            DataPoints.Clear();
            for (int j = 0; j < ListDataAcq.Count; j++)
            {
                PlotData(j);
                if (ListDataAcqMA != null)
                {
                    PlotMovingAverage(j);
                }
            }
            return true;
        }

        private void checkBoxLegendEnable_CheckedChanged(object sender, EventArgs e)
        {
            tChart1.Legend.Visible = checkBoxLegendEnable.Checked;
        }

        private void tChart1_Click(object sender, EventArgs e)
        {

        }

        private void toolStripButtonStart_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < ListDataAcq.Count; i++)
            {
                ChartSeries[i].Clear();
            }
            EncoderSeries.Clear();
            foreach (TLimitsMarker LineSeries in LimitSeries)
            {
                LineSeries.line.Clear();
                LineSeries.line.Add(0, LineSeries.value);
            }
            DataPoints.Clear();
            StartAcq();
        }

        private void toolStripButtonStop_Click(object sender, EventArgs e)
        {
            StopAcq();
        }

        private void toolStripButtonSave_Click(object sender, EventArgs e)
        {
            if (ListDataAcq[0].Count == 0)
            {
                MessageBox.Show("No Data to Save !");
            }
            else
            {
                // Displays a SaveFileDialog so the user can save the Image  
                // assigned to Button2.  
                saveFileDialog1.Filter = "CSV File |*.csv";
                saveFileDialog1.Title = "Save as CSV File";
                saveFileDialog1.ShowDialog();

                // If the file name is not an empty string open it for saving.  
                if (saveFileDialog1.FileName != "")
                {
                    SaveData(saveFileDialog1.FileName);
                }
                else
                {
                    MessageBox.Show("File not found ");
                }
            }
        }

        private void toolStripButtonLoad_Click(object sender, EventArgs e)
        {
            if (ListDataAcq[0].Count != 0)
            {
                DialogResult dialogResult = MessageBox.Show("Overwrite ?", "Information", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.No)
                {
                    return;
                }
            }

            // Displays a SaveFileDialog so the user can save the Image  
            // assigned to Button2.  
            //openFileDialog1 saveFileDialog1 = new SaveFileDialog();
            openFileDialog1.Filter = "CSV File |*.csv";
            openFileDialog1.Title = "Load data select CSV File";
            openFileDialog1.ShowDialog();

            // If the file name is not an empty string open it for saving.  
            if (openFileDialog1.FileName != "")
            {
                LoadData(openFileDialog1.FileName);
            }
            else
            {
                MessageBox.Show("File not found ");
            }
        }

        private void dataGridViewValues_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        public int GetChannelIndex(string ChannelName)
        {
            int i = 0;
            foreach (TAIChannels item in AIChannels)
            {
                if (item.AIChannel.Contains(ChannelName))
                {
                    break;
                }
                i++;
            }
            return i;
        }

        private void checkBoxLimitsVisible_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxLimitsVisible.Checked == true)
            {
                foreach (TLimitsMarker LineSeries in LimitSeries)
                {
                    LineSeries.line.Visible = true;
                    LineSeries.line.ShowInLegend = true;
                    LineSeries.line.Add(LastPlotCount, LineSeries.value);
                }
            }
            else
            {
                foreach (TLimitsMarker LineSeries in LimitSeries)
                {
                    LineSeries.line.Visible = false;
                    LineSeries.line.ShowInLegend = false;
                }
            }
        }

        private void toolStripButtonReset_Click(object sender, EventArgs e)
        {
            ResetDAQPoint();
        }
    }

    public class TAcqData
    {
        public double value;
        public DateTime AcqTime;

        public TAcqData(double value, DateTime AcqTime)
        {
            this.value = value;
            this.AcqTime = AcqTime;
        }
    }

    public class TLimitsMarker
    {
        public FastLine line;
        public double value;

        public TLimitsMarker(FastLine line, double value)
        {
            this.line = line;
            this.value = value;
        }
    }

    public class TReferenceMarker
    {
        public FastLine line;

        public TReferenceMarker(FastLine line)
        {
            this.line = line;
        }
    }
    public class TAcqChannels
    {
        public Color colour;
        public VerticalAxis YAxisValue;
        public TAIChannels TAIChannel;
        public int MovingAverageWindow;
        public string units;
        public string name;
        public bool visible;

        public TAcqChannels(string AIChannel, string AIChannelName, AITerminalConfiguration ChannelConfiguration, double MinVal, double MaxVal, AIVoltageUnits Units, double Scale, Color colour, VerticalAxis YAxisValue, string units = null, int MovingAverageWindow = 0, bool visible = true)
        {
            this.TAIChannel = new TAIChannels(AIChannel, AIChannelName, ChannelConfiguration, MinVal, MaxVal, Units, Scale);
            this.colour = colour;
            this.YAxisValue = YAxisValue;
            this.MovingAverageWindow = MovingAverageWindow;
            this.units = units;
            this.name = AIChannelName;
            this.visible = visible;
        }
    };

    public class TEncoderChannels
    {
        public Color colour;
        public VerticalAxis YAxisValue;
        public TCIChannels TCIChannel;
        public string name;

        public TEncoderChannels(string CIChannel, string CIChannelName, VerticalAxis axis, Color colour, CIEncoderDecodingType encoderType, CIEncoderZIndexPhase encoderPhase, int pulsePerRev, double zIndexValue = 0, bool zIndexEnable = false)
        {
            this.name = CIChannelName;
            this.TCIChannel = new TCIChannels(CIChannel, CIChannelName, encoderType, encoderPhase, pulsePerRev, zIndexValue, zIndexEnable);
            this.colour = colour;
            this.YAxisValue = axis;
        }
    };

    public class TMathChannels
    {
        public enum MathFunction
        {
            Addition,
            Subtraction,
            Multiplication,
            Division
        }

        public string AIChannel;
        public string MathChannelName;
        public MathFunction MathConfiguration;
        public int Value1channelID;
        public int Value2channelID;

        public TMathChannels(string AIChannel, string MathChannelName, MathFunction MathConfiguration, int Value1channelID, int Value2channelID)
        {
            this.AIChannel = AIChannel;
            this.MathChannelName = MathChannelName;
            this.MathConfiguration = MathConfiguration;
            this.Value1channelID = Value1channelID;
            this.Value2channelID = Value2channelID;
        }
    };
}
