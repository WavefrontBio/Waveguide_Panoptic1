using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace Waveguide
{
    /// <summary>
    /// Interaction logic for ManualImagerDialog.xaml
    /// </summary>
    public partial class ManualImagerDialog : Window
    {
        Imager m_imager;
        public bool m_imagerReady;

        VM_ManualImagerDialog VM;

        public ManualImagerDialog(Imager imager, bool isManualMode)
        {
            InitializeComponent();

            VM = new VM_ManualImagerDialog();

            this.DataContext = VM;

            m_imager = imager;
            VM.ManualMode = isManualMode;

            if (!VM.ManualMode) SavePB.Visibility = Visibility.Collapsed;

            m_imager.m_temperatureEvent += m_imager_m_temperatureEvent;

            m_imagerReady = false;

            if (m_imager == null)
            {
                MessageBox.Show("No Imager Set", "Imager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            if (!m_imager.IsInitialized())
            {
                MessageBox.Show("Imager Not Initialized", "Imager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            else
            {
                m_imagerReady = true;
            }
            
            RunImagerControl.SetImager(m_imager);
            RunImagerControl.SetManualMode(isManualMode);
            
            if(!isManualMode)
            {
                // automatically run Optimize
                RunImagerControl.SetFilters(RunImagerControl.VM.ExFilter, RunImagerControl.VM.EmFilter);
                //RunImagerControl.Optimize(1, 50, 5, 50, 500, 50, (double)GlobalVars.MaxPixelValue * 0.05);
            }
           
        }

        void m_imager_m_temperatureEvent(object sender, TemperatureEventArgs e)
        {
            VM.CameraTemp = e.Temperature;
        }

        public RunImager GetRunImagerControl()
        {
            return RunImagerControl;
        }

        private void OkPB_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SavePB_Click(object sender, RoutedEventArgs e)
        {
            SaveImageDialog dlg = new SaveImageDialog(RunImagerControl.GetImageBitmap(),
                                                      RunImagerControl.GetImageData(),
                                                      RunImagerControl.m_imageWidth,
                                                      RunImagerControl.m_imageHeight);

            dlg.ShowDialog();
        }
    }





    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////


    public class VM_ManualImagerDialog : INotifyPropertyChanged
    {
        private int _cameraTemp;

        private string _cameraTempString;

        private bool _manualMode;

        public int CameraTemp
        {
            get { return _cameraTemp; }
            set
            {
                _cameraTemp = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("CameraTemp"));

                // set CameraTempString
                CameraTempString = "Temp: " + _cameraTemp.ToString();
            }
        }


        public string CameraTempString
        {
            get { return _cameraTempString; }
            set
            {
                _cameraTempString = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("CameraTempString"));
            }
        }


        public bool ManualMode
        {
            get { return _manualMode; }
            set
            {
                _manualMode = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("ManualMode"));
            }
        }

        public VM_ManualImagerDialog()
        {
            _cameraTemp = 0;

            _cameraTempString = "Temp: --";

            _manualMode = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }



}
