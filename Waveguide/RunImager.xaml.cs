using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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

namespace Waveguide
{

    public delegate void StoppedImagingHandler(object sender, EventArgs e);

    /// <summary>
    /// Interaction logic for RunImager.xaml
    /// </summary>
    public partial class RunImager : UserControl
    {
        TaskScheduler m_uiTask;
        ColorModel m_colorModel;
        RangeClass m_range;
        CancellationTokenSource m_tokenSource;
        CancellationToken m_cancelToken;
        WriteableBitmap m_bitmap;
        public VM_RunImager VM;
        MaskContainer m_mask;
        ushort[] m_grayImage;

        public int m_imageWidth;
        public int m_imageHeight;

        Imager m_imager;

        ImagingParameters m_iParams;

        Histogram m_histogram;

        WaveguideDB wgDB;

        public event StoppedImagingHandler ImagingStopped;



         ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> m_displayPipeline;

         ITargetBlock<Tuple<ExperimentImageContainer, ushort[]>> m_storagePipeline;

         ITargetBlock<ushort[]> m_histogramPipeline;

        

        

        public RunImager()
        {
            InitializeComponent();

            m_imager = null;

            VM = new VM_RunImager("Ready");

            this.DataContext = VM;

            m_range = new RangeClass();

            wgDB = new WaveguideDB();

            m_histogram = new Histogram();

            bool success = wgDB.GetDefaultColorModel(out m_colorModel, GlobalVars.MaxPixelValue, 1024);

            if (!success)
            {
                // setup default color model
                m_colorModel = new ColorModel("Default");
                m_colorModel.InsertColorStop(0, 0, 0, 0);
                m_colorModel.InsertColorStop(1023, 255, 255, 255);                               
            }

            // make sure that control points 1 and 2 match up with the thumb controls
            // on the slider
            m_colorModel.m_controlPts[1].m_value = 0;
            m_colorModel.m_controlPts[1].m_colorIndex = 0;
            m_colorModel.m_controlPts[2].m_value = GlobalVars.MaxPixelValue;
            m_colorModel.m_controlPts[2].m_colorIndex = 1023; 

            m_colorModel.BuildColorGradient();
            m_colorModel.BuildColorMap();


            RangeSlider.DataContext = m_range;
            m_range.RangeMin = 0;
            m_range.RangeMax = 100;

            HistogramImage.Source = m_histogram.GetHistogramBitmap();
            
        }

     

        public void SetImager(Imager imager)
        {
            m_imager = imager;
            m_imager.m_cameraEvent += Imager_CameraEvent;
            m_imager.m_temperatureEvent += m_imager_temperatureEvent;
            LoadFilters();

            // set defaults for imaging area;
            VM.RoiX = 1;
            VM.RoiY = 1;
            VM.RoiW = m_imager.m_camera.XPixels;
            VM.RoiH = m_imager.m_camera.YPixels;

            ImageDisplayControl.Init(GlobalVars.PixelWidth/VM.HorzBinning, GlobalVars.PixelHeight/VM.VertBinning, 
                GlobalVars.MaxPixelValue,m_colorModel.m_colorMap);
        }

        public void SetMask(MaskContainer mask)
        {
            m_mask = mask;
            m_histogram.SetMask(m_mask, VM.HorzBinning, VM.VertBinning);
        }

        void m_imager_temperatureEvent(object sender, TemperatureEventArgs e)
        {
            
        }

        public void SetROI(int x, int y, int w, int h)
        {
            if (x > 0 && x <= m_imager.m_camera.XPixels) VM.RoiX = x; else VM.RoiX = 1;
            if (y > 0 && y <= m_imager.m_camera.YPixels) VM.RoiY = y; else VM.RoiY = 1;

            if ((VM.RoiX + w - 1) <= m_imager.m_camera.XPixels) VM.RoiW = w; else VM.RoiW = m_imager.m_camera.XPixels - VM.RoiX + 1;
            if ((VM.RoiY + h - 1) <= m_imager.m_camera.YPixels) VM.RoiH = h; else VM.RoiH = m_imager.m_camera.YPixels - VM.RoiY + 1;
        }

