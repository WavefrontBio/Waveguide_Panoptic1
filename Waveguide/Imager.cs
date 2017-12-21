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
using System.Windows.Media.Imaging;

namespace Waveguide
{
  
    public delegate void CameraEventHandler(object sender, CameraEventArgs e);
    public delegate void TemperatureEventHandler(object sender, TemperatureEventArgs e);
   

    public class Imager
    {
        public bool m_ImagerInitialized;
        public Camera m_camera;
        public Lambda m_lambda;
        public Thor m_thor;

        CancellationTokenSource m_cameraTemperatureTokenSource;
        CancellationToken m_cameraTemperatureCancelToken;

        //CancellationTokenSource m_imagingTokenSource;
        //CancellationToken m_imagingCancelToken;

        bool m_imagerRunning;
        bool m_tempMonitorRunning;
        private bool resetTimer = true;
        private Stopwatch sequenceCounter = new Stopwatch();  // used to timestamp each image in the sequence

        
        public event CameraEventHandler m_cameraEvent;
        protected void OnCameraEvent(CameraEventArgs e)
        {
            m_cameraEvent(this, e);
        }

        public event TemperatureEventHandler m_temperatureEvent;
        protected virtual void OnTemperatureEvent(TemperatureEventArgs e)
        {
            m_temperatureEvent(this, e);
        }


               

        public Imager()  // Constructor
        {
            m_ImagerInitialized = false;
            m_camera = new Camera();
            m_lambda = new Lambda();
            m_thor = new Thor();
            m_imagerRunning = false;
            m_tempMonitorRunning = false;
        }


        public bool Initialize()
        {
            m_ImagerInitialized = true;
         
            // Initialize Camera
            bool success = m_camera.Initialize();
            if (!success)
            {
                m_ImagerInitialized = false;

                OnCameraEvent(new CameraEventArgs("Camera FAILED to Initialize", false));

                return success;
            }
            else
            {
                GlobalVars.PixelWidth = m_camera.XPixels;
                GlobalVars.PixelHeight = m_camera.YPixels;
                GlobalVars.MaxPixelValue = (int)(Math.Pow(2,m_camera.BitDepth)-1);               

                OnCameraEvent(new CameraEventArgs("Camera Initialized Successfully", false));

                // turn on Camera cooler
                m_camera.CoolerON(true);
                m_camera.SetCoolerTemp(GlobalVars.CameraTargetTemperature);

                // start Temperature Monitoring Task
                if (!m_tempMonitorRunning)
                {
                    m_cameraTemperatureTokenSource = new CancellationTokenSource();
                    m_cameraTemperatureCancelToken = m_cameraTemperatureTokenSource.Token;

                    var progressIndicator = new Progress<TemperatureProgressReport>(ReportTemperature);

                    Task.Factory.StartNew(() =>
                        {
                            TempMonitorWorker(progressIndicator);
                        }, m_cameraTemperatureCancelToken);

                    OnCameraEvent(new CameraEventArgs("Camera Temperature Monitoring Started", false));
                }


                // Initialize Lambda (filter controller)
                if (!m_lambda.SystemInitialized)
                {               
                    success = m_lambda.Initialize();
                    if (!success) 
                    { 
                        m_ImagerInitialized = false;
                        OnCameraEvent(new CameraEventArgs("Filter Controller FAILED to Initialize", false));
                        return success;
                    }
                    else 
                    { 
                        OnCameraEvent(new CameraEventArgs("Filter Controller Initialized Successfully", false)); 
                    }
                }

                // Initialize Thor (light controller)
                //success = m_thor.Initialize();
                //if (!success) { m_ImagerInitialized = false; }
                
            }

            return success;
        }


        public void Shutdown()
        {            
            m_cameraTemperatureTokenSource.Cancel();  // stops the temperature monitoring Task          
        }

        public bool IsInitialized()
        {
            return m_ImagerInitialized;
        }

        public void SetExcitationFilter(int position)
        {
            m_lambda.MoveFilterA((byte)position, 5);
        }

        public void SetEmissionFilter(int position)
        {
            m_lambda.MoveFilterB((byte)position, 5);
        }


        public int GetSequenceCount()
        {
            return (int)sequenceCounter.ElapsedMilliseconds; 
        }


        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////

        private void TempMonitorWorker(IProgress<TemperatureProgressReport> progress)  // this is run on a separate Task
        {
            m_tempMonitorRunning = true;
            int t = 0;
            while (true)
            {
                if (m_camera.SystemInitialized)
                {
                    var ok = m_camera.GetCoolerTemp(ref t);
                    if (ok)
                    {
                        m_camera.CameraTemperature = t;

                        progress.Report(new TemperatureProgressReport(true, t));

                        if (m_cameraTemperatureCancelToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    else
                    {
                        // camera did not successfully report temperature
                        progress.Report(new TemperatureProgressReport(false, t));
                    }
                }
                else
                {
                    // camera not initialized
                    progress.Report(new TemperatureProgressReport(false, 0));
                }
                Thread.Sleep(1000);
            }
            m_tempMonitorRunning = false;
        }


        private void ReportTemperature(TemperatureProgressReport progress)
        {
            OnTemperatureEvent(new TemperatureEventArgs(progress.GoodReading, progress.Temperature));
        }



        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////


        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        
        


        public async void StartImaging_2(ImagingParameters iParams,            
            CancellationToken cancelToken,
            Progress<int> progress, WG_Color[] colorMap,
            ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> displayPipeline,
            ITargetBlock<Tuple<ushort[], int, int, int>> storagePipeline = null,
            ITargetBlock<ushort[]> histogramPipeline = null,
            ITargetBlock<Tuple<ushort[], int, int>> analysisPipeline = null
            )
        {
            // the imageDisplay and chartArray parameters are a little confusing.  You either supply one or the other.  If you have a chartArray that the images
            // are displayed to, then supply chartArray and set imageDisplay = null.  If you're not using a chartArray, but simply want to display to 
            // and ImageDisplay, then supply an imageDisplay and set chartArray = null;

            int imageCount = 0;


            /// Start Imaging Task 
            Task<int> ImagingTask = Task.Factory.StartNew<int>(() => ImageReader_worker(iParams,
                progress,
                cancelToken,
                colorMap,
                displayPipeline,
                storagePipeline,
                histogramPipeline,
                analysisPipeline),
                cancelToken);


            try
            {
                imageCount = await ImagingTask;
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

                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs(sb.ToString(), false));
            }
            catch (OperationCanceledException)
            {
                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs("Imaging Cancelled", false));
            }
            catch (Exception ex)
            {
                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs(ex.Message, false));
            }
            finally
            {
                ImagingTask.Dispose();
            }

            ((IProgress<int>)progress).Report(imageCount);
        }








        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////



