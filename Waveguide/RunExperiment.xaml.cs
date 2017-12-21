using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Waveguide
{
    /// <summary>
    /// Interaction logic for RunExperiment.xaml
    /// </summary>
    public partial class RunExperiment : Window
    {
        WaveguideDB m_wgDB;

        Imager m_imager;
        public VWorks m_vworks;
        public int m_imageCount;

        RunExperiment_ViewModel VM;

        DispatcherTimer m_timer;
        int m_delayTime;

        Dispatcher m_dispatcher;

        DispatcherTimer m_simulationTimer;
        private DateTime m_timerStartDateTime { get; set; }
        List<ushort[]> m_simulationImageList;
        int m_simulationTime;
        bool m_simulationRunning;
        int m_indicatorIndex;
        int m_imageIndex;        
        ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> m_displayPipeline;
        ITargetBlock<Tuple<ushort[], int, int, int>> m_storagePipeline = null;
        ITargetBlock<ushort[]> m_histogramPipeline = null;
        ITargetBlock<Tuple<ushort[], int, int>> m_analysisPipeline = null;

        EnclosureCameraViewer m_enclosureCameraViewer = null;       


        Progress<int> m_progress;

        ProjectContainer m_project;
        PlateContainer m_experimentPlate;
        MethodContainer m_method;
        MaskContainer m_mask;
        PlateTypeContainer m_plateType;        
        ObservableCollection<ExperimentIndicatorContainer> m_indicatorList;
        ObservableCollection<ExperimentCompoundPlateContainer> m_compoundPlateList;
        ImagingParameters m_iParams;

        ObservableCollection<Tuple<int,int>> m_controlSubtractionWellList;
        int m_numFoFrames;
        ExperimentIndicatorContainer m_dynamicRatioNumerator;
        ExperimentIndicatorContainer m_dynamicRatioDenominator;

        TaskScheduler m_uiTask;
        CancellationTokenSource m_tokenSource;
        CancellationToken m_cancelToken;
        ColorModel m_colorModel;
        Histogram m_histogram;

        // containers for the Experiment data
        ExperimentContainer m_experiment;
        List<AnalysisContainer> m_analysisList;


        


        string m_vworksProtocolFilename;

       
        public RunExperiment(Imager imager, TaskScheduler uiTask)
        {
            //if(vworks == null)
            //{
            //    MessageBox.Show("VWorks not running!", "VWorks Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return;
            //}

            m_vworks = new VWorks();

            InitializeComponent();

            m_wgDB = new WaveguideDB();

            VM = new RunExperiment_ViewModel();

            m_uiTask = uiTask;

            m_dispatcher = this.Dispatcher;

            this.DataContext = VM;

            m_timer = new DispatcherTimer();
            m_timer.Tick += m_timer_Tick;
            m_timer.Interval = TimeSpan.FromMilliseconds(1000);

          
            VM.RunState = RunExperiment_ViewModel.RUN_STATE.WAITING_TO_RUN;

            
            m_imager = imager;

            if(m_imager == null)
            {
                m_imager = new Imager();
                m_imager.Initialize();
            }

            m_imager.m_cameraEvent += m_imager_cameraEvent;
            m_imager.m_temperatureEvent += m_imager_temperatureEvent;


            m_vworks.PostVWorksCommandEvent += m_vworks_PostVWorksCommandEvent;


            m_simulationRunning = false;
            m_indicatorIndex = 0;
            m_imageIndex = 0;

            LoadDefaultColorModel();


            // catch close event caused by clicking X button
            this.Closing += new System.ComponentModel.CancelEventHandler(Window_Closing);
                   

        }


        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // catch closing event caused by hitting X button if experiment is running

            if (VM.RunState == RunExperiment_ViewModel.RUN_STATE.RUNNING)
            {
                e.Cancel = true;

                MessageBoxResult result = MessageBox.Show("Are you sure you want to Abort?", "Abort Experiment",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        RunPB_Click(null, null); // this will Abort the Experiment (calling RunPB_Click when VM.RunState == RUNNING)                        
                        break;
                    case MessageBoxResult.No:                        
                        break;
                }
            }

            // Good time to unload/shutdown VWorks


            m_vworks = null;
            
            // set this if you want to cancel the close event: e.Cancel = true;
        }

        public void LoadDefaultColorModel()
        {
            m_colorModel = null;

            bool success = m_wgDB.GetAllColorModels();
            if (success)
            {
                foreach(ColorModelContainer cModel in m_wgDB.m_colorModelList)
                {
                    if(cModel.IsDefault || m_colorModel == null)
                    {
                        m_colorModel = new ColorModel(cModel, GlobalVars.MaxPixelValue);                        
                    }
                }
            }
        }




        void m_imager_temperatureEvent(object sender, TemperatureEventArgs e)
        {
            if (e.GoodReading) VM.TemperatureText = e.Temperature.ToString();
        }

        void m_imager_cameraEvent(object sender, CameraEventArgs e)
        {
            PostMessage(e.Message);
        }



        public void Configure(ImagingParameters iParams, ProjectContainer project, PlateContainer plate, ExperimentContainer experiment,
                         MethodContainer method, 
                         MaskContainer mask, PlateTypeContainer plateType,
                         ObservableCollection<ExperimentIndicatorContainer> indicatorList,
                         ObservableCollection<ExperimentCompoundPlateContainer> compoundPlateList,
                         ObservableCollection<Tuple<int,int>> controlSubtractionWellList,
                         int numFoFrames,
                         ExperimentIndicatorContainer dynamicRatioNumerator,
                         ExperimentIndicatorContainer dynamicRatioDenominator)
        {
            m_iParams = iParams;
            m_project = project;
            m_experimentPlate = plate;
            m_experiment = experiment;
            m_method = method;
            m_mask = mask;
            m_plateType = plateType;
            m_indicatorList = indicatorList;
            m_compoundPlateList = compoundPlateList;
            m_controlSubtractionWellList = controlSubtractionWellList;
            m_numFoFrames = numFoFrames;
            m_dynamicRatioNumerator = dynamicRatioNumerator;
            m_dynamicRatioDenominator = dynamicRatioDenominator;


            m_vworksProtocolFilename = m_method.BravoMethodFile;

            ChartArray.BuildChartArray(mask.Rows, mask.Cols, m_iParams.HorzBinning, m_iParams.VertBinning, m_indicatorList);

            Dictionary<int, ImageDisplay> idDictionary = ChartArray.GetImageDisplayDictionary();

            m_displayPipeline = m_imager.CreateDisplayPipeline(m_uiTask, idDictionary);


            m_storagePipeline = m_imager.CreateImageStoragePipeline(GlobalVars.CompressionAlgorithm, m_iParams.imageWidth, m_iParams.imageHeight);

            int numerID = 0;
            int denomID = 0;
            if (m_dynamicRatioNumerator != null) numerID = m_dynamicRatioNumerator.ExperimentIndicatorID;
            if (m_dynamicRatioDenominator != null) denomID = m_dynamicRatioDenominator.ExperimentIndicatorID;
            
            m_analysisPipeline = m_imager.CreateAnalysisPipeline(ChartArray, m_mask, m_iParams.imageWidth,
                    m_iParams.imageHeight, m_iParams.HorzBinning, m_iParams.VertBinning,
                    m_iParams.ExperimentIndicatorID, m_controlSubtractionWellList, m_numFoFrames,
                    numerID, denomID);                    
            

            if (m_histogram != null)
            {
                m_histogramPipeline = m_imager.CreateHistogramPipeline(m_uiTask, m_histogram);
            }


        }





        public void PostMessage(string msg)
        {
            if (this.Dispatcher.CheckAccess())
            {
                MessageDisplay.AppendText(Environment.NewLine);
                MessageDisplay.AppendText(msg);
                MessageDisplay.ScrollToEnd();
            }
            else
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    MessageDisplay.AppendText(Environment.NewLine);
                    MessageDisplay.AppendText(msg);
                    MessageDisplay.ScrollToEnd();
                }));
            }
        }



        void m_vworks_PostVWorksCommandEvent(object sender, WaveGuideEvents.VWorksCommandEventArgs e)
        {
            VWORKS_COMMAND command = e.Command;
            int param1 = e.Param1;
            string name = e.Name;
            string desc = e.Description;
            int sequenceNumber = m_imager.GetSequenceCount();
            bool success;
            EventMarkerContainer eventMarker;

            switch (command)
            {
                case VWORKS_COMMAND.Protocol_Aborted:
                    PostMessage("VWorks - Protocol Aborted");

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        m_timer.Stop();
                        VM.DelayText = "";
                        m_tokenSource.Cancel();  // stops the imaging task
                        VM.RunState = RunExperiment_ViewModel.RUN_STATE.ABORTED;
                        SetButton(VM.RunState);
                    }));
                    
                    break;
                case VWORKS_COMMAND.Protocol_Resumed:
                    PostMessage("VWorks - Protocol Resumed");
                     m_dispatcher.Invoke((Action)(() =>
                        {
                            m_timer.Stop();
                            VM.DelayText = "";
                            VM.DelayHeaderVisible = false;
                        }));
                    break;
                case VWORKS_COMMAND.Protocol_Complete:
                    PostMessage("VWorks - Protocol Complete");
                    
                        m_dispatcher.Invoke((Action)(() =>
                        {
                            m_timer.Stop();
                            VM.DelayText = "";
                            VM.DelayHeaderVisible = false;
                            m_tokenSource.Cancel(); // make sure the imaging task stops
                            VM.RunState = RunExperiment_ViewModel.RUN_STATE.FINISHED;
                            SetButton(VM.RunState);

                            ReportDialog dlg = new ReportDialog(m_project, m_experiment, m_indicatorList);
                            dlg.ShowDialog();
                        }));
                     
                    
                    break;
                case VWORKS_COMMAND.Protocol_Paused:
                    PostMessage("VWorks - Protocol Paused");

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        m_delayTime = param1;
                        VM.DelayHeaderVisible = true;
                        m_timer.Start();
                    }));
                    
                    break;
                case VWORKS_COMMAND.Event_Marker:
                    PostMessage("VWorks - Event Marker");

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        ChartArray.AddEventMarker(sequenceNumber, desc);
                        eventMarker = new EventMarkerContainer();
                        eventMarker.Description = desc;
                        eventMarker.ExperimentID = m_experiment.ExperimentID;
                        eventMarker.Name = name;
                        eventMarker.SequenceNumber = sequenceNumber;
                        eventMarker.TimeStamp = DateTime.Now;
                        success = m_wgDB.InsertEventMarker(ref eventMarker);
                        if (!success) PostMessage("Database Error in InsertEventMarker: " + m_wgDB.GetLastErrorMsg());
                    }));

                    
                    break;
                case VWORKS_COMMAND.Initialization_Complete:
                    PostMessage("VWorks Initialization Complete");
                    
                    m_dispatcher.Invoke((Action)(() =>
                    {
                        BringWindowToFront();
                    }));

                    break;
                case VWORKS_COMMAND.Pause_Until:
                    //PostMessage("VWorks - Pause Until");

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        m_delayTime = param1;
                        VM.DelayHeaderVisible = true;
                        VM.DelayText = ((int)(m_delayTime / 1000)).ToString();
                        m_timer.Start();
                    }));

                    
                    break;
                case VWORKS_COMMAND.Set_Time_Marker:
                    //PostMessage("VWorks - Set Time Marker");
                    break;
                case VWORKS_COMMAND.Start_Imaging:
                    PostMessage("VWorks - Start Imaging");
                                   
                    /////////////////////////////////////////////////////////////////////////////////

                    /// Start Imaging Task 
                    m_dispatcher.Invoke((Action)(() =>
                    {
                        BringWindowToFront();
                    }));
                    
                    StartImaging_2(10000);
                   
                    ////////////////////////////////////////////////////////////////////////////////


                    break;
                case VWORKS_COMMAND.Stop_Imaging:
                    PostMessage("VWorks - Stop Imaging");

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        StopImaging();
                    }));                                      

                    break;
                case VWORKS_COMMAND.Unrecoverable_Error:
                    PostMessage("VWorks - Unrecoverable Error" + ", " + name + ", " + desc);

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        m_timer.Stop();
                        VM.DelayText = "";
                        m_tokenSource.Cancel();  // stops the imaging task
                        VM.RunState = RunExperiment_ViewModel.RUN_STATE.ERROR;
                        SetButton(VM.RunState);
                    }));
                    
                    break;
                case VWORKS_COMMAND.Error:
                    PostMessage("VWorks - Error" + ", " + name + ", " + desc);

                    m_dispatcher.Invoke((Action)(() =>
                    {
                        m_timer.Stop();
                        VM.DelayText = "";
                        VM.DelayHeaderVisible = false;
                        m_tokenSource.Cancel();  // stops the imaging task
                        VM.RunState = RunExperiment_ViewModel.RUN_STATE.ERROR;
                        SetButton(VM.RunState);
                    }));
                                            
                    break;
                case VWORKS_COMMAND.Message:
                    PostMessage("VWorks - Message, " + name + ", " + desc);
                    break;
                default:
                    break;
            }
        }


        public void StopImaging()
        {
            m_tokenSource.Cancel();
        }

      
        public void StartImaging_2(int numberOfImagesToTake)
        {
            m_imager.StartImaging_2(m_iParams, m_cancelToken, m_progress, m_colorModel.m_colorMap,
                m_displayPipeline, m_storagePipeline, m_histogramPipeline, m_analysisPipeline);
        }


        public async void StartImaging(int numberOfImagesToTake)
        {
            if (m_imager == null) return;

            //m_uiTask = TaskScheduler.FromCurrentSynchronizationContext();
                      
            Task ImagingTask = Task.Factory.StartNew(() => m_imager.StartImaging(m_iParams,
                Tuple.Create(m_mask,m_controlSubtractionWellList,m_numFoFrames,
                             m_dynamicRatioNumerator.ExperimentIndicatorID,
                             m_dynamicRatioDenominator.ExperimentIndicatorID),
                m_cancelToken, m_uiTask, m_progress,
                null, m_colorModel.m_colorMap,
                m_histogram), m_cancelToken);


            try
            {
                await ImagingTask;
            }            
            catch (OperationCanceledException)
            {
            }            
            finally
            {
                ImagingTask.Dispose();                
            }
        }


        void BringWindowToFront()
        {
            // Bring this window into view (on top of VWorks)
                if (!this.IsVisible)
                {
                    this.Show();
                }

                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }

                this.Activate();
                this.Topmost = true;  // important
                this.Topmost = false; // important
                this.Focus();         // important 
            
        }



        void m_timer_Tick(object sender, EventArgs e)
        {
            m_delayTime -= 1000;
            if (m_delayTime <= 0)
            {
                m_timer.Stop();
                VM.DelayHeaderVisible = false;
                VM.DelayText = "";
            }
            else
            {
                VM.DelayHeaderVisible = true;
                VM.DelayText = ((int)(m_delayTime / 1000)).ToString();
            }
        }


        private void AbortExperiment()
        {
            m_vworks.VWorks_AbortProtocol();
            m_tokenSource.Cancel();  // stops the imaging task
        }




        private void RunPB_Click(object sender, RoutedEventArgs e)
        {

            switch (VM.RunState)
            {
                case RunExperiment_ViewModel.RUN_STATE.WAITING_TO_RUN:
                    m_tokenSource = new CancellationTokenSource();
                    m_cancelToken = m_tokenSource.Token;
                    m_progress = new Progress<int>();

                        // RUN EXPERIMENT !!
                    VM.RunState = RunExperiment_ViewModel.RUN_STATE.RUNNING;
                        SetButton(VM.RunState);

                        m_vworks.StartMethod(m_vworksProtocolFilename); 
               break;

                case RunExperiment_ViewModel.RUN_STATE.RUNNING:                                        
                    AbortExperiment();
                    VM.RunState = RunExperiment_ViewModel.RUN_STATE.ABORTED;
                    SetButton(VM.RunState);
               break;

                case RunExperiment_ViewModel.RUN_STATE.FINISHED:                    
                    Close_EnclosureCameraViewer();
                    Close();
               break;

                case RunExperiment_ViewModel.RUN_STATE.ABORTED:                    
                    Close_EnclosureCameraViewer();
                    Close();
               break;

                case RunExperiment_ViewModel.RUN_STATE.ERROR:                    
                    Close_EnclosureCameraViewer();
                    Close();
               break;
            }  // END switch (m_state)

        } // END RunPB_Click




        public void SetButton(RunExperiment_ViewModel.RUN_STATE state)
        {
            switch (state)
            {
                case RunExperiment_ViewModel.RUN_STATE.WAITING_TO_RUN:
                    RunPB.Content = "Run";
                    break;
                case RunExperiment_ViewModel.RUN_STATE.RUNNING:
                    RunPB.Content = "Abort";
                    break;
                case RunExperiment_ViewModel.RUN_STATE.FINISHED:
                    RunPB.Content = "Close";
                    break;
                case RunExperiment_ViewModel.RUN_STATE.ABORTED:
                    RunPB.Content = "Close";
                    break;
                case RunExperiment_ViewModel.RUN_STATE.ERROR:
                    RunPB.Content = "Close";
                    break;

            }
        }



        //////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////

        


        public async void StartVideo(ImagingParameters iParams)
        {
            if (m_imager == null) return;
 
            //m_uiTask = TaskScheduler.FromCurrentSynchronizationContext();

            m_tokenSource = new CancellationTokenSource();
            m_cancelToken = m_tokenSource.Token;

            Progress<int> progress = new Progress<int>();
            progress.ProgressChanged += (sender1, num) =>
            {
                VM.MessageText = num.ToString() + " images";
            };

            Dictionary<int,ImageDisplay> imageDisplayDictionary = ChartArray.GetImageDisplayDictionary();

            foreach (KeyValuePair<int, ImageDisplay> entry in imageDisplayDictionary)
            {
                ImageDisplay imageDisplay = entry.Value;
                imageDisplay.SetImageSize(iParams.imageWidth, m_iParams.imageHeight, m_iParams.maxPixelValue);
            }


            Task ImagingTask = Task.Factory.StartNew(() => m_imager.StartImaging(m_iParams, 
                Tuple.Create(m_mask,m_controlSubtractionWellList, m_numFoFrames,
                             m_dynamicRatioNumerator.ExperimentIndicatorID,
                             m_dynamicRatioDenominator.ExperimentIndicatorID),
                m_cancelToken, m_uiTask, progress,                                                           
                null, m_colorModel.m_colorMap, m_histogram,ChartArray), m_cancelToken);


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
               
            }
            catch (Exception ex)
            {
                MessageBoxResult result = MessageBox.Show(ex.Message, "Unknown Exception Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImagingTask.Dispose();
            }

        }




        //////////////////////////////////////////////////////////////////////////
        // simulation-related methods
        
        private void SimulatePB_Click(object sender, RoutedEventArgs e)
        {
            if (!m_simulationRunning)
            {
                StartSimulationTimer(1000);
                m_simulationRunning = true;
                RunPB.IsEnabled = false;
                SimulatePB.Content = "Stop";
            }
            else
            {
                StopSimulation();
                m_simulationRunning = false;
                RunPB.IsEnabled = true;
                SimulatePB.Content = "Sim";

                ReportDialog dlg = new ReportDialog(m_project, m_experiment, m_indicatorList);
                dlg.ShowDialog();
            }
        }

        private void StopSimulation()
        {
            m_simulationTimer.Stop();
        }


        public void StartSimulationTimer(int interval)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            LoadSimulationConfiguration();

            int hBinning = 1, vBinning = 1;

            ChartArray.BuildChartArray(m_mask.Rows, m_mask.Cols, hBinning, vBinning, m_indicatorList);

            ////////////////////////////////////////////////////
            //m_uiTask = TaskScheduler.FromCurrentSynchronizationContext();
            Dictionary<int,ImageDisplay> idDictionary = ChartArray.GetImageDisplayDictionary();
            
            m_displayPipeline = m_imager.CreateDisplayPipeline(m_uiTask, idDictionary);
                
            
            m_storagePipeline = m_imager.CreateImageStoragePipeline(GlobalVars.CompressionAlgorithm, m_iParams.imageWidth, m_iParams.imageHeight);
            
            if (ChartArray != null)
            {
                m_analysisPipeline = m_imager.CreateAnalysisPipeline(ChartArray, m_mask, m_iParams.imageWidth,
                    m_iParams.imageHeight, m_iParams.HorzBinning, m_iParams.VertBinning,
                    m_iParams.ExperimentIndicatorID, m_controlSubtractionWellList, m_numFoFrames,
                    m_dynamicRatioNumerator.ExperimentIndicatorID, 
                    m_dynamicRatioDenominator.ExperimentIndicatorID);
            }

            if (m_histogram != null)
            {
                m_histogramPipeline = m_imager.CreateHistogramPipeline(m_uiTask, m_histogram);
            }




            /////////////////////////////////////////////////////////
            // use reference images as simulated images from camera
            bool success = m_wgDB.GetAllReferenceImages();
            m_simulationImageList = new List<ushort[]>();
            foreach(ReferenceImageContainer image in m_wgDB.m_refImageList)
            {
                ushort[] img = new ushort[image.Width * image.Height];
                Buffer.BlockCopy(image.ImageData, 0, img, 0, image.NumBytes);
                m_simulationImageList.Add(img);
            }

            m_simulationTimer = new DispatcherTimer();
            m_simulationTimer.Tick += m_simulationTimer_Tick;
            m_simulationTimer.Interval = TimeSpan.FromMilliseconds(interval);
            m_simulationTime = 0;
            m_timerStartDateTime = DateTime.Now;
            m_simulationTimer.Start();

            stopwatch.Stop();
            PostMessage("startup time = " + stopwatch.ElapsedMilliseconds.ToString());
        }

        void m_simulationTimer_Tick(object sender, EventArgs e)
        {            
            m_simulationTime += m_simulationTimer.Interval.Milliseconds;

            m_indicatorIndex++;
            if(m_indicatorIndex>=m_indicatorList.Count()) m_indicatorIndex = 0;
            ExperimentIndicatorContainer expInd = m_indicatorList.ElementAt(m_indicatorIndex);

            m_imageIndex++;
            if (m_imageIndex >= m_simulationImageList.Count()) m_imageIndex = 0;

            m_displayPipeline.Post(Tuple.Create(m_simulationImageList.ElementAt(m_imageIndex),
                                                expInd.ExperimentIndicatorID,
                                                m_iParams.imageWidth, m_iParams.imageHeight,
                                                ChartArray.m_colorModel.m_colorMap));

            var currentValue = DateTime.Now - m_timerStartDateTime;
            int totalMilliseconds = currentValue.Minutes*60000 + currentValue.Seconds*1000 + currentValue.Milliseconds;

            m_storagePipeline.Post(Tuple.Create(m_simulationImageList.ElementAt(m_imageIndex), expInd.ExperimentIndicatorID,
                totalMilliseconds, expInd.Exposure));

            if(m_histogramPipeline!=null)
                m_histogramPipeline.Post(m_simulationImageList.ElementAt(m_imageIndex));

            m_analysisPipeline.Post(Tuple.Create(m_simulationImageList.ElementAt(m_imageIndex), expInd.ExperimentIndicatorID, 
                totalMilliseconds));
            

            PostMessage("Image Index = " + m_imageIndex.ToString() + "      Time = " + totalMilliseconds.ToString());
        }



        void LoadSimulationConfiguration()
        {
            //  m_iParams = iParams;
            //  m_project = project;
            //  m_experimentPlate = plate;
            //  m_method = method;
            //  m_mask = mask;
            //  m_plateType = plateType;
            //  m_indicatorList = indicatorList;
            //  m_compoundPlateList = compoundPlateList;
            //  m_controlSubtractionWellList = controlSubtractionWellList;
            //m_numFoFrames = numFoFrames;
            //m_dynamicRatioNumerator = dynamicRatioNumerator;
            //m_dynamicRatioDenominator = dynamicRatioDenominator;

            //ChartArray.BuildChartArray(mask.Rows, mask.Cols, m_indicatorList);

            ////////////////////////
            // set up project
            m_project = null;
            bool success = m_wgDB.GetAllProjects(false);
            foreach(ProjectContainer project in m_wgDB.m_projectList)
            {
                if (project.Description.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                {
                    m_project = project;
                    break;
                }
            }
            if(m_project==null) // not found in database, so create it
            {
                m_project = new ProjectContainer();
                m_project.Description = "Debug";
                m_project.TimeStamp = DateTime.Now;
                m_project.Archived = false;
                success = m_wgDB.InsertProject(ref m_project);
            }


            ////////////////////////
            // set up plateType
            m_plateType = null;
            success = m_wgDB.GetAllPlateTypes();
            if (m_wgDB.m_plateTypeList.Count() > 0)
            {
                m_plateType = m_wgDB.m_plateTypeList.ElementAt(0);
            }
            else
            {
                // create a new plateType
                m_plateType = new PlateTypeContainer();
                m_plateType.Cols = 24;
                m_plateType.Description = "Debug";
                m_plateType.IsDefault = false;
                m_plateType.Rows = 16;
                success = m_wgDB.InsertPlateType(ref m_plateType);
            }



            ////////////////////////
            // set up experiment plate
            m_experimentPlate = null;
            success = m_wgDB.GetAllPlatesForProject(m_project.ProjectID);
            if(m_wgDB.m_plateList.Count() > 0)
            {
                m_experimentPlate = m_wgDB.m_plateList.ElementAt(0);
            }
            else
            {
                // create a new plate
                m_experimentPlate = new PlateContainer();
                m_experimentPlate.Barcode = "12345678";
                m_experimentPlate.Description = "Debug";
                m_experimentPlate.IsPublic = true;
                m_experimentPlate.OwnerID = GlobalVars.UserID;
                m_experimentPlate.PlateTypeID = m_plateType.PlateTypeID;
                m_experimentPlate.ProjectID = m_project.ProjectID;

                success = m_wgDB.InsertPlate(ref m_experimentPlate);
            }


            

            ////////////////////////
            // set up method
            m_method = null;
            success = m_wgDB.GetAllMethodsForUser(GlobalVars.UserID);
            foreach (MethodContainer method in m_wgDB.m_methodList)
            {
                if (method.Description.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                {
                    m_method = method;                    
                    break;
                }
            }
            if(m_method == null)
            {
                m_method = new MethodContainer();
                m_method.BravoMethodFile = "";
                m_method.Description = "Debug";
                m_method.IsPublic = true;
                m_method.OwnerID = GlobalVars.UserID;
                success = m_wgDB.InsertMethod(ref m_method);                
            }
            success = m_wgDB.GetAllIndicatorsForMethod(m_method.MethodID);
            if(m_wgDB.m_indicatorList.Count<1)
            {
                // create indicators for this new method
                IndicatorContainer ind = new IndicatorContainer();
                ind.Description = "Debug";
                ind.EmissionsFilterPosition = 6;
                ind.ExcitationFilterPosition = 4;
                ind.MethodID = m_method.MethodID;
                ind.SignalType = SIGNAL_TYPE.UP;
                success = m_wgDB.InsertIndicator(ref ind);
            }

            


            ////////////////////////
            // set up experiment
            m_experiment = null;
            ObservableCollection<ExperimentContainer> expList;
            success = m_wgDB.GetAllExperimentsForPlate(m_experimentPlate.PlateID, out expList);
            if (expList.Count() > 0)
            {
                m_experiment = expList.ElementAt(0);
            }
            else
            {
                m_experiment = new ExperimentContainer();
                m_experiment.Description = "Debug";
                m_experiment.HorzBinning = 1;
                m_experiment.MethodID = m_method.MethodID;
                m_experiment.PlateID = m_experimentPlate.PlateID;
                m_experiment.ROI_Height = GlobalVars.PixelHeight;
                m_experiment.ROI_Width = GlobalVars.PixelWidth;
                m_experiment.ROI_Origin_X = 1;
                m_experiment.ROI_Origin_Y = 1;
                m_experiment.TimeStamp = DateTime.Now;
                m_experiment.VertBinning = 1;
                success = m_wgDB.InsertExperiment(ref m_experiment);
            }


            ////////////////////////
            // set up mask
            m_mask = null;
            success = m_wgDB.GetAllMasksForPlateType(m_experimentPlate.PlateTypeID);
            if (m_wgDB.m_maskList.Count() > 0)
            {
                m_mask = m_wgDB.m_maskList.ElementAt(0);
            }
            else
            {
                // create a new mask
                m_mask = new MaskContainer();
                m_mask.Angle = 0.0;
                m_mask.Cols = 24;
                m_mask.Description = "Debug";
                m_mask.IsDefault = false;
                m_mask.NumEllipseVertices = 24;
                m_mask.PlateTypeID = m_experimentPlate.PlateTypeID;
                m_mask.ReferenceImageID = 0;
                m_mask.Rows = 16;
                m_mask.Shape = 0;
                m_mask.XOffset = 28;
                m_mask.XSize = 28;
                m_mask.XStep = 41.522;
                m_mask.YOffset = 190;
                m_mask.YSize = 28;
                m_mask.YStep = 41.467;
                success = m_wgDB.InsertMask(ref m_mask);
            }



             ////////////////////////////////////
            // setup test indicator(s)
            m_indicatorList = new ObservableCollection<ExperimentIndicatorContainer>();
            ObservableCollection<ExperimentIndicatorContainer> expIndList;
            success = m_wgDB.GetAllExperimentIndicatorsForExperiment(m_experiment.ExperimentID,out expIndList);
            if(expIndList.Count > 0)
            {                
                foreach(ExperimentIndicatorContainer ex in expIndList)
                {
                    m_indicatorList.Add(ex);
                }
            }
            else
            {
                success = m_wgDB.GetAllIndicatorsForMethod(m_method.MethodID);
                foreach(IndicatorContainer ind in m_wgDB.m_indicatorList)
                {
                    ExperimentIndicatorContainer exInd = new ExperimentIndicatorContainer();
                    exInd.Description = ind.Description;

                    FilterContainer filter;
                    success = m_wgDB.GetExcitationFilterAtPosition(ind.ExcitationFilterPosition, out filter);
                    exInd.ExcitationFilterDesc = filter.Description;
                    success = m_wgDB.GetEmissionFilterAtPosition(ind.EmissionsFilterPosition, out filter);
                    exInd.EmissionFilterDesc = filter.Description;

                    exInd.EmissionFilterPos = ind.EmissionsFilterPosition;                    
                    exInd.ExcitationFilterPos = ind.ExcitationFilterPosition;
                    exInd.ExperimentID = m_experiment.ExperimentID;
                    exInd.Exposure = 150;
                    exInd.Gain = 5;
                    exInd.MaskID = m_mask.MaskID;
                    exInd.SignalType = SIGNAL_TYPE.UP;
                    exInd.Verified = true;
                    success = m_wgDB.InsertExperimentIndicator(ref exInd);
                    m_indicatorList.Add(exInd);
                }               
            }


            ////////////////////////////////////
            // compound plates
            m_compoundPlateList = new ObservableCollection<ExperimentCompoundPlateContainer>();


            ////////////////////////////////////
            // control subtraction well list
            m_controlSubtractionWellList = new ObservableCollection<Tuple<int, int>>();
            // all wells in last row = control wells
            for (int c = 0; c < m_mask.Cols; c++)
                m_controlSubtractionWellList.Add(Tuple.Create(m_mask.Rows - 1, c));

            ////////////////////////////////////
            //m_numFoFrames = numFoFrames;
            m_numFoFrames = 5;


            if(m_indicatorList.Count()>1)
            {
                ////////////////////////////////////
                //m_dynamicRatioNumerator = dynamicRatioNumerator;
                m_dynamicRatioNumerator = m_indicatorList.ElementAt(0);
                ////////////////////////////////////
                //m_dynamicRatioDenominator = dynamicRatioDenominator;
                m_dynamicRatioDenominator = m_indicatorList.ElementAt(1);
            }
            else 
            {
                m_dynamicRatioNumerator = new ExperimentIndicatorContainer();
                m_dynamicRatioNumerator.ExperimentIndicatorID = 0;
                m_dynamicRatioDenominator = new ExperimentIndicatorContainer();
                m_dynamicRatioDenominator.ExperimentIndicatorID = 0;
            }


            if (m_iParams == null) m_iParams = new ImagingParameters();


            m_iParams.maxPixelValue = GlobalVars.MaxPixelValue;
            m_iParams.imageWidth = GlobalVars.PixelWidth;
            m_iParams.imageHeight = GlobalVars.PixelHeight;
            m_iParams.Image_StartCol = 1;
            m_iParams.Image_EndCol = GlobalVars.PixelWidth;
            m_iParams.Image_StartRow = 1;
            m_iParams.Image_EndRow = GlobalVars.PixelHeight;
            m_iParams.BravoMethodFilename = "";
            m_iParams.CameraTemperature = GlobalVars.CameraTargetTemperature;
            m_iParams.HorzBinning = 1;
            m_iParams.VertBinning = 1;
            m_iParams.EmissionFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            m_iParams.ExcitationFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            m_iParams.LightIntensity = 100;
            m_iParams.NumImages = 1000000; // artificial limit on number of images
            m_iParams.NumIndicators = m_indicatorList.Count;
            m_iParams.SyncExcitationFilterWithImaging = true;

            m_iParams.CycleTime = new int[m_indicatorList.Count];
            m_iParams.EmissionFilter = new byte[m_indicatorList.Count];
            m_iParams.ExcitationFilter = new byte[m_indicatorList.Count];
            m_iParams.Exposure = new float[m_indicatorList.Count];
            m_iParams.Gain = new int[m_indicatorList.Count];
            m_iParams.ExperimentIndicatorID = new int[m_indicatorList.Count];
            m_iParams.IndicatorName = new string[m_indicatorList.Count];
            m_iParams.LampShutterIsOpen = new bool[m_indicatorList.Count];


            int i = 0;
            foreach (ExperimentIndicatorContainer ind in m_indicatorList)
            {
                m_iParams.CycleTime[i] = 1000;
                m_iParams.EmissionFilter[i] = (byte)ind.EmissionFilterPos;
                m_iParams.ExcitationFilter[i] = (byte)ind.ExcitationFilterPos;
                m_iParams.Exposure[i] = (float)ind.Exposure / 1000;
                m_iParams.Gain[i] = ind.Gain;
                m_iParams.ExperimentIndicatorID[i] = 0; // created by the RunExperiment object when the experiment is run
                m_iParams.IndicatorName[i] = ind.Description;
                m_iParams.LampShutterIsOpen[i] = true;
                m_iParams.ExperimentIndicatorID[i] = ind.ExperimentIndicatorID;

                i++;
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //TemperatureMonitorDialog dlg = new TemperatureMonitorDialog(m_imager.m_camera);

            //dlg.ShowDialog();
        }

        private void EnclosureCameraPB_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (m_enclosureCameraViewer == null)
            {
                m_enclosureCameraViewer = new EnclosureCameraViewer();

                m_enclosureCameraViewer.Closed += m_enclosureCameraViewer_Closed;

                m_enclosureCameraViewer.Show();
            }
            else
            {
                m_enclosureCameraViewer.BringWindowToFront();
            }
        }

        void m_enclosureCameraViewer_Closed(object sender, EventArgs e)
        {
            m_enclosureCameraViewer = null;
        }

        void Close_EnclosureCameraViewer()
        {
            if (m_enclosureCameraViewer != null)
                m_enclosureCameraViewer.Close();
        }


        //////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////

    } // END RunExperiment Class

    

    public class RunExperiment_ViewModel : INotifyPropertyChanged
    {
        public enum RUN_STATE
        {
            WAITING_TO_RUN,
            RUNNING,
            FINISHED,
            ABORTED,
            ERROR
        };

        private RUN_STATE _runState;

        private string _messageString;
        private string _delayText;
        private bool _delayHeaderVisible;
        private string _temperatureText;
        private ProjectContainer _project;
        private PlateContainer _experimentPlate;
        private MethodContainer _method;
        private MaskContainer _mask;
        private PlateTypeContainer _plateType;

        public RUN_STATE RunState { get { return _runState; } set { _runState = value; NotifyPropertyChanged("RunState"); } }

        public string MessageText { get { return _messageString; } set { _messageString = value; NotifyPropertyChanged("MessageText"); } }
        public string DelayText { get { return _delayText; } set { _delayText = value; NotifyPropertyChanged("DelayText"); } }
        public bool DelayHeaderVisible { get { return _delayHeaderVisible; } set { _delayHeaderVisible = value; NotifyPropertyChanged("DelayHeaderVisible"); } }
        public string TemperatureText { get { return _temperatureText; } set { _temperatureText = value; NotifyPropertyChanged("TemperatureText"); } }

        public ProjectContainer Project { get { return _project; } set { _project = value; NotifyPropertyChanged("Project"); } }
        public PlateContainer ExperimentPlate { get { return _experimentPlate; } set { _experimentPlate = value; NotifyPropertyChanged("ExperimentPlate"); } }
        public MethodContainer Method { get { return _method; } set { _method = value; NotifyPropertyChanged("Method"); } }
        public MaskContainer Mask { get { return _mask; } set { _mask = value; NotifyPropertyChanged("Mask"); } }
        public PlateTypeContainer PlateType { get { return _plateType; } set { _plateType = value; NotifyPropertyChanged("PlateType"); } }

        public RunExperiment_ViewModel()
        {
            DelayText = "";
            DelayHeaderVisible = false;
            TemperatureText = "--";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }
    }

}
