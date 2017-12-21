using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
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

namespace Waveguide
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        Imager m_imager;
        MainWindowViewModel VM;
        WaveguideDB m_wgDB;
        EnclosureCameraViewer m_enclosureCameraViewer;

        public MainWindow()
        {
            InitializeComponent();

            m_wgDB = new WaveguideDB();
            m_enclosureCameraViewer = null;

            //GlobalVars.UserID = 1;  // should get from login

            switch (GlobalVars.UserRole)
            {
                case GlobalVars.USER_ROLE_ENUM.ADMIN:
                    break;
                case GlobalVars.USER_ROLE_ENUM.USER:
                    UsersTab.Visibility = Visibility.Collapsed;
                    FiltersTab.Visibility = Visibility.Collapsed;
                    PlateTypesTab.Visibility = Visibility.Collapsed;
                    MaintenanceTab.Visibility = Visibility.Collapsed;
                    break;
                case GlobalVars.USER_ROLE_ENUM.OPERATOR:
                    MethodsTab.Visibility = Visibility.Collapsed;
                    ProjectsTab.Visibility = Visibility.Collapsed;
                    UsersTab.Visibility = Visibility.Collapsed;
                    FiltersTab.Visibility = Visibility.Collapsed;
                    PlateTypesTab.Visibility = Visibility.Collapsed;
                    MaintenanceTab.Visibility = Visibility.Collapsed;
                    break;
            }
            
           
        
            m_imager = null;

            this.Title = "Waveguide     " + GlobalVars.UserDisplayName + "  (" + GlobalVars.UserRole.ToString() + ")";
            

            VM = new MainWindowViewModel();

            this.DataContext = VM;

            // catch close event caused by clicking X button
            this.Closing += new System.ComponentModel.CancelEventHandler(Window_Closing);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
           
            m_imager = new Imager();
            m_imager.m_cameraEvent += Imager_CameraEvent;
            m_imager.m_temperatureEvent += Imager_TemperatureEvent;

            bool success = m_imager.Initialize();

            if (success)
            {

                m_imager.m_camera.CoolerON(true);
                VM.CoolingOn = true;
                TempOnIndicator.Fill = new SolidColorBrush(Colors.Blue);

                MyExperimentConfigurator.SetImager(m_imager);
            }
            else
            {
                MessageBox.Show("Imager Failed to Initialize.  Imager operation disabled.", "Imager Error",MessageBoxButton.OK, MessageBoxImage.Error);

                Dispatcher.BeginInvoke((Action)(() => MainTabControl.SelectedIndex = 1));
                ExperimentConfiguratorTab.IsEnabled = false;
                MaintenanceTab.IsEnabled = false;
            }
        }




        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // catch closing event caused by hitting X button if experiment is running

            MessageBoxResult result = MessageBox.Show("Are you sure you want to Logout?", "Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }

            if(m_imager!=null)
            {
                m_imager.m_camera.CoolerON(false);
            }


            // set this if you want to cancel the close event: e.Cancel = true;
        }




        void Imager_TemperatureEvent(object sender, TemperatureEventArgs e)
        {
            if (this.Dispatcher.CheckAccess())
            {
                if (e.GoodReading)
                {
                    VM.CameraTemp = e.Temperature;
                    VM.CameraTempString = e.Temperature.ToString();
                }
                else
                {
                    VM.CameraTempString = "-";
                }
            }
            else
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (e.GoodReading)
                        {
                            VM.CameraTemp = e.Temperature;
                            VM.CameraTempString = e.Temperature.ToString();
                        }
                        else
                        {
                            VM.CameraTempString = "-";
                        }
                    }));
            }
        }

        void Imager_CameraEvent(object sender, CameraEventArgs e)
        {
            if (this.Dispatcher.CheckAccess())
            {
               PostMessage(e.Message);
            }
            else
            {
                this.Dispatcher.BeginInvoke(new Action(() => PostMessage(e.Message)));
            }
        }

   
        public void PostMessage(string msg)
        {
            MainMessageWindow.AppendText(Environment.NewLine);
            MainMessageWindow.AppendText(msg);
            MainMessageWindow.ScrollToEnd();
        }


        private void Imager_Click(object sender, RoutedEventArgs e)
        {            
            ManualImagerDialog dlg = new ManualImagerDialog(m_imager,true);

            RunImager rc = dlg.GetRunImagerControl();

            WaveguideDB wgDB = new WaveguideDB();
                                           
            rc.SetImager(m_imager);
            
            rc.SetBinning(1,1);
            rc.SetFilters(rc.VM.ExFilter,rc.VM.EmFilter);
            rc.SetIndicatorName("Manual Imaging");
            rc.SetROI(rc.VM.RoiX, rc.VM.RoiY, rc.VM.RoiW, rc.VM.RoiH);

            dlg.ShowDialog();
        }

        private void RunExperimentPB_Click(object sender, RoutedEventArgs e)
        {
            
            TaskScheduler uiTask = TaskScheduler.FromCurrentSynchronizationContext();

            RunExperiment runWindow = new RunExperiment(m_imager,uiTask);
            ObservableCollection<ExperimentIndicatorContainer> indicators = 
                new ObservableCollection<ExperimentIndicatorContainer>();


            ExperimentIndicatorContainer indicator = new ExperimentIndicatorContainer();
            indicator.Description = "Test Indicator";
            indicator.EmissionFilterDesc = "Em Filt";
            indicator.EmissionFilterPos = 6;
            indicator.ExcitationFilterDesc = "Ex Filt";
            indicator.ExcitationFilterPos = 4;
            indicator.ExperimentID = 1;
            indicator.ExperimentIndicatorID = 5; // made this number up
            indicator.Exposure = 150;
            indicator.Gain = 5;
            indicator.MaskID = 7;
            indicator.SignalType = SIGNAL_TYPE.UP;
            indicator.Verified = true;
            indicators.Add(indicator);

            int hBinning = 1, vBinning = 1;

            runWindow.ChartArray.BuildChartArray(16, 24, hBinning, vBinning,  indicators);
            
            foreach(ExperimentIndicatorContainer ind in indicators)
            {
                runWindow.ChartArray.DrawXonImage(ind.ExperimentIndicatorID);
            }

            runWindow.ChartArray.AddEventMarker(100, "Event Marker 1");

            runWindow.ShowDialog();
        }

        private void TempOnIndicator_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            VM.CoolingOn = !VM.CoolingOn;
            m_imager.m_camera.CoolerON(VM.CoolingOn);
            if (VM.CoolingOn)
            {
                TempOnIndicator.Fill = new SolidColorBrush(Colors.Blue);
                PostMessage("Camera Cooler ON");
            }
            else
            {
                TempOnIndicator.Fill = new SolidColorBrush(Colors.Red);
                PostMessage("Camera Cooler OFF");
            }
            
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            // turn off camera cooler
            if(m_imager!=null)
                m_imager.m_camera.CoolerON(false);
        }

        private void LogoutPB_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ViewEnclosureCameraPB_Click(object sender, RoutedEventArgs e)
        {
            if (m_enclosureCameraViewer == null)
            {
                m_enclosureCameraViewer = new EnclosureCameraViewer();
                m_enclosureCameraViewer.Closed += m_enclosureCameraViewer_Closed;
                m_enclosureCameraViewer.Show();
            }
            else
            {
                // need code here to bring Enclosure Camera Viewer window to front
            }
 
        }

        void m_enclosureCameraViewer_Closed(object sender, EventArgs e)
        {
            m_enclosureCameraViewer = null;
        }


    }


    // //////////////////////////////////////////////////////////////////////
    // //////////////////////////////////////////////////////////////////////
    // //////////////////////////////////////////////////////////////////////



    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private int _cameraTemp;
        private string _cameraTempString;
        private bool _coolingOn;

        public int CameraTemp
        {
            get { return _cameraTemp; }
            set { _cameraTemp = value; NotifyPropertyChanged("CameraTemp"); }
        }

        public string CameraTempString
        {
            get { return _cameraTempString; }
            set { _cameraTempString = value; NotifyPropertyChanged("CameraTempString"); }
        }

        public bool CoolingOn
        {
            get { return _coolingOn; }
            set { _coolingOn = value; NotifyPropertyChanged("CoolingOn"); }
        }

        public MainWindowViewModel()
        {
            CameraTempString = "-";
            CoolingOn = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }

    }
}