        public void SetFilters(FilterContainer ExcitationFilter, FilterContainer EmissionFilter)
        {
            SetExcitationFilter(ExcitationFilter);
            SetEmissionFilter(EmissionFilter);
        }

        public void SetBinning(int horzBinning, int vertBinning)
        {
            VM.HorzBinning = horzBinning;
            VM.VertBinning = vertBinning;

            if (horzBinning == 1 && vertBinning == 1) Binning_1x1.IsChecked = true;
            else if (horzBinning == 2 && vertBinning == 2) Binning_2x2.IsChecked = true;
            else if (horzBinning == 4 && vertBinning == 4) Binning_4x4.IsChecked = true;
            else if (horzBinning == 8 && vertBinning == 8) Binning_8x8.IsChecked = true;
        }

        public void SetIndicatorName(string indicatorName)
        {
            VM.IndicatorName = indicatorName;
        }

        public void SetGain(int gain)
        {
            VM.Gain = gain;
        }

        public void SetExposure(int exposure)
        {
            VM.Exposure = exposure;
        }


        public void LoadFilters()
        {
            bool success = wgDB.GetAllExcitationFilters();
            if (success)
            {
                VM.ExFilterList.Clear();
                for (int i = 0; i < wgDB.m_filterList.Count; i++)
                {
                    VM.ExFilterList.Add(wgDB.m_filterList[i]);
                }

                if (VM.ExFilterList.Count > 0) 
                    VM.ExFilter = VM.ExFilterList[0];
            }

            success = wgDB.GetAllEmissionFilters();
            if (success)
            {
                VM.EmFilterList.Clear();
                for (int i = 0; i < wgDB.m_filterList.Count; i++)
                {
                    VM.EmFilterList.Add(wgDB.m_filterList[i]);
                }

                if (VM.EmFilterList.Count > 0)
                    VM.EmFilter = VM.EmFilterList[0];
            }            
        }


        protected virtual void OnImagingStopped(EventArgs e)
        {
            if (ImagingStopped != null) ImagingStopped(this, e);
        }


        public void BuildPipelines()
        {
            m_uiTask = TaskScheduler.FromCurrentSynchronizationContext();

            Dictionary<int,ImageDisplay> idDictionary = new Dictionary<int,ImageDisplay>();
            idDictionary.Add(0, ImageDisplayControl);

            m_displayPipeline = m_imager.CreateDisplayPipeline(m_uiTask, idDictionary);
           
            m_histogramPipeline = m_imager.CreateHistogramPipeline(m_uiTask, m_histogram);
        }





