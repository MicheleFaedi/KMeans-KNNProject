using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using Microsoft.Win32;
using System.IO;
using System.Xml;
using System.Runtime.Serialization;

namespace Kmeans
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string nameFileUsed = "KAlgoritms";
        private SyncronisedList<UIPoint> _pointsUI = new SyncronisedList<UIPoint>();

        private SyncronisedList<UICluster> _clusterUI = new SyncronisedList<UICluster>();
        const int MAXPOINT = int.MaxValue;

        private DispatcherTimer _dsp = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(40) };
        private bool _firstPlay = true;
        public MainWindow()
        {
            InitializeComponent();
            btnClusters.IsEnabled = false;
            btnPoints.IsEnabled = false;
            _dsp.Tick += Dsp_Tick;

            dtgCluster.ItemsSource = _clusterUI;
            dtgPointsTypology.ItemsSource = Enum.GetValues(typeof(Point.Type));
        }

        private void btnPoints_Click(object sender, RoutedEventArgs e)
        {

            Random rnd = new Random();
            for (int index = 0; index < int.Parse(txtNumberPoints.Text) && !(_pointsUI.Count > MAXPOINT); index++)
            {
                System.Windows.Point tmp;

                do
                {
                    tmp = GenerateRandomPoint(rnd, cnvPoints.ActualHeight * sldSpread.Value);
                    tmp.X = (int)(tmp.X + cnvPoints.ActualWidth / 2);
                    tmp.Y = (int)(tmp.Y + cnvPoints.ActualHeight / 2);

                } while (tmp.X > cnvPoints.ActualWidth || tmp.Y > cnvPoints.ActualHeight || tmp.X < 0 || tmp.Y < 0); // If the point exit to the margin of the canvas recalculate it

                if (!((tmp.X > cnvPoints.ActualWidth) || (tmp.Y > cnvPoints.ActualHeight)))
                {
                    UIPoint _new = new UIPoint(new Point(tmp.X, tmp.Y, Point.Type.KMEANS));
                    _new.AddFatherPoint(cnvPoints);
                    _pointsUI.Add(_new);

                }
                if (!_firstPlay)
                {
                    _dsp.Start();
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            _dsp.Start();
            _firstPlay = false;
        }

        private void Dsp_Tick(object sender, EventArgs e)
        {
            if ((bool)chcThread.IsChecked)
            {
                int numberThread = (int)(_pointsUI.Count / 500);

                if (numberThread > 1000)
                    numberThread = 1000;
                if (numberThread <= 0)
                    numberThread = 1;

                int rangeForThread = 1 + _pointsUI.Count / numberThread;

                ManualResetEvent[] doneEvent = new ManualResetEvent[numberThread];
                Thread[] ts = new Thread[numberThread];


                for (int index = 0, startRange = 0, endRange = rangeForThread - 1; index < numberThread; index++)
                {
                    doneEvent[index] = new ManualResetEvent(false);

                    ts[index] = new Thread(new ParameterizedThreadStart((object o) =>
                     {
                         SearchClusterRange((PointsDataCalc)o);
                     }));
                    ts[index].Start(new PointsDataCalc(doneEvent[index], startRange, endRange));
                    startRange += rangeForThread;
                    endRange += rangeForThread;
                }

                //Aspetta che i thread finiscano
                for (int index = 0; index < ts.Length; index++)
                    ts[index].Join();

                /*
                Thread t = new Thread(() =>
                {

                    PointsDataCalc[] dataCalcs = new PointsDataCalc[numberThread];
                    ManualResetEvent[] doneEvent = new ManualResetEvent[numberThread];
                    int rangeForThread = _pointsUI.Count / numberThread;

                    for (int index = 0, startRange = 0, endRange = rangeForThread; index < numberThread; index++, startRange += rangeForThread, endRange += rangeForThread)
                    {
                        doneEvent[index] = new ManualResetEvent(false);
                        dataCalcs[index] = new PointsDataCalc(doneEvent[index], startRange, endRange);

                        ThreadPool.QueueUserWorkItem(SearchClusterRange, dataCalcs[index]);
                    }
                    foreach (ManualResetEvent m in doneEvent)
                        m.WaitOne();
                });

                t.Start();
                t.Join();
                */

            }
            else
                SearchClusterRange(new PointsDataCalc(new ManualResetEvent(false), 0, _pointsUI.Count));

            bool changed = false;
            for (int index = 0; index < _clusterUI.Count; index++)
            {
                _clusterUI[index].UpdatePosition();
                //if(_clusterUI[index].) Aggiunge il father se non ce l'ha DA FARE
                if (_clusterUI[index].Element.Changing)
                {
                    changed = true;
                    _firstPlay = false;
                }
            }
            if (!changed)
                _dsp.Stop();

        }

        void SearchClusterRange(PointsDataCalc calc)
        {
            for (int index = calc.StartIndex; index < _pointsUI.Count && index <= calc.EndIndex; index++)
            {
                _pointsUI[index].SearchCluster(_clusterUI, _pointsUI);

            }
            calc.DoneEvent.Set();
        }

        private void cnvPoints_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_pointsUI.Count < MAXPOINT)
            {
                System.Windows.Point tmp = Mouse.GetPosition(cnvPoints);
                UIPoint _new;

                if ((bool)chcKMeans.IsChecked)
                    _new = new UIPoint(new Point(tmp.X, tmp.Y, Point.Type.KMEANS));
                else if ((bool)chcKNN.IsChecked)
                    _new = new UIPoint(new Point(tmp.X, tmp.Y, Point.Type.KNN));
                else
                {
                    _new = null;
                    MessageBox.Show("Scegliere se usare il Kmeans o il KNN", "Informazione", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _new.AddFatherPoint(cnvPoints);
                _pointsUI.Add(_new);

                if (!_firstPlay)
                {
                    _dsp.Start();
                }
            }
        }

        private void txtNumberPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            int a = 0;
            if (!int.TryParse(txtNumberPoints.Text, out a) || a > MAXPOINT - _pointsUI.Count || a < 1)
            {
                txtNumberPointsLbl.Foreground = Brushes.Red;
                btnPoints.IsEnabled = false;
            }
            else
            {
                txtNumberPointsLbl.Foreground = Brushes.Black;
                btnPoints.IsEnabled = true;
            }
        }

        private void txtNumberCluster_TextChanged(object sender, TextChangedEventArgs e)
        {
            int a = 0;
            if (!int.TryParse(txtNumberCluster.Text, out a) || a > 1000 || a < 1)
            {
                txtNumberClusterLbl.Foreground = Brushes.Red;
                btnClusters.IsEnabled = false;
            }
            else
            {
                txtNumberClusterLbl.Foreground = Brushes.Black;
                btnClusters.IsEnabled = true;
            }
        }

        private void btnClusters_Click(object sender, RoutedEventArgs e)
        {
            Random rnd = new Random();
            for (int index = 0; index < int.Parse(txtNumberCluster.Text); index++)
            {
                UICluster tmp = new UICluster(rnd.Next((int)cnvPoints.ActualWidth), rnd.Next((int)cnvPoints.ActualHeight), rnd);
                tmp.AddFather(cnvPoints);
                _clusterUI.Add(tmp);
            }
            if (!_firstPlay)
                _dsp.Start();
        }

        private System.Windows.Point GenerateRandomPoint(Random rnd, double radius)
        {
            double u = rnd.NextDouble();
            double v = rnd.NextDouble();

            int x = (int)(radius * Math.Sqrt(-2 * Math.Log(u)) * Math.Sin(2 * Math.PI * v));
            int y = (int)(radius * Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v));

            return new System.Windows.Point(x, y);
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _dsp.Stop();
            _firstPlay = true;
        }

        private void btnViewData_Click(object sender, RoutedEventArgs e)
        {
            _dsp.Stop();
            _firstPlay = true;
            grdData.Visibility = Visibility.Visible;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            grdData.Visibility = Visibility.Hidden;
        }

        private void chcKMeans_Checked(object sender, RoutedEventArgs e)
        {
            chcKNN.IsChecked = false;
            grdKmeansControll.IsEnabled = true;
        }

        private void chcKNN_Checked(object sender, RoutedEventArgs e)
        {
            if (_dsp.IsEnabled)
            {
                MessageBox.Show("Non è possibile usare il KNN mentre il KMeans è in esecuzione", "Errore", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            chcKMeans.IsChecked = false;
            grdKmeansControll.IsEnabled = false;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Sei sicuro di voler chiudere? Ogni progresso verrà cancellato", "Attenzione, sei sicuro?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                Close();
        }

        private void mnuSalva_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "KMeans & KNN Save file|*.xml|Tutti i file|*.*";
                sfd.DefaultExt = ".xml";
                sfd.FileName = nameFileUsed;
                if((bool)sfd.ShowDialog())
                {
                    nameFileUsed = System.IO.Path.GetFileName(sfd.FileName);
                    using (FileStream saveStream = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        XmlWriterSettings xws = new XmlWriterSettings();
                        xws.Indent = true;
                        using (XmlWriter xmlWriter = XmlWriter.Create(saveStream, xws))
                        {
                            DataContractSerializer dcSerializer = new DataContractSerializer(typeof(SyncronisedList<UICluster>));
                            dcSerializer.WriteObject(xmlWriter, _clusterUI);
                        }
                    }
                }
            }
            catch(Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void mnuSalvaCome_Click(object sender, RoutedEventArgs e)
        {

        }

        private void mnuApri_Click(object sender, RoutedEventArgs e)
        {
            _pointsUI = new SyncronisedList<UIPoint>();
            _clusterUI = new SyncronisedList<UICluster>();
            try
            {
                OpenFileDialog odf = new OpenFileDialog();
                odf.DefaultExt = ".xml";
                odf.FileName = nameFileUsed;
                if((bool)odf.ShowDialog())
                {
                    nameFileUsed = System.IO.Path.GetFileName(odf.FileName);
                    using (FileStream openStream = new FileStream(odf.FileName, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        using (XmlReader xmlReader = XmlReader.Create(openStream))
                        {
                            DataContractSerializer dcSerializer = new DataContractSerializer(typeof(SyncronisedList<UICluster>));
                            _clusterUI= (SyncronisedList<UICluster>)dcSerializer.ReadObject(xmlReader);
                        }
                    }
                    Random rnd = new Random();
                    for(int index=0; index< _clusterUI.Count; index++)
                    {
                        _clusterUI[index].Init(cnvPoints, rnd);
                        for(int index1=0; index1< _clusterUI[index].Element.Points.Count; index1++)
                        {
                            _pointsUI.Add(_clusterUI[index].Element.Points[index1].Element);
                        }
                    }
                }
            }
            catch(Exception error)
            { MessageBox.Show(error.Message); }
        }

        private void dtgPoint_AddingNewItem(object sender, AddingNewItemEventArgs e)
        {
            dtgCluster.Items.Refresh();
        }

        private void dtgPoint_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            ((Point)e.Row.Item).Member = ((UICluster)dtgCluster.SelectedItem).Element;
            if (!((Point)e.Row.Item).Element.VisiblePoint)
            {
                ((Point)e.Row.Item).Element.AddFatherPoint(cnvPoints);
            }
            if (!((Point)e.Row.Item).Element.VisibleLine)
                ((Point)e.Row.Item).Element.AddFatherLine(cnvPoints);
        }

        private void dtgCluster_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if(!((UICluster)e.Row.Item).IsVisible)
            {
                ((UICluster)e.Row.Item).AddFather(cnvPoints);
            }
        }
    }
}