        public async void StartImaging(ImagingParameters iParams, 
            Tuple<MaskContainer,ObservableCollection<Tuple<int,int>>,int,int,int> analysisParams,
            CancellationToken cancelToken, TaskScheduler uiTask, 
            Progress<int> progress, ImageDisplay imageDisplay, WG_Color[] colorMap,  
            Histogram histogram, ChartArray chartArray = null)
        {
            // the imageDisplay and chartArray parameters are a little confusing.  You either supply one or the other.  If you have a chartArray that the images
            // are displayed to, then supply chartArray and set imageDisplay = null.  If you're not using a chartArray, but simply want to display to 
            // and ImageDisplay, then supply an imageDisplay and set chartArray = null;


            bool saveImages = false;
            int imageCount = 0;
            

            if (iParams.ExperimentIndicatorID != null)
                if (iParams.ExperimentIndicatorID.Length > 0)
                    if (iParams.ExperimentIndicatorID[0] > 0)
                        saveImages = true;

            ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> displayPipeline;

            if (chartArray == null && imageDisplay != null)// ImageDisplay Supplied
            {
                // create a hashtable for the image display
                Dictionary<int,ImageDisplay> idDictionary = new Dictionary<int,ImageDisplay>();
                idDictionary.Add(iParams.ExperimentIndicatorID[0], imageDisplay);
                                
                displayPipeline = CreateDisplayPipeline(uiTask, idDictionary);
            }
            else if (chartArray != null)  // ChartArray supplied
            {
                Dictionary<int, ImageDisplay> idDictionary = chartArray.GetImageDisplayDictionary();
                
                displayPipeline = CreateDisplayPipeline(uiTask, idDictionary);
                
            }
            else  // neither an ImageDisplay or ChartArray were supplied
                return;



            ITargetBlock<Tuple<ushort[], int, int, int>> storagePipeline = null;

            ITargetBlock<ushort[]> histogramPipeline = null;

            ITargetBlock<Tuple<ushort[], int, int>> analysisPipeline = null;

            if (saveImages)
            {
                storagePipeline = CreateImageStoragePipeline(GlobalVars.CompressionAlgorithm,iParams.imageWidth,iParams.imageHeight);
            }

            if(chartArray!=null && analysisParams!=null)
            {
                MaskContainer mask = analysisParams.Item1;
                ObservableCollection<Tuple<int,int>> controlWells = analysisParams.Item2;
                int numFoFrames = analysisParams.Item3;
                int dynamicRatioNumeratorID = analysisParams.Item4;
                int dynamicRatioDenominatorID = analysisParams.Item5;

                analysisPipeline = CreateAnalysisPipeline(chartArray, mask, iParams.imageWidth,
                    iParams.imageHeight,iParams.HorzBinning,iParams.VertBinning,
                    iParams.ExperimentIndicatorID,controlWells,numFoFrames,
                    dynamicRatioNumeratorID, dynamicRatioDenominatorID);
            }

            if(histogram != null)
            {               
                histogramPipeline = CreateHistogramPipeline(uiTask, histogram);
            }

            
            /// Start Imaging Task 
            Task<int> ImagingTask = Task.Factory.StartNew<int>(() => ImageReader_worker(iParams, 
                progress, 
                cancelToken, 
                colorMap, 
                displayPipeline, 
                storagePipeline, 
                histogramPipeline,
                analysisPipeline), 
                cancelToken);


            try
            {
                imageCount = await ImagingTask;
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

                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs(sb.ToString(), false));
            }
            catch (OperationCanceledException)
            {
                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs("Imaging Cancelled", false));
            }
            catch (Exception ex)
            {
                m_imagerRunning = false;
                OnCameraEvent(new CameraEventArgs(ex.Message, false));
            }
            finally
            {
                ImagingTask.Dispose();                
            }

            ((IProgress<int>)progress).Report(imageCount);
        }



        /// <summary>
        /// ///////////////////////////////////////////////////////////////////////////
        /// ImageReader_worker()
        /// This method runs in a separate thread to acquire images from the camera
        /// imagingParams must be fully set up and passed into this function
        /// Set storagePipeline = null if you don't want to save images
        /// </summary> 
        /// 
        public int ImageReader_worker(ImagingParameters imagingParams, IProgress<int> progress, CancellationToken cancelToken, WG_Color[] colorMap,
                                        ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> displayPipeline,
                                        ITargetBlock<Tuple<ushort[], int, int, int>> storagePipeline,
                                        ITargetBlock<ushort[]> histogramPipeline,
                                        ITargetBlock<Tuple<ushort[], int, int>> analysisPipeline)
        {
            m_imagerRunning = true;

            int imageCount = 0;
            bool aborted = false;
           

            OnCameraEvent(new CameraEventArgs("Imaging Started", m_imagerRunning));
     
            
            /////////////////////////////////////////////////
            // TODO:  Configure Camera

            uint ecode;
            int indicatorIndex = 0;
            uint TotalPixels;
            int imageTimeStamp;

            // make sure no acquisition is currently running
            //m_camera.AbortAcquisition();


            long frequency = Stopwatch.Frequency;  // timer frequency in ticks/second


            //////////////////////////// SET UP CAMERA

            // initialize camera

            if (!m_camera.SystemInitialized) m_camera.Initialize();
            ecode = m_camera.MyCamera.SetShutter(1, 1, 0, 0);

            ecode = m_camera.MyCamera.SetReadMode(4); // image mode

            // configure binning and image area: part of the ccd taken as the image
            ecode = m_camera.MyCamera.SetImage(imagingParams.HorzBinning, imagingParams.VertBinning,
                                               imagingParams.Image_StartCol, imagingParams.Image_EndCol,
                                               imagingParams.Image_StartRow, imagingParams.Image_EndRow);

            ecode = m_camera.MyCamera.SetADChannel(0);

            // TEMP CHANGE - BG 8/7/14, trying to see if this removes some spurrious noise in image readout
            //    VS Speeds (index = speed): 0 = 0.9, 1 = 1.7, 2 = 3.3, 3 = 6.5, 4 = 12.9
            //    HS Speeds (index = speed): 0 = 10, 1 = 5, 2 = 3

            int VSindex = 3;
            int HSindex = 2;


            // SetHSSpeed: first parameter = type (0 for EMCCD), second parameter only seems to work if set to 1
            ecode = m_camera.MyCamera.SetHSSpeed(0, HSindex);

            int index = 0;
            float speed = 0.0f;
            ecode = m_camera.MyCamera.GetFastestRecommendedVSSpeed(ref index, ref speed);

            ecode = m_camera.MyCamera.SetVSSpeed(VSindex);

            ecode = m_camera.MyCamera.PrepareAcquisition();

            // SetAcquisitionMode: Single Scan(1), Accumulate(2), kinetic(3), fast kinetic(4), run till abort(5)
            ecode = m_camera.MyCamera.SetAcquisitionMode(5);

            // SetRingExposuretimes: set up the exposure time for each group of images to take
            ecode = m_camera.MyCamera.SetRingExposureTimes(imagingParams.NumIndicators, imagingParams.Exposure);

            ecode = m_camera.MyCamera.SetTriggerMode(10);                          // internal trigger(0), software trigger(10)

            ecode = m_camera.MyCamera.SetPreAmpGain(2);

            ecode = m_camera.MyCamera.SetEMGainMode(3);


            ///////////////////////////////

            uint cols = (uint)((imagingParams.Image_EndCol - imagingParams.Image_StartCol + 1) / imagingParams.HorzBinning);
            uint rows = (uint)((imagingParams.Image_EndRow - imagingParams.Image_StartRow + 1) / imagingParams.VertBinning);
            TotalPixels = (uint)(cols * rows);


            // initialize filter positions for first image           
            m_lambda.MoveFilterAB(imagingParams.ExcitationFilter[indicatorIndex],
                                  imagingParams.EmissionFilter[indicatorIndex],
                                  imagingParams.ExcitationFilterChangeSpeed,
                                  imagingParams.EmissionFilterChangeSpeed);

            // initialize camera gain for first image
            ecode = m_camera.MyCamera.SetEMCCDGain(imagingParams.Gain[indicatorIndex]);


            // initialize light source
            m_thor.SetIntensity((byte)imagingParams.LightIntensity);

            // open shutters
            if (!imagingParams.SyncExcitationFilterWithImaging)
            {
                m_lambda.OpenShutterA();
                //m_lambda.OpenShutterB(); FilterWheelB doesn't have a shutter
            }



            // Define an array with two AutoResetEvent WaitHandles.
            WaitHandle[] eventHandle = new WaitHandle[] 
            {
                new AutoResetEvent(false),
                new AutoResetEvent(false)
            };

            ecode = m_camera.MyCamera.SetPCIMode(1, 1);

            ecode = m_camera.MyCamera.SetCameraStatusEnable(1);

            ecode = m_camera.MyCamera.SetAcqStatusEvent(eventHandle[0].SafeWaitHandle.DangerousGetHandle());

            ecode = m_camera.MyCamera.SetDriverEvent(eventHandle[1].SafeWaitHandle.DangerousGetHandle());

            ///////////////////////////////////////////////////////////////////////////////////// 

            int eventNumber = 0;

            imageCount = 0;

            int prevNdx = 0;

            int maxWaitDuration = Timeout.Infinite;

            // create structure to hold the new image  
            var dimensions = new Tuple<ushort, ushort>((ushort)cols, (ushort)rows);
            var detectordimensions = new Tuple<ushort, ushort>((ushort)m_camera.XPixels, (ushort)m_camera.YPixels);
            var binning = new Tuple<ushort, ushort>((ushort)imagingParams.VertBinning, (ushort)imagingParams.HorzBinning);
            var offset = new Tuple<ushort, ushort>((ushort)(imagingParams.Image_StartCol - 1), (ushort)(imagingParams.Image_StartRow - 1));
            bool clipped = false;
            if (m_camera.XPixels == imagingParams.HorzBinning * cols &&
                m_camera.YPixels == imagingParams.VertBinning * rows)
            {
                clipped = true;
            }

            if (resetTimer)
            {
                sequenceCounter.Restart();  // reset the timestamp counter
            }
            ecode = m_camera.MyCamera.StartAcquisition();


            // Start Imaging Loop
            while (!aborted)
            {

                // max wait for Exposure Finished event is exposure time plus 30 msec
                maxWaitDuration = (int)(imagingParams.Exposure[indicatorIndex] * 1000) + 30;

                imageTimeStamp = (int)sequenceCounter.ElapsedMilliseconds;


                // TEMP EDIT BG 9/4/2014 Comment out to add the following line that always opens shutter A
                //Open Shutter if running in sync mode
                //if (m_imagingParams.SyncExcitationFilterWithImaging && m_imagingParams.LampShutterIsOpen[labelIndex])
                //{
                //    m_lambda.OpenShutterA();
                //}
                //m_lambda.OpenShutterB();FilterWheelB doesn't have a shutter

                m_lambda.OpenShutterA();

                /////////////////////////////////////////////////////////////////////////////////
                // acquire image
                ///////////////////////////

                // send trigger to start an acquisition
                ecode = m_camera.SendSoftwareTrigger();

                // Wait for Exposure Delay event or Timeout (waits for camera to finish the exposure)
                eventNumber = WaitHandle.WaitAny(eventHandle, maxWaitDuration, false);  // eventNumber should be 0, unless timeout occurs (in that case, it's -1)
                //Close Shutter if running in sync mode
                /*if (eventNumber != 0)
                {
                    PostError("Camera Exposure Delay Event Timed out");
                }*/
                while (((int)sequenceCounter.ElapsedMilliseconds - imageTimeStamp) < (imagingParams.Exposure[indicatorIndex] * 1000))
                {
                    Thread.Sleep(1);
                }

                // TEMP EDIT  BG 9/4/2014 testing if shutter command interferes with filter change command
                //m_lambda.CloseShutterA();  
                //m_lambda.CloseShutterB(); // not necessary.  our system doesn't have this shutter

                // Adjust Filters and Light to next setting
                prevNdx = indicatorIndex;
                indicatorIndex++;
                if (indicatorIndex == imagingParams.NumIndicators) indicatorIndex = 0;

                // TEMP EDIT BG 9/5/2014 change to add close shutter A to batch command to filter controller
                m_lambda.MoveFilterABandCloseShutterA(imagingParams.ExcitationFilter[indicatorIndex],
                              imagingParams.EmissionFilter[indicatorIndex],
                              imagingParams.ExcitationFilterChangeSpeed,
                              imagingParams.EmissionFilterChangeSpeed);


                //  check to see if camera is EMCCD, and if so, do the following 
                if (m_camera.IsEMCCD)
                {
                    ecode = m_camera.MyCamera.SetEMCCDGain(imagingParams.Gain[indicatorIndex]);

                    // Wait for Acquisition Finished Event (this event happens when the camera is finished unloading the 
                    // data from the CCD, and thus is ready to read from the camera)
                    //////////////////////////////////////////////////////////////////////////////////////////////////////
                    // This event doesn't seem to happen for the sCMOS, but is needed for the EMCCD
                    eventNumber = WaitHandle.WaitAny(eventHandle, Timeout.Infinite, false);
                    //////////////////////////////////////////////////////////////////////////////////////////////////////
                    /*if (eventNumber != 0)
                    {
                        lock (AbortLocker) _abort = true;
                        m_camera.MyCamera.AbortAcquisition();
                       PostError("Camera Set Gain after exposure Timed out");
                    }*/
                }

                // prepare for image data                
                ushort[] newImage = new ushort[TotalPixels]; 

                // get the image data
                ecode = m_camera.MyCamera.GetOldestImage16(newImage, TotalPixels);


                // post to Display Pipeline
                displayPipeline.Post(Tuple.Create(newImage,imagingParams.ExperimentIndicatorID[prevNdx],
                                                  (int)cols, (int)rows, colorMap));

                
                // check to see if imaging was aborted
                if (cancelToken.IsCancellationRequested)
                {
                    aborted = true;
                }
                
                // post to Storage Pipeline
                if (storagePipeline != null)
                {                    
                    storagePipeline.Post(Tuple.Create(newImage, 
                                                      imagingParams.ExperimentIndicatorID[prevNdx],
                                                      imageTimeStamp, (int)(imagingParams.Exposure[prevNdx] * 1000)));
                }

                // post to Histogram Pipeline
                if(histogramPipeline != null)
                {
                    histogramPipeline.Post(newImage);
                }

                // post to Analysis Pipeline: image, indicatorID, time
                if(analysisPipeline != null)
                {
                    analysisPipeline.Post(Tuple.Create(newImage, 
                                                       imagingParams.ExperimentIndicatorID[prevNdx], 
                                                       imageTimeStamp)); ;
                }


                if (!aborted)
                {                   
                    imageCount++;
                }



                /////////////////////////////////////////////////////////////////////////////////
                // logic to determine when to stop

                // check to see if all images have been taken
                if (imageCount >= imagingParams.NumImages)
                {
                    aborted = true;
                    break;
                }


                /////////////////////////////////////////////////////////////////////////////////
                // delay if necessary to get the specified cycle time (time between successive images)
                // sleep for most of the remaining time

                int delay = (int)(imagingParams.CycleTime[prevNdx]) - ((int)sequenceCounter.ElapsedMilliseconds - imageTimeStamp);
                if (delay > 5) Thread.Sleep(delay - 5);

                // tight loop for the last few msecs
                delay = (int)(imagingParams.CycleTime[prevNdx]) - ((int)sequenceCounter.ElapsedMilliseconds - imageTimeStamp);
                while (delay > 0)
                {
                    delay = (int)(imagingParams.CycleTime[prevNdx]) - ((int)sequenceCounter.ElapsedMilliseconds - imageTimeStamp);
                }


            } // END Imaging Loop


            // imaging finished                    
           
            m_camera.MyCamera.AbortAcquisition();

            // close the light source shutter
            if (!imagingParams.SyncExcitationFilterWithImaging)
            {
                m_lambda.CloseShutterA();
                //m_lambda.CloseShutterB();FilterWheelB doesn't have a shutter
            }

            m_imagerRunning = false;
            OnCameraEvent(new CameraEventArgs("Imaging Started", m_imagerRunning));

         
            return imageCount;

        }  // END ImageReader_worker()





        /// <summary>
        /// Convenience function for taking quick images of with given parameters
        /// </summary>
        /// <param name="hBinning">Horizontal Binning</param>
        /// <param name="vBinning">Vertical Binning</param>
        /// <param name="cropStartCol">Start Column Pixel for cropped image</param>
        /// <param name="cropEndCol">End Column Pixel for cropped image</param>
        /// <param name="cropStartRow">Start Row Pixel for cropped image</param>
        /// <param name="cropEndRow">End Row Pixel for cropped image</param>
        /// <param name="exposure">Exposure time in seconds</param>
        /// <param name="gain">Gain of amplifier</param>
        /// <param name="excitationFilter">position for the excitation filter</param>
        /// <param name="emissionFilter">position for the emission filter</param>
        /// <returns></returns>
        public void TakeSnapShot( out ushort[] grayImage,                       
            int exposure,
            int gain,
            int hBinning,
            int vBinning,
            int excitationFilter,
            int emissionFilter)
        {
            grayImage = null;


            if (!m_ImagerInitialized)
            {
                CameraEventArgs e = new CameraEventArgs("Camera Not Initialized", false);
                OnCameraEvent(e);
                return;
            }


            /////////////////////////////////////////////////
            // TODO:  Configure Camera

            uint ecode;
            uint TotalPixels;

            // make sure no acquisition is currently running
            //m_camera.AbortAcquisition();

            //////////////////////////// SET UP CAMERA

            // initialize camera

            if (!m_camera.SystemInitialized) m_camera.Initialize();
            ecode = m_camera.MyCamera.SetShutter(1, 1, 0, 0);

            ecode = m_camera.MyCamera.SetReadMode(4); // image mode

            // configure binning and image area: part of the ccd taken as the image
            ecode = m_camera.MyCamera.SetImage(hBinning, vBinning, 1, 
                GlobalVars.PixelWidth, 1, GlobalVars.PixelHeight);                                               

            ecode = m_camera.MyCamera.SetADChannel(0);


            // TEMP CHANGE - BG 8/7/14, trying to see if this removes some spurrious noise in image readout
            //    VS Speeds (index = speed): 0 = 0.9, 1 = 1.7, 2 = 3.3, 3 = 6.5, 4 = 12.9
            //    HS Speeds (index = speed): 0 = 10, 1 = 5, 2 = 3

            int VSindex = 3;
            int HSindex = 2;


            // SetHSSpeed: first parameter = type (0 for EMCCD), second parameter only seems to work if set to 1
            ecode = m_camera.MyCamera.SetHSSpeed(0, HSindex);

            int index = 0;
            float speed = 0.0f;
            ecode = m_camera.MyCamera.GetFastestRecommendedVSSpeed(ref index, ref speed);

            ecode = m_camera.MyCamera.SetVSSpeed(VSindex);


            ecode = m_camera.MyCamera.PrepareAcquisition();

            // SetAcquisitionMode: Single Scan(1), Accumulate(2), kinetic(3), fast kinetic(4), run till abort(5)
            ecode = m_camera.MyCamera.SetAcquisitionMode(5);

            // SetRingExposuretimes: set up the exposure time for each group of images to take
            var exposures = new float[16];
            for (int e = 0; e < exposures.Length; e++)
            {
                exposures[e] = 0.050f;
            }
            exposures[0] = (float)exposure/1000;

            ecode = m_camera.MyCamera.SetRingExposureTimes(1, exposures);

            ecode = m_camera.MyCamera.SetTriggerMode(10);                          // internal trigger(0), software trigger(10)

            ecode = m_camera.MyCamera.SetPreAmpGain(2);

            ecode = m_camera.MyCamera.SetEMGainMode(3);


            ///////////////////////////////

            TotalPixels = (uint)((GlobalVars.PixelWidth / hBinning) * (GlobalVars.PixelHeight / vBinning));


            // initialize filter positions for first image           
            m_lambda.MoveFilterAB((byte)excitationFilter,
                                  (byte)emissionFilter,
                                  (byte)GlobalVars.FilterChangeSpeed,
                                  (byte)GlobalVars.FilterChangeSpeed);

            // initialize camera gain for first image
            ecode = m_camera.MyCamera.SetEMCCDGain(gain);


            // initialize light source
            m_thor.SetIntensity((byte)100);

            // open shutters
            m_lambda.OpenShutterA();



            // Define an array with two AutoResetEvent WaitHandles.
            WaitHandle[] eventHandle = new WaitHandle[] 
            {
                new AutoResetEvent(false),
                new AutoResetEvent(false)
            };

            ecode = m_camera.MyCamera.SetPCIMode(1, 1);

            ecode = m_camera.MyCamera.SetCameraStatusEnable(1);

            ecode = m_camera.MyCamera.SetAcqStatusEvent(eventHandle[0].SafeWaitHandle.DangerousGetHandle());

            ecode = m_camera.MyCamera.SetDriverEvent(eventHandle[1].SafeWaitHandle.DangerousGetHandle());

            ///////////////////////////////////////////////////////////////////////////////////// 

            int eventNumber = 0;

            int maxWaitDuration = Timeout.Infinite;
      
            ecode = m_camera.MyCamera.StartAcquisition();
            

            // max wait for Exposure Finished event is exposure time plus 30 msec
            maxWaitDuration = exposure + 50; 
           

            // send trigger to start an acquisition
            ecode = m_camera.SendSoftwareTrigger();

            // Wait for Exposure Delay event or Timeout (waits for camera to finish the exposure)
            eventNumber = WaitHandle.WaitAny(eventHandle, maxWaitDuration, false);  // eventNumber should be 0, unless timeout occurs (in that case, it's -1)

            if (eventNumber != 0)
            {
                System.Diagnostics.Trace.WriteLine("Camera Exposure Timed out");
            }

            //  check to see if camera is EMCCD, and if so, do the following 
            if (m_camera.IsEMCCD)
            {  
                // Wait for Acquisition Finished Event (this event happens when the camera is finished unloading the 
                // data from the CCD, and thus is ready to read from the camera)
                //////////////////////////////////////////////////////////////////////////////////////////////////////
                // This event doesn't seem to happen for the sCMOS, but is needed for the EMCCD
                eventNumber = WaitHandle.WaitAny(eventHandle, maxWaitDuration * 10, false);
                //////////////////////////////////////////////////////////////////////////////////////////////////////
                if (eventNumber != 0)
                {
                    System.Diagnostics.Trace.WriteLine("Camera Set Gain after exposure Timed out");
                }
            }

            // Acquire memory to hold image data                
            grayImage = new ushort[TotalPixels];
         
            // get the image
            ecode = m_camera.MyCamera.GetOldestImage16(grayImage, TotalPixels);

            m_camera.MyCamera.AbortAcquisition();

            // close the light source shutter
            m_lambda.CloseShutterA();
                        
        }








        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////






        public uint AcquireImage(int exposure, int gain, int hBinning, int vBinning, out ushort[] grayImage)
        { // exp is the exposure time in seconds

            if(!m_camera.SystemInitialized) m_camera.Initialize();

            uint uiErrorCode;
            uint ecode;
            int status = 0;
            grayImage = null;
      
            // set exposure time in seconds
            //m_camera.MyCamera.SetAcquisitionMode(1);

            ecode = m_camera.MyCamera.SetShutter(1, 1, 0, 0);
            ecode = m_camera.MyCamera.SetReadMode(4); // image mode
            ecode = m_camera.MyCamera.SetImage(hBinning, vBinning, 1, GlobalVars.PixelWidth, 1, GlobalVars.PixelHeight);
            ecode = m_camera.MyCamera.SetADChannel(0);

            ecode = m_camera.MyCamera.SetExposureTime((float)(exposure / 1000));
            ecode = m_camera.MyCamera.PrepareAcquisition();
            ecode = m_camera.MyCamera.StartAcquisition();

            while (status != 20073) //m_camera.AndorSDK.DRV_IDLE)
            {
                uiErrorCode = m_camera.MyCamera.GetStatus(ref status);
            }

            //uiErrorCode = m_camera.MyCamera.WaitForAcquisition();            

            // if good acquisition occurred, get image
            uiErrorCode = m_camera.MyCamera.GetStatus(ref status);
            if (status == 20073)
            {
                uint TotalPixels;
                TotalPixels = (uint)((GlobalVars.PixelWidth/hBinning) * (GlobalVars.PixelHeight/vBinning));
                grayImage = new ushort[TotalPixels];                
                uiErrorCode = m_camera.MyCamera.GetAcquiredData16(grayImage, TotalPixels);
                //uiErrorCode = m_camera.MyCamera.GetOldestImage16(grayImage, TotalPixels);
            }

            return uiErrorCode;
        }





        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////



        public ITargetBlock<Tuple<ushort[], int, int, int, WG_Color[]>> CreateDisplayPipeline(TaskScheduler uiTask, Dictionary<int,ImageDisplay> imageDisplayDictionary)
        {
            // imageDisplayHash - key=ExperimentIndicatorID(int), value=ImageDisplay

            var degreeOfParallelism = 8; // Environment.ProcessorCount / 2;

            while (degreeOfParallelism > Environment.ProcessorCount / 2) degreeOfParallelism /= 2;

            if (degreeOfParallelism < 1) degreeOfParallelism = 1;

            Dictionary<int,ImageDisplay> m_imageDisplayDictionary = imageDisplayDictionary;



            // convertToColor - convert grayImage to a color image using the given colorMap
            //  input: grayImage (ushort[]), ExperimentIndicatorID (int), image width (int), image height (int),
            //         and colorMap (WG_Color[])
            // output: width (int), height (int), colorImage (WG_Color[]), grayImage (ushort[]), 
            //         and ExperimentIndicatorID (int)
            var convertToColor = new TransformBlock<Tuple<ushort[], int, int, int, WG_Color[]>, Tuple<int, int, byte[], ushort[], int>>(inputData =>
            {
                ushort[] grayImage = inputData.Item1;
                int experimentIndicatorID = inputData.Item2;
                int width = inputData.Item3;
                int height = inputData.Item4;
                WG_Color[] colorMap = inputData.Item5;

                ImageDisplay imageDisplay = m_imageDisplayDictionary[experimentIndicatorID];

                if (imageDisplay.m_colorMap[0] != null)
                {
                    colorMap = imageDisplay.m_colorMap;
                }


                try
                {                    

                    byte[] colorImage = new byte[width * height * 4];

                    var grayImageLength = grayImage.Length;

                    int maxColorIndex = colorMap.Length - 1;

                    var tasks = new Task[degreeOfParallelism];

                    byte[] localColorImage = colorImage;

                    for (int taskNumber = 0; taskNumber < degreeOfParallelism; taskNumber++)
                    {
                        // capturing taskNumber in lambda wouldn't work correctly
                        int taskNumberCopy = taskNumber;

                        tasks[taskNumber] = Task.Factory.StartNew(() =>
                        {
                            var max = grayImageLength * (taskNumberCopy + 1) / degreeOfParallelism;

                            for (int i = grayImageLength * taskNumberCopy / degreeOfParallelism; i < max; i++)
                            {
                                int colorIndex = grayImage[i];

                                if (colorIndex > maxColorIndex) colorIndex = maxColorIndex;

                                int pos = i * 4;

                                colorImage[pos] = colorMap[colorIndex].m_blue;       // blue
                                colorImage[pos + 1] = colorMap[colorIndex].m_green;  // green
                                colorImage[pos + 2] = colorMap[colorIndex].m_red;    // red
                                colorImage[pos + 3] = colorMap[colorIndex].m_alpha;  // alpha
                            }
                        });
                    }

                    Task.WaitAll(tasks);

                
                    /////////////////////////////////////////

                    return Tuple.Create(width, height, colorImage, grayImage, experimentIndicatorID);

                }
                catch (OperationCanceledException)
                {
                    return null;
                } 
            },
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 8
               });



            // displayColorImage - create dataflow block for image display
            //  input: image width (int), image height (int), colorImage (byte[]), grayImage (ushort[]),
            //         ExperimentIndicatorID (int)
            var displayColorImage = new ActionBlock<Tuple<int, int, byte[], ushort[], int>>(inputData =>
            {
                int width = inputData.Item1;
                int height = inputData.Item2;
                byte[] colorImage = inputData.Item3;
                ushort[] grayImage = inputData.Item4;
                int experimentIndicatorID = inputData.Item5;

                try
                {
                    ImageDisplay imageDisplay = m_imageDisplayDictionary[experimentIndicatorID];

                    Int32Rect rect = new Int32Rect(0, 0, width, height);
                    imageDisplay.m_imageBitmap.Lock();
                    imageDisplay.m_imageBitmap.WritePixels(rect, colorImage, 4 * width, 0);
                    imageDisplay.m_imageBitmap.Unlock();

                    // copy raw image to ImageDisplay control so that user can adjust colorModel and see results
                    Buffer.BlockCopy(grayImage, 0, imageDisplay.m_grayImage, 0, width * height * sizeof(ushort));
                    imageDisplay.SetHasImage(true);
                }
                catch (OperationCanceledException)
                {
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   TaskScheduler = uiTask,
                   MaxDegreeOfParallelism = 1
               });



            // link blocks
            convertToColor.LinkTo(displayColorImage, colorImage => colorImage != null);


            // return head of display pipeline
            return convertToColor;

        }





        public ITargetBlock<ushort[]> CreateHistogramPipeline(TaskScheduler uiTask, Histogram histogram)
        {

            // create dataflow block for building histogram 
            var buildHistogram = new TransformBlock<ushort[],bool>(grayImage =>
            {
                try
                {
                    if (histogram != null)
                    {
                        histogram.BuildImageHistogram(grayImage);
                    }

                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {                  
                   MaxDegreeOfParallelism = 1
               });



            // create dataflow block for displaying histogram 
            var displayHistogram = new ActionBlock<bool>(statusOK =>
            {
                try
                {
                    if (statusOK)
                    {
                        histogram.DrawHistogram();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   TaskScheduler = uiTask,
                   MaxDegreeOfParallelism = 1
               });


             // link blocks
            buildHistogram.LinkTo(displayHistogram);



            return buildHistogram;
        }









        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////




        public ITargetBlock<Tuple<ushort[],int,int,int>>CreateImageStoragePipeline(COMPRESSION_ALGORITHM compAlgorithm, int pixelWidth, int pixelHeight)
        {           
            COMPRESSION_ALGORITHM m_compAlgorithm = compAlgorithm;
            int m_pixelWidth = pixelWidth;
            int m_pixelHeight = pixelHeight;
            int m_byteDepth = 2;

            // input: grayimage (ushort[]), ExperimentIndicatorID (int), time (int), exposure (int)            
            var storeImage = new ActionBlock<Tuple<ushort[],int,int,int>>(inputData =>
            {
                ushort[] grayImage = inputData.Item1;
                int expIndicatorID = inputData.Item2;
                int time = inputData.Item3;
                int exposure = inputData.Item4;
                int width = m_pixelWidth;
                int height = m_pixelHeight;
                int depth = m_byteDepth;

                try
                {
                    ExperimentImageContainer expImage = new ExperimentImageContainer();
                    expImage.CompressionAlgorithm = m_compAlgorithm;
                    expImage.Depth = depth;
                    expImage.ExperimentIndicatorID = expIndicatorID;
                    expImage.Exposure = exposure;
                    expImage.Width = width;
                    expImage.Height = height;
                    expImage.ImageData = grayImage;
                    expImage.MaxPixelValue = GlobalVars.MaxPixelValue;
                    expImage.MSecs = time;
                    expImage.NumBytes = grayImage.Length * depth;
                    expImage.TimeStamp = DateTime.Now;

                    WaveguideDB wgDB = new WaveguideDB();

                    bool success = wgDB.InsertExperimentImage(ref expImage);

                }
                catch (OperationCanceledException)
                {

                }
            },
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 8
               });


            // return head of storage pipeline
            return storeImage;

        }





        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////



        public ITargetBlock<Tuple<ushort[], int, int>> CreateAnalysisPipeline(ChartArray chartArray,
                 MaskContainer mask, int pixelWidth, int pixelHeight, int hBinning, int vBinning,
                 int[] indicatorIdList, ObservableCollection<Tuple<int,int>> controlWells, 
                 int numFoFrames, int dynamicRatioNumeratorID, int dynamicRatioDenominatorID)
        {
            // perform pre-processing of mask.  This creates a 2D array with an array element for each mask aperture.  The idea
            // is that for each aperture, create an array of the pixels inside that aperture.  This is only done once, when the 
            // AnalysisPipeline is created.  Thus all that is required to find the sum of pixels within an aperture is to go to
            // that aperture's array of pixels and get the value for each one of those pixels, adding to the sum.  
            //
            // int pixelList[mask.rows, mask.cols][numPixels] will contain the pixel list for each aperture.
            //
            // for example, to sum all pixels in aperture [1,1], you would do this:
            //
            //     foreach(int ndx in pixelList[1,1]) sum[1,1] += grayImage[ndx];  NOTE: grayImage is the raw image from the camera and is a 1D array

            // now...preprocess the mask, i.e. create pixelList[mask.rows,mask.cols][numPixels]

            mask.BuildPixelList(pixelWidth, pixelHeight, hBinning, vBinning);

            TaskScheduler m_chartArrayTask = chartArray.GetTaskScheduler();
         
            Hashtable m_Fo_Hash = new Hashtable();
            Hashtable m_FoCount_Hash = new Hashtable();
            Hashtable m_FoReady_Hash = new Hashtable();
            Hashtable m_analysis_Hash = new Hashtable();

            int m_dynamicRatioNumeratorID = dynamicRatioNumeratorID;
            int m_dynamicRatioDenominatorID = dynamicRatioDenominatorID;
            bool m_dynamicRatioNumeratorReady = false;
            bool m_dynamicRatioDenominatorReady = false;
            float[,] m_dynamicRatioNumeratorValues = new float[mask.Rows, mask.Cols];
            float[,] m_dynamicRatioDenominatorValues = new float[mask.Rows, mask.Cols];

            WaveguideDB wgDb = new WaveguideDB();

            foreach (int indID in indicatorIdList)
            {
                float[,] Fo = new float[mask.Rows, mask.Cols];
                for (int r = 0; r < mask.Rows; r++)
                    for (int c = 0; c < mask.Cols; c++)
                    {
                        Fo[r, c] = 0.0f;
                    }
                m_Fo_Hash.Add(indID, Fo);

                int Fo_count = 0;
                m_FoCount_Hash.Add(indID, Fo_count);

                bool Fo_ready = false;
                m_FoReady_Hash.Add(indID, Fo_ready);

                AnalysisContainer anal = new AnalysisContainer();
                anal.Description = "Runtime Raw Pixel Sums";
                anal.ExperimentIndicatorID = indID;
                anal.TimeStamp = DateTime.Now;
                wgDb.InsertAnalysis(ref anal);
                m_analysis_Hash.Add(indID, anal);
            }




            // calculate the sum of all pixel values for each mask aperture.  These sums are 
            // stored in a array of mask.rows x mask.cols. 
            //      input: grayimage (ushort[]), ExperimentIndicatorID (int), time (int)
            //      output: array of raw pixel sums for each mask aperture (float[,]), ExperimentIndicatorID (int)
            //              and time (int)
            var calculateApertureSums = new TransformBlock<Tuple<ushort[], int, int>,Tuple<float[,],int,int>>(inputData =>
            {
                ushort[] grayImage = inputData.Item1;
                int expIndicatorID = inputData.Item2;
                int time = inputData.Item3;

                try
                {
                    // sum all pixels in pixelList
                    float[,] F = new float[mask.Rows,mask.Cols];

                    for (int r = 0; r < mask.Rows; r++)
                        for (int c = 0; c < mask.Cols; c++)
                        {
                            F[r,c] = 0;

                            // calculate sum of pixels inside mask aperture[r,c]
                            foreach (int ndx in mask.PixelList[r, c])
                            {
                                F[r,c] += grayImage[ndx];
                            }                            
                        }
                                        
                    return Tuple.Create(F,expIndicatorID,time);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            },
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 1
               });



            // calculate Static Ratio: F/Fo for each mask aperture.  Fo is the average of the 
            // first N frames for each mask aperture, and F is the raw pixel sum from each 
            // mask aperture.  N is set to numFoFrames, which is passed in during the creation
            // of this pipeline.
            //      input: F array (float[,]), ExperimentIndicatorID (int), and time (int)
            //      output: F array (float[,]), F/Fo array (float[,]), ExperimentIndicatorID (int),
            //              and time (int)
            var calculateStaticRatio = new TransformBlock<Tuple<float[,],int,int>,Tuple<float[,],float[,],int,int>>(inputData =>
            {
                float[,] F = inputData.Item1;
                int expIndicatorID = inputData.Item2;
                int time = inputData.Item3;

                // retrieve data relevant to this ExperimentIndicatore
                float[,] Fo = (float[,])m_Fo_Hash[expIndicatorID];
                int FoCount = (int)m_FoCount_Hash[expIndicatorID];
                bool FoReady = (bool)m_FoReady_Hash[expIndicatorID];

                float[,] staticRatio = null;

                try
                {
                    if(FoReady)
                    {
                        staticRatio = new float[mask.Rows, mask.Cols];
                        for (int r = 0; r < mask.Rows; r++)
                            for (int c = 0; c < mask.Cols; c++)
                            {
                                staticRatio[r, c] = F[r,c]/Fo[r,c];
                            }                        
                    }
                    else
                    {
                        for(int r = 0; r<mask.Rows; r++)
                            for(int c = 0; c<mask.Cols; c++)
                            {
                                Fo[r, c] += F[r, c];
                            }
                        FoCount++;
                        m_FoCount_Hash[expIndicatorID] = FoCount; // update Hashtable

                        if(FoCount >= numFoFrames)
                        {
                            FoReady = true;
                            m_FoReady_Hash[expIndicatorID] = FoReady; // update Hashtable

                            for (int r = 0; r < mask.Rows; r++)
                                for (int c = 0; c < mask.Cols; c++)
                                {
                                    Fo[r, c] /= numFoFrames;
                                }
                        }

                        staticRatio = null;
                    }

                    return Tuple.Create(F, staticRatio, expIndicatorID, time);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {                   
                   MaxDegreeOfParallelism = 1
               });







            // calculate Control Subtraction: for a selected group of wells (mask apertures), designated
            // as "control wells", find the average of the static ratio for those wells and subtract 
            // it from the static ratio of each individual mask aperture.
            //      input: F array (float[,]), F/Fo array (float[,]), ExperimentIndicatorID (int),
            //             and time(int)
            //      output: F array (float[,]), F/Fo array aka "static Ratio" (float[,]), 
            //              control subtraction array (float[,]), and ExperimentIndicatorID (int)
            var calculateControlSubtraction = new TransformBlock<Tuple<float[,],float[,], int,int>, Tuple<float[,], float[,], float[,], int,int>>(inputData =>
            {
                float[,] F = inputData.Item1;
                float[,] staticRatio = inputData.Item2;
                int expIndicatorID = inputData.Item3;
                int time = inputData.Item4;

                float avgControl = 0.0f;

                float[,] controlSubtraction = new float[mask.Rows, mask.Cols];
           
                try
                {
                    // only do this if the control wells are specified and staticRatio available
                    if (controlWells.Count() > 0 && staticRatio != null)  
                    {
                        // calculate Avg(F/Fo) for control wells
                        foreach (Tuple<int, int> well in controlWells)
                        {
                            int row = well.Item1;
                            int col = well.Item2;

                            avgControl += staticRatio[row, col];
                        }
                        avgControl /= controlWells.Count();


                        for (int r = 0; r < mask.Rows; r++)
                            for (int c = 0; c < mask.Cols; c++)
                            {
                                controlSubtraction[r, c] = staticRatio[r, c] - avgControl;
                            }
                    }
                    else
                    {
                        controlSubtraction = null;
                    }
                    
                    return Tuple.Create(F,staticRatio,controlSubtraction,expIndicatorID,time);

                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 1
               });







            // calculate Dynamic Ratio: this is the ration of the static ratios for two given 
            // indicators.  Before this block can actually create a value, it must recieve an
            // input from both indicators.  
            //      input: F array (float[,]), F/Fo array aka "static Ratio" (float[,]), 
            //              control subtraction array (float[,]), ExperimentIndicatorID (int),
            //              and time (int)
            //      output: F array (float[,]), F/Fo array aka "static Ratio" (float[,]), 
            //              control subtraction array (float[,]), dyanamic ratio array (float[,]),
            //              ExperimentIndicatorID (int), and time (int)
            var calculateDynamicRatio = new TransformBlock<Tuple<float[,], float[,], float[,], int, int>, Tuple<float[,], float[,], float[,], float[,], int, int>>(inputData =>
            {
                float[,] F = inputData.Item1;
                float[,] staticRatio = inputData.Item2;
                float[,] controlSubtraction = inputData.Item3;
                int expIndicatorID = inputData.Item4;
                int time = inputData.Item5;

                float[,] dynamicRatio = null;

                try
                {
                    if (staticRatio != null)
                    {
                        if (expIndicatorID == m_dynamicRatioNumeratorID)
                        {
                            for (int r = 0; r < mask.Rows; r++)
                                for (int c = 0; c < mask.Cols; c++)
                                {
                                    m_dynamicRatioNumeratorValues[r, c] = staticRatio[r, c];
                                }
                            m_dynamicRatioNumeratorReady = true;
                        }
                        else if (expIndicatorID == m_dynamicRatioDenominatorID)
                        {
                            for (int r = 0; r < mask.Rows; r++)
                                for (int c = 0; c < mask.Cols; c++)
                                {
                                    m_dynamicRatioDenominatorValues[r, c] = staticRatio[r, c];
                                }
                            m_dynamicRatioDenominatorReady = true;
                        }
                    }
                    
                    if(m_dynamicRatioNumeratorReady && m_dynamicRatioDenominatorReady)
                    {
                        m_dynamicRatioNumeratorReady = false;
                        m_dynamicRatioDenominatorReady = false;

                        dynamicRatio = new float[mask.Rows, mask.Cols];

                        for (int r = 0; r < mask.Rows; r++)
                            for (int c = 0; c < mask.Cols; c++)
                            {
                                dynamicRatio[r, c] = m_dynamicRatioNumeratorValues[r, c] / m_dynamicRatioDenominatorValues[r, c];
                            }
                    }
                    else
                    {
                        dynamicRatio = null;
                    }

                    return Tuple.Create(F, staticRatio, controlSubtraction, dynamicRatio, expIndicatorID, time);

                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 1
               });







            // Post the analysis results to the charting display
            //      input: F array (float[,]), F/Fo array aka "static Ratio" (float[,]),  
            //              control subtraction array (float[,]), dyanamic ratio array (float[,]),
            //              ExperimentIndicatorID (int), and time (int)
            var PostAnalysisResults = new TransformBlock<Tuple<float[,], float[,], float[,], float[,], int, int>,
                Tuple<float[,], float[,], float[,], float[,], int, int>>(inputData =>
            {
                float[,] F = inputData.Item1;
                float[,] staticRatio = inputData.Item2;
                float[,] controlSubtraction = inputData.Item3;
                float[,] dynamicRatio = inputData.Item4;
                int expIndicatorID = inputData.Item5;
                int time = inputData.Item6;

                try
                {
                    // send the data to be displayed
                    chartArray.AppendNewData(ref F, ref staticRatio, ref controlSubtraction,
                                             ref dynamicRatio, time, expIndicatorID);

                    return Tuple.Create(F, staticRatio, controlSubtraction, dynamicRatio, expIndicatorID, time);

                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception e)
                {
                    string errmsg = e.Message;
                    return null;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   TaskScheduler = m_chartArrayTask,
                   MaxDegreeOfParallelism = 8
               });






            // Post the analysis results to the charting display and store the results in the 
            // database.                          
            //      input: F array (float[,]), F/Fo array aka "static Ratio" (float[,]),  
            //              control subtraction array (float[,]), dyanamic ratio array (float[,]),
            //              ExperimentIndicatorID (int), and time (int)
            var StoreAnalysisResults = new ActionBlock<Tuple<float[,],float[,], float[,], float[,], int, int>>(inputData =>
            {
                float[,] F = inputData.Item1;
                float[,] staticRatio = inputData.Item2;
                float[,] controlSubtraction = inputData.Item3;
                float[,] dynamicRatio = inputData.Item4;
                int expIndicatorID = inputData.Item5;
                int time = inputData.Item6;


                try
                {                   
                    // write F to database
                    WaveguideDB wgDB = new WaveguideDB();

                    AnalysisContainer anal = (AnalysisContainer)m_analysis_Hash[expIndicatorID];                   
                   
                    // write the F array (raw sums of pixels in mask aperture) to the database
                    //AnalysisValueContainer aVal = new AnalysisValueContainer();
                        
                    //aVal.AnalysisID = anal.AnalysisID;
                    //aVal.SequenceNumber = time;                    

                    bool success = wgDB.InsertAnalysisFrame(anal.AnalysisID, time, F);

                    //for(int r = 0; r<mask.Rows; r++)
                    //    for(int c = 0; c<mask.Cols; c++)
                    //    {
                    //        aVal.Row = r;
                    //        aVal.Col = c;
                    //        aVal.Value = F[r, c];

                    //        bool success = wgDB.InsertAnalysisValue(ref aVal);

                    //        if (!success) break; 
                    //    }
                }
                catch (OperationCanceledException)
                {                    
                }
                catch(Exception e)
                {
                    string errmsg = e.Message;
                }
            },
                // Specify a task scheduler as that which is passed in
                // so that the action runs on the UI thread. 
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 8
               });




            // link blocks
            calculateApertureSums.LinkTo(calculateStaticRatio);
            calculateStaticRatio.LinkTo(calculateControlSubtraction);
            calculateControlSubtraction.LinkTo(calculateDynamicRatio);
            calculateDynamicRatio.LinkTo(PostAnalysisResults);
            PostAnalysisResults.LinkTo(StoreAnalysisResults);


            // return head of display pipeline
            return calculateApertureSums;
            
        } // END CreateAnalysisPipeline()




        // ///////////////////////////////////////////////////////////////////////////////////////      
        // ///////////////////////////////////////////////////////////////////////////////////////     




        public async Task<Tuple<bool,int,int,double>> OptimizeGainExposure(int gain, int exposure, double targetBrightness, int gainStep, int exposureStep, 
                                                                           int hBinning, int vBinning, int maxGain, int maxExposure, int exFilter, int emFilter,
                                                                           MaskContainer mask, CancellationToken cancelToken, 
                                                                           IProgress<Tuple<int,int,double,double>> reportProgress)
        {
            bool cancelled = false;
            bool success = false;

            ushort[] grayImage = null;
            double avgBrightness;
            double maxAvgBrightnessFound = 0;
            int bestGain = gain;
            int bestExposure = exposure;

            int Gain = gain;
            int Exposure = exposure;


            int GainStep = gainStep;
            int ExposureStep = exposureStep;

            for (Exposure = exposure; Exposure <= maxExposure; Exposure += ExposureStep)
            {

                for (Gain = gain; Gain <= maxGain; Gain += GainStep)
                {  

                    try
                    {
                        // take image
                       // await Task.Factory.StartNew(() => AcquireImage(Exposure, Gain, hBinning, vBinning, out grayImage), cancelToken);

                        await Task.Factory.StartNew(() => TakeSnapShot(out grayImage, Exposure, Gain, hBinning, vBinning, exFilter, emFilter), cancelToken);

                        // calculate brightness
                        if (grayImage != null)
                        {
                            avgBrightness = CalculateAverageBrightness(grayImage, mask);

                            reportProgress.Report(Tuple.Create(Gain, Exposure, avgBrightness, targetBrightness));

                            if (avgBrightness > maxAvgBrightnessFound)
                            {
                                maxAvgBrightnessFound = avgBrightness;
                                bestGain = Gain;
                                bestExposure = Exposure;
                            }

                            if (avgBrightness >= targetBrightness)
                            {
                                success = true;
                                gain = Gain;
                                exposure = Gain;
                                break;
                            }
                        }

                        if (Gain == 1) Gain = 0;  // this just cleans up the gain steps.  For example if Gain starts at 1 and GainStep = 5, Gain will have the 
                                                 // sequence: 1, 5, 10, 15, 20, ...    instead of: 1, 6, 11, 16, 21, ...
                    }

                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                    }
                    finally
                    {
                    }
                    
                }

                if (success || cancelled) break;
            }


            if (success)
            {
                gain = Gain;
                exposure = Exposure;
                targetBrightness = maxAvgBrightnessFound;
            }
            else
            {
                gain = bestGain;
                exposure = bestExposure;
                targetBrightness = maxAvgBrightnessFound;
            }

            return (Tuple.Create(success, gain, exposure, targetBrightness));
        }






        // ///////////////////////////////////////////////////////////////////////////////////////      
        // ///////////////////////////////////////////////////////////////////////////////////////      


      

        public double CalculateAverageBrightness(ushort[] grayImage, MaskContainer mask)
        {

            ulong sum = 0;
            double avg;

            if (mask == null)
            {
                for (int i = 0; i < grayImage.Length; i++)
                {
                    sum += grayImage[i];
                }
            }
            else
            {
                for (int r = 0; r < mask.Rows; r++)
                    for (int c = 0; c < mask.Cols; c++)
                    {
                        foreach (int ndx in mask.PixelList[r, c])
                        {
                            sum += grayImage[ndx];                            
                        }
                    }
            }

            avg = (double)(sum) / (double)grayImage.Length;

            return avg;
        }
















    } // END Imager Class





      
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////






    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////////////////////// 



    public class CameraEventArgs : EventArgs
    {
        private string _message;
        private bool _cameraImaging;

        public CameraEventArgs(string TextMessage, bool CameraBusyImaging)
        {
            _message = TextMessage;
            _cameraImaging = CameraBusyImaging;
        }

        public string Message
        {
            get { return _message; }
            set { _message = value; }
        }

        public bool CameraImaging
        {
            get { return _cameraImaging; }
            set { _cameraImaging = value; }
        }
    }


    public class TemperatureEventArgs : EventArgs
    {
        private bool _goodReading;
        private int _temperature;

        public bool GoodReading
        {
            get { return _goodReading; }
            set { _goodReading = value; }
        }

        public int Temperature
        {
            get { return _temperature; }
            set { _temperature = value; }
        }

        public TemperatureEventArgs(bool goodReading, int temperature)
        {
            GoodReading = goodReading;
            Temperature = temperature;
        }
    }




    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////






    // class containing parameters used in the kinetic imaging
    public class ImagingParameters
    {
        //public int millisecondsBetweenAcquistions;
        //public int millisecondsExposure;
        //public int numberOfAcquisitions;
        public int imageWidth;
        public int imageHeight;        
        public int maxPixelValue;

        public int NumIndicators;
        public int NumImages;

        public float[] Exposure;
        public byte[] ExcitationFilter;
        public byte[] EmissionFilter;
        public string[] IndicatorName;
        public int[] CycleTime;
        public int[] Gain;
        public bool[] LampShutterIsOpen;
        public int[] ExperimentIndicatorID;  // list of Experiment Indicator IDs, used if images are saved

        public int HorzBinning;
        public int VertBinning;
        public int LightIntensity;
        public string BravoMethodFilename;
        public int CameraTemperature;
        public byte ExcitationFilterChangeSpeed;
        public byte EmissionFilterChangeSpeed;
        public bool SyncExcitationFilterWithImaging;
        

        // define part of sensor that is used for image
        public int Image_StartCol;
        public int Image_EndCol;
        public int Image_StartRow;
        public int Image_EndRow;


        public ImagingParameters() // constructor
        { 
            NumIndicators = 1;
            NumImages = 1;

            Exposure = new float[16];
            ExcitationFilter = new byte[16];
            EmissionFilter = new byte[16];
            IndicatorName = new string[16];
            CycleTime = new int[16];
            Gain = new int[16];            
            LampShutterIsOpen = new bool[16];
            ExperimentIndicatorID = new int[16];

            HorzBinning = 1;
            VertBinning = 1;
            LightIntensity = 100;
            BravoMethodFilename = "";
            CameraTemperature = GlobalVars.CameraTargetTemperature;
            ExcitationFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            EmissionFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            SyncExcitationFilterWithImaging = true;
            
            Image_StartCol = 1;
            Image_EndCol = GlobalVars.PixelWidth;
            Image_StartRow = 1;
            Image_EndRow = GlobalVars.PixelHeight;

            for (int i = 0; i < 16; i++)
            {
                IndicatorName[i] = "Uninitialized";
                CycleTime[i] = GlobalVars.CameraDefaultCycleTime;
                Gain[i] = 1;
                ExcitationFilter[i] = 1;
                EmissionFilter[i] = 1;
                Exposure[i] = 0.500f;
                LampShutterIsOpen[i] = true;
                ExperimentIndicatorID[i] = 0;
            }
        }

    }




    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////



    public class ImagerSettings : INotifyPropertyChanged
    {

        private int _cycleTime;  // milliseconds
        private ObservableCollection<IndicatorSettings> _indicators;

        public int CycleTime
        { get { return _cycleTime; } set { _cycleTime = value; NotifyPropertyChanged("CycleTime"); } }

        public ObservableCollection<IndicatorSettings> Indicators
        { get { return _indicators; } set { _indicators = value; NotifyPropertyChanged("Indicators"); } }


        public ImagerSettings()
        {
            CycleTime = 1000;
            Indicators = new ObservableCollection<IndicatorSettings>();
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }

    }



    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////


    public class IndicatorSettings : INotifyPropertyChanged
    {
        private int _exposure; // milliseconds
        private int _emissionsFilterPosition;
        private int _excitationFilterPosition;


        public int Exposure
        { get { return _exposure; } set { _exposure = value; NotifyPropertyChanged("Exposure"); } }

        public int EmissionsFilterPosition
        { get { return _emissionsFilterPosition; } set { _emissionsFilterPosition = value; NotifyPropertyChanged("EmissionsFilterPosition"); } }

        public int ExcitationFilterPosition
        { get { return _excitationFilterPosition; } set { _excitationFilterPosition = value; NotifyPropertyChanged("ExcitationFilterPosition"); } }

        public IndicatorSettings()
        {
            Exposure = 100;
            EmissionsFilterPosition = 0;
            ExcitationFilterPosition = 0;
        }



        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }
    }



    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////


    public class TemperatureProgressReport
    {
        public bool GoodReading { get; set; }
        public int Temperature { get; set; }

        public TemperatureProgressReport(bool goodReading, int temp)
        {
            GoodReading = goodReading;
            Temperature = temp;
        }
    }







}