        public async void TakeSnapShot()
        {
            if (m_imager == null) return;

            if (m_displayPipeline == null) BuildPipelines();


            SnapShotPB.Content = "Cancel";
            VideoPB.IsEnabled = false;
            GainSpin.IsEnabled = false;
            ExposureSpin.IsEnabled = false;
            Optimize1PB.IsEnabled = false;            

            m_tokenSource = new CancellationTokenSource();
            m_cancelToken = m_tokenSource.Token;

            VM.StatusString = "Imaging";

            ImageDisplayControl.SetImageSize(VM.RoiW/VM.HorzBinning,VM.RoiH/VM.VertBinning,GlobalVars.MaxPixelValue);


            m_grayImage = null;


            //Task ImagingTask = Task.Factory.StartNew(() => m_imager.AcquireImage(VM.Exposure, VM.Gain, VM.HorzBinning, VM.VertBinning, out grayImage)
            //                                                                     , m_cancelToken);



            Task ImagingTask = Task.Factory.StartNew(() => m_imager.TakeSnapShot(out m_grayImage, VM.Exposure, VM.Gain, VM.HorzBinning, VM.VertBinning,
                                                                                 VM.ExFilter.PositionNumber, VM.EmFilter.PositionNumber)
                                                                                 , m_cancelToken);

            try
            {
                await ImagingTask;

                // display image
                if (m_grayImage != null)
                {
                    m_imageWidth = VM.RoiW / VM.HorzBinning;
                    m_imageHeight = VM.RoiH / VM.VertBinning;

                    m_displayPipeline.Post(Tuple.Create(m_grayImage, 0, m_imageWidth, m_imageHeight, 
                        m_colorModel.m_colorMap));
                    m_histogramPipeline.Post(m_grayImage);
                }
            }
            catch (AggregateException aggEx)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Exception(s) occurred: ");
                foreach (Exception ex in aggEx.InnerExceptions)
                {
                    sb.Append(ex.Message);
                    sb.Append(", ");
                }
                MessageBoxResult result = MessageBox.Show(sb.ToString(), "Exception(s) Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (OperationCanceledException)
            {
                VM.StatusString = "Canceled";
            }
            catch (Exception ex)
            {
                MessageBoxResult result = MessageBox.Show(ex.Message, "Unknown Exception Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImagingTask.Dispose();
                OnImagingStopped(EventArgs.Empty);

                VideoPB.Content = "Video";
                SnapShotPB.Content = "Snap Shot";
                VideoPB.IsEnabled = true;
                SnapShotPB.IsEnabled = true;
                GainSpin.IsEnabled = true;
                ExposureSpin.IsEnabled = true;
                Optimize1PB.IsEnabled = true;

                //ExcitationFilterCombo.IsEnabled = true;
                //EmissionFilterCombo.IsEnabled = true;

                VM.IsImaging = false;
                VM.StatusString = "Ready";
            }

                 
        }



        public async void StartVideo(int numberOfImagesToTake)
        {
            if (m_imager == null) return;

            VM.StatusString = "Imaging";

            if (numberOfImagesToTake == 1)
            {
                SnapShotPB.Content = "Cancel";
                VideoPB.IsEnabled = false;
            }
            else
            {
                SnapShotPB.IsEnabled = false;
                VideoPB.Content = "Cancel";
            }

 
            GainSpin.IsEnabled = false;
            ExposureSpin.IsEnabled = false;
            Optimize1PB.IsEnabled = false;

            //ExcitationFilterCombo.IsEnabled = false;
            //EmissionFilterCombo.IsEnabled = false;


            m_uiTask = TaskScheduler.FromCurrentSynchronizationContext();

            m_iParams = new ImagingParameters();
            m_iParams.NumIndicators = 1;
            m_iParams.NumImages = numberOfImagesToTake;
            m_iParams.Exposure[0] = ((float)VM.Exposure) / 1000;
            m_iParams.CycleTime[0] = VM.Exposure + 250;
            m_iParams.ExcitationFilter[0] = (byte)VM.ExFilter.PositionNumber;
            m_iParams.EmissionFilter[0] = (byte)VM.EmFilter.PositionNumber;
            m_iParams.ExcitationFilterChangeSpeed = 5;
            m_iParams.EmissionFilterChangeSpeed = 5;
            m_iParams.Gain[0] = VM.Gain;
            m_iParams.HorzBinning = VM.HorzBinning;
            m_iParams.VertBinning = VM.VertBinning;
            m_iParams.LampShutterIsOpen[0] = true;
            m_iParams.LightIntensity = 100;
            m_iParams.SyncExcitationFilterWithImaging = true;

            m_iParams.Image_StartCol = VM.RoiX;
            m_iParams.Image_EndCol = VM.RoiX + VM.RoiW - 1;
            m_iParams.Image_StartRow = VM.RoiY;
            m_iParams.Image_EndRow = VM.RoiY + VM.RoiH - 1;

            m_iParams.imageWidth = VM.RoiW;
            m_iParams.imageHeight = VM.RoiH;
            m_iParams.maxPixelValue = (2 ^ m_imager.m_camera.BitDepth) - 1;

            m_iParams.ExperimentIndicatorID[0] = 0;  // 0 here indicates that images will not be saved
            m_iParams.IndicatorName[0] = VM.IndicatorName;

            m_tokenSource = new CancellationTokenSource();
            m_cancelToken = m_tokenSource.Token;

            Progress<int> progress = new Progress<int>();
            progress.ProgressChanged += (sender1, num) =>
            {
                VM.StatusString = "Completed " + num.ToString() + " images";
            };


            ImageDisplayControl.SetImageSize(m_iParams.imageWidth, m_iParams.imageHeight, m_iParams.maxPixelValue);


            m_bitmap = ImageDisplayControl.m_imageBitmap;


            Task ImagingTask = Task.Factory.StartNew(() => m_imager.StartImaging(m_iParams,
                null,  // no analysis parameters needed here
                m_cancelToken, m_uiTask, progress, 
                ImageDisplayControl, m_colorModel.m_colorMap,
                m_histogram), m_cancelToken);


            try
            {
                await ImagingTask;
            }
            catch (AggregateException aggEx)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Exception(s) occurred: ");
                foreach (Exception ex in aggEx.InnerExceptions)
                {
                    sb.Append(ex.Message);
                    sb.Append(", ");
                }
                MessageBoxResult result = MessageBox.Show(sb.ToString(), "Exception(s) Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (OperationCanceledException)
            {
                VM.StatusString = "Canceled";
            }
            catch (Exception ex)
            {
                MessageBoxResult result = MessageBox.Show(ex.Message, "Unknown Exception Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImagingTask.Dispose();
                OnImagingStopped(EventArgs.Empty);
                
                VideoPB.Content = "Video";
                SnapShotPB.Content = "Snap Shot";
                VideoPB.IsEnabled = true;
                SnapShotPB.IsEnabled = true;
                GainSpin.IsEnabled = true;
                ExposureSpin.IsEnabled = true;
                Optimize1PB.IsEnabled = true;

                //ExcitationFilterCombo.IsEnabled = true;
                //EmissionFilterCombo.IsEnabled = true;

                VM.IsImaging = false;
                VM.StatusString = "Ready";
            }

        }

        

        void Imager_CameraEvent(object sender, CameraEventArgs e)
        {
            //MessageBox.Show(e.Message, "Camera: ", MessageBoxButton.OK, MessageBoxImage.Error);

            VM.IsImaging = e.CameraImaging;

            if (VM.IsImaging)
            {                
                 Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(
                delegate()
                {
                    StopPB.Visibility = Visibility.Visible;
                    SnapShotPB.Visibility = Visibility.Collapsed;
                    VideoPB.Visibility = Visibility.Collapsed;
                }
                ));
            }
            else
            {
                 Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(
                delegate()
                {
                    StopPB.Visibility = Visibility.Collapsed;
                    SnapShotPB.Visibility = Visibility.Visible;
                    VideoPB.Visibility = Visibility.Visible;
                }
                ));
            }
        }



        public void StopImaging()
        {
            m_tokenSource.Cancel();
        }

        public ushort[] GetImageData()
        {
            return m_grayImage;
        }

        public WriteableBitmap GetImageBitmap()
        {
            return ImageDisplayControl.m_imageBitmap;
        }


        private void RangeMinThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            m_colorModel.m_controlPts[1].m_value = (int)RangeMinThumb.Value;
            m_colorModel.m_controlPts[1].m_colorIndex = 0;
            m_colorModel.BuildColorMap();

            if (ImageDisplayControl.IsReady())// && ImageDisplayControl.HasImage())
            {
                ImageDisplayControl.SetColorMap(m_colorModel.m_colorMap);
                ImageDisplayControl.UpdateImage();
            }
        }


        private void RangeMaxThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            m_colorModel.m_controlPts[2].m_value = (int)RangeMaxThumb.Value;
            m_colorModel.m_controlPts[2].m_colorIndex = 1023;
            m_colorModel.BuildColorMap();

            if (ImageDisplayControl.IsReady())// && ImageDisplayControl.HasImage())
            {
                ImageDisplayControl.SetColorMap(m_colorModel.m_colorMap);
                ImageDisplayControl.UpdateImage();
            }
        }

        private void RangeSlider_TrackFillDragCompleted(object sender, Infragistics.Controls.Editors.TrackFillChangedEventArgs<double> e)
        {
            m_colorModel.m_controlPts[1].m_value = (int)RangeMinThumb.Value;
            m_colorModel.m_controlPts[1].m_colorIndex = 0;
            m_colorModel.m_controlPts[2].m_value = (int)RangeMaxThumb.Value;
            m_colorModel.m_controlPts[2].m_colorIndex = 1023;
            m_colorModel.BuildColorMap();

            if (ImageDisplayControl.IsReady())// && ImageDisplayControl.HasImage())
            {
                ImageDisplayControl.SetColorMap(m_colorModel.m_colorMap);
                ImageDisplayControl.UpdateImage();
            }
        }

        private void ExcitationFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int position = ((FilterContainer)((ComboBox)sender).SelectedItem).PositionNumber;

            m_imager.SetExcitationFilter(position);
        }

        private void EmissionFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int position = ((FilterContainer)((ComboBox)sender).SelectedItem).PositionNumber;

            m_imager.SetEmissionFilter(position);
        }

        private void SnapShotPB_Click(object sender, RoutedEventArgs e)
        {
            //StartVideo(1);            
            TakeSnapShot();
        }

        private void VideoPB_Click(object sender, RoutedEventArgs e)
        {
            string str = VideoPB.Content.ToString();
            string str1 = @"Cancel";
            bool cancelMode = str.Equals(str1, StringComparison.OrdinalIgnoreCase);
            if(cancelMode)
            {
                m_tokenSource.Cancel();
            }
            else
            {
                StartVideo(1000000);                      
            }
            
        }

        private void StopPB_Click(object sender, RoutedEventArgs e)
        {
            StopImaging();
        }

        private async void Optimize1PB_Click(object sender, RoutedEventArgs e)
        {
            int maxGain = 50;
            int startGain = VM.Gain;
            int gainStep = 5;

            int maxExposure = 500;
            int startExposure = VM.Exposure;
            int exposureStep = 25;

            double targetBrightness = GlobalVars.MaxPixelValue * 0.05;

            SnapShotPB.IsEnabled = false;
            VideoPB.Content = "Cancel";
            GainSpin.IsEnabled = false;
            ExposureSpin.IsEnabled = false;
            Optimize1PB.IsEnabled = false;


            m_tokenSource = new CancellationTokenSource();
            m_cancelToken = m_tokenSource.Token;

            try
            {
                var progressIndicator = new Progress<Tuple<int,int,double,double>>(ReportOptimizationProgress);

                Tuple<bool, int, int, double> result = await m_imager.OptimizeGainExposure(startGain, startExposure, targetBrightness, gainStep, exposureStep,
                                                                                  VM.HorzBinning, VM.VertBinning,
                                                                                  maxGain, maxExposure, VM.ExFilter.PositionNumber, VM.EmFilter.PositionNumber,
                                                                                  m_mask, m_cancelToken,progressIndicator);

                bool success = result.Item1;
                int gain = result.Item2;
                int exposure = result.Item3;
                double brightness = result.Item4;

                if (success)
                {
                    VM.Gain = gain;
                    VM.Exposure = exposure;
                }
                else
                {
                    MessageBox.Show("Target Brightness not possible.  Best results at: " +
                        "Gain = " + gain.ToString() + "  Exposure = " + exposure.ToString() +
                        "   gave average brightness = " + brightness.ToString());
                }
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                SnapShotPB.IsEnabled = true;
                SnapShotPB.Content = "Snap Shot";
                VideoPB.Content = "Video";
                GainSpin.IsEnabled = true;
                ExposureSpin.IsEnabled = true;
                Optimize1PB.IsEnabled = true;

                //StartVideo(1);
                TakeSnapShot();

                VM.StatusString = "Ready";
            }
        }


        void ReportOptimizationProgress(Tuple<int,int,double,double> data)
        {
            int gain = data.Item1;
            int exposure = data.Item2;
            double brightness = data.Item3;
            double target = data.Item4;

            VM.StatusString = string.Format("Gain: {0:###}   Exposure: {1:#####}   Brightness: {2:####.#}   Target: {3:####.#}",
                gain, exposure, brightness, target);
        }


        public async void Optimize(int startGain, int maxGain, int gainStep,
                                    int startExposure, int maxExposure, int exposureStep,
                                    double targetBrightness)
        {
           // Optimize1PB_Click(null,null);

            SnapShotPB.IsEnabled = false;
            VideoPB.Content = "Cancel";
            GainSpin.IsEnabled = false;
            ExposureSpin.IsEnabled = false;
            Optimize1PB.IsEnabled = false;


            m_tokenSource = new CancellationTokenSource();
            m_cancelToken = m_tokenSource.Token;

            try
            {

                var progressIndicator = new Progress<Tuple<int, int, double, double>>(ReportOptimizationProgress);

                Tuple<bool, int, int, double> result = await m_imager.OptimizeGainExposure(startGain, startExposure, targetBrightness, gainStep, exposureStep,
                                                                                  VM.HorzBinning, VM.VertBinning,
                                                                                  maxGain, maxExposure, VM.ExFilter.PositionNumber, VM.EmFilter.PositionNumber,
                                                                                  m_mask, m_cancelToken, progressIndicator);

                bool success = result.Item1;
                int gain = result.Item2;
                int exposure = result.Item3;
                double brightness = result.Item4;

                if (success)
                {
                    VM.Gain = gain;
                    VM.Exposure = exposure;
                }
                else
                {
                    MessageBox.Show("Target Brightness not possible.  Best results at: " +
                        "Gain = " + gain.ToString() + "  Exposure = " + exposure.ToString() +
                        "   gave average brightness = " + brightness.ToString());
                }
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                SnapShotPB.IsEnabled = true;
                SnapShotPB.Content = "Snap Shot";
                VideoPB.Content = "Video";
                GainSpin.IsEnabled = true;
                ExposureSpin.IsEnabled = true;
                Optimize1PB.IsEnabled = true;

                StartVideo(1);
                
            }
        }


  

        public void SetEmissionFilter(FilterContainer filter)
        {
            foreach(FilterContainer fc in VM.EmFilterList)
            {
                if(filter.FilterID == fc.FilterID)
                {
                    VM.EmFilter = fc;
                    m_imager.SetEmissionFilter(fc.PositionNumber);
                    break;
                }
            }
        }


        public void SetExcitationFilter(FilterContainer filter)
        {
            foreach (FilterContainer fc in VM.ExFilterList)
            {
                if (filter.FilterID == fc.FilterID)
                {
                    VM.ExFilter = fc;
                    m_imager.SetExcitationFilter(fc.PositionNumber);
                    break;
                }
            }            
        }

        public void SetManualMode(bool isManualMode)
        {
            VM.ManualMode = isManualMode;
        }

        private void GainSpin_ValueChanged(object sender, EventArgs e)
        {
            if(m_iParams!=null)
                m_iParams.Gain[0] = VM.Gain;
        }

        private void ExposureSpin_ValueChanged(object sender, EventArgs e)
        {
            if(m_iParams!=null)
                m_iParams.Exposure[0] = (float)VM.Exposure / 1000;
        }

        private void Binning_1x1_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.HorzBinning = 1;
            VM.VertBinning = 1;
            ImageDisplayControl.SetImageSize(GlobalVars.PixelWidth / VM.HorzBinning,
                                             GlobalVars.PixelHeight / VM.VertBinning,
                                             GlobalVars.MaxPixelValue);
        }

        private void Binning_2x2_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.HorzBinning = 2;
            VM.VertBinning = 2;
            ImageDisplayControl.SetImageSize(GlobalVars.PixelWidth / VM.HorzBinning,
                                             GlobalVars.PixelHeight / VM.VertBinning,
                                             GlobalVars.MaxPixelValue);
        }

        private void Binning_4x4_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.HorzBinning = 4;
            VM.VertBinning = 4;
            ImageDisplayControl.SetImageSize(GlobalVars.PixelWidth / VM.HorzBinning,
                                             GlobalVars.PixelHeight / VM.VertBinning,
                                             GlobalVars.MaxPixelValue);
        }

        private void Binning_8x8_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.HorzBinning = 8;
            VM.VertBinning = 8;
            ImageDisplayControl.SetImageSize(GlobalVars.PixelWidth / VM.HorzBinning,
                                             GlobalVars.PixelHeight / VM.VertBinning,
                                             GlobalVars.MaxPixelValue);
        }




    }



    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////


        public class VM_RunImager : INotifyPropertyChanged
        {
            private string _statusString;
            private WriteableBitmap _imageBitmap;
            private ObservableCollection<FilterContainer> _exFilterList;
            private ObservableCollection<FilterContainer> _emFilterList;
            private FilterContainer _exFilter;
            private FilterContainer _emFilter;
            private int _gain;
            private int _exposure;
            private int _roiX;
            private int _roiY;
            private int _roiW;
            private int _roiH;
            private int _horzBinning;
            private int _vertBinning;
            private string _indicatorName;
            private bool _isImaging;
            private bool _manualMode;


           
            public string StatusString
            {
                get { return _statusString; }
                set
                {
                    _statusString = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("StatusString"));
                }
            }
                       
            public WriteableBitmap ImageBitmap
            {
                get { return _imageBitmap; }
                set
                {
                    _imageBitmap = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("ImageBitmap"));
                }
            }

            public ObservableCollection<FilterContainer> ExFilterList
            {
                get { return _exFilterList; }
                set
                {
                    _exFilterList = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("ExFilterList"));
                }
            }

            public FilterContainer ExFilter
            {
                get { return _exFilter; }
                set
                {
                    _exFilter = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("ExFilter"));
                }
            }
            
            public ObservableCollection<FilterContainer> EmFilterList
            {
                get { return _emFilterList; }
                set
                {
                    _emFilterList = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("EmFilterList"));
                }
            }

            public FilterContainer EmFilter
            {
                get { return _emFilter; }
                set
                {
                    _emFilter = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("EmFilter"));
                }
            }

            public int Gain
            {
                get { return _gain; }
                set
                {
                    _gain = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Gain"));
                }
            }


            public int Exposure
            {
                get { return _exposure; }
                set
                {
                    _exposure = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Exposure"));
                }
            }


            public int RoiX
            {
                get { return _roiX; }
                set
                {
                    _roiX = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("RoiX"));
                }
            }

            public int RoiY
            {
                get { return _roiY; }
                set
                {
                    _roiY = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("RoiY"));
                }
            }

            public int RoiW
            {
                get { return _roiW; }
                set
                {
                    _roiW = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("RoiW"));
                }
            }


            public int RoiH
            {
                get { return _roiH; }
                set
                {
                    _roiH = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("RoiH"));
                }
            }


            public int HorzBinning
            {
                get { return _horzBinning; }
                set
                {
                    _horzBinning = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("HorzBinning"));
                }
            }

            public int VertBinning
            {
                get { return _vertBinning; }
                set
                {
                    _vertBinning = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("VertBinning"));
                }
            }


            public string IndicatorName
            {
                get { return _indicatorName; }
                set
                {
                    _indicatorName = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IndicatorName"));
                }
            }


            public bool IsImaging
            {
                get { return _isImaging; }
                set
                {
                    _isImaging = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IsImaging"));
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


            public VM_RunImager(string status)
            {
                _statusString = status;
                _exFilterList = new ObservableCollection<FilterContainer>();
                _emFilterList = new ObservableCollection<FilterContainer>();

                _indicatorName = "";

                Gain = 1;
                Exposure = 100;

                IsImaging = false;
                ManualMode = false;

                _horzBinning = 1;
                _vertBinning = 1;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }



    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////


        public class RunImagerEventArgs : EventArgs
        {
            private string _message;

            public RunImagerEventArgs(string TextMessage)
            {
                _message = TextMessage;
            }

            public string Message
            {
                get { return _message; }
                set { _message = value; }
            }
        }




    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////
    // /////////////////////////////////////////////////////////////////////////////////

    public class RangeClass
    {
        public int RangeMin
        {
            get;
            set;
        }

        public int RangeMax
        {
            get;
            set;
        }
    }
}
