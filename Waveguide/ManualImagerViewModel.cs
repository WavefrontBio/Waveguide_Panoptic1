using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;



namespace WG_Test_App
{
    
    class ManualImagerViewModel : PropertyChangedBase
    {
        private Imager imagerModel;
        private ImagingMethodViewModel advancedImagingSettings;
        private bool clipToPlateMask;

        private object lockCurrentImage;
        private WG_Queue_Image currentImage;

        private PlateMask_Record selectedPlateMask;
        private List<PlateMask_Record> plateMasks;

        private List<Tuple<Tuple<int,double>, WGImage>> imageOptimizationResults;

        PlateMaskFactoryViewModel plateMaskFactory;
        WaveGuideDB wgDB;
        IEventAggregator events;

        [ImportingConstructor]
        public ManualImagerViewModel(Imager iModel, IEventAggregator ag, WaveGuideDB db, PlateMaskFactoryViewModel maskFactory)
        {
            lockCurrentImage = new object();
            imagerModel = iModel;
            advancedImagingSettings = new ImagingMethodViewModel(db, new WaveAppSettingsViewModel(ag), ag);
            imagerModel.PostErrorEvent += PostErrorMessage;
            imagerModel.ConfigureImaging(true, false, false);//Display images but don't save or analyze them
            RunMode = ImagingRunMode.Assay;
            imagerModel.DisplayQueue.Changed += UpdateImage;
            imagerModel.m_camera.PropertyChanged += HandleCameraPropertyChanged;
            events = ag;
            wgDB = db;
            plateMaskFactory = maskFactory;
            settingUpExperiment = false;
            BravoIsRunning = false;
            currentExperiment = new Experiment_Record();
            currentPlate = new Plate_Record();
            currentProject = new Project_Record();
            plateMasks = new List<PlateMask_Record>();
            imageOptimizationResults = new List<Tuple<Tuple<int, double>, WGImage>>();
            selectedPlateMask = new PlateMask_Record();
            InitDatabaseDependentFields();
        }

        public void InitDatabaseDependentFields()
        {
            LoadPlateMasks();
            InitImagingMethod();
            advancedImagingSettings.PropertyChanged += HandleImagingMethodViewModelChanged;
            InitAnalysisMethods();
            InitWaveAnalysisMethods();
        }

        public void InitImagingMethod()
        {
            string statusMsg = "";
            imagingMethods = wgDB.GetAllImagingMethod(ref statusMsg);
            NotifyOfPropertyChange(() => ImagingMethods);
        }

        public void InitAnalysisMethods()
        {
            string statusMsg = "";
            Analysis_Record[] records;
            var n = wgDB.GetAllAnalysisTemplates(out records, ref statusMsg);
            analysisMethods = records.ToList();
            NotifyOfPropertyChange(() => AnalysisMethods);
        }

        public void InitWaveAnalysisMethods()
        {
            string statusMsg = "";
            waveAnalysisMethods = wgDB.GetAllWaveSetTransform(ref statusMsg);
            NotifyOfPropertyChange(() => WaveAnalysisMethods);
        }

        private Project_Record currentProject;
        public Project_Record CurrentProject
        {
            get { return currentProject; }
            set
            {
                currentProject = value;
                NotifyOfPropertyChange(() => CurrentProject);
            }
        }

        private Experiment_Record currentExperiment;
        public Experiment_Record CurrentExperiment
        {
            get { return currentExperiment; }
            set
            {
                currentExperiment = value;
                NotifyOfPropertyChange(() => CurrentExperiment);
            }
        }

        private Plate_Record currentPlate;
        public Plate_Record CurrentPlate
        {
            get { return currentPlate; }
            set
            {
                currentPlate = value;
                NotifyOfPropertyChange(() => CurrentPlate);
                NotifyOfPropertyChange(() => EnableRun);
            }
        }

        private List<ImagingMethod_Record> imagingMethods;
        public List<ImagingMethod_Record> ImagingMethods
        {
            get { return imagingMethods; }
        }

        private Analysis_Record selectedAnalysisMethod;
        public Analysis_Record SelectedAnalysisMethod
        {
            get { return selectedAnalysisMethod; }
            set
            {
                selectedAnalysisMethod = value;
                if (selectedAnalysisMethod != null)
                {
                    events.Publish( new WaveGuideEvents.SelectedAnalysisMethod(selectedAnalysisMethod));
                }
                NotifyOfPropertyChange(() => SelectedAnalysisMethod);
            }
        }

        private List<Analysis_Record> analysisMethods;
        public List<Analysis_Record> AnalysisMethods
        {
            get { return analysisMethods; }
        }

        private List<WaveSetTransform_Record> waveAnalysisMethods;
        public List<WaveSetTransform_Record> WaveAnalysisMethods
        {
            get { return waveAnalysisMethods; }
        }


        public int CameraTemp
        {
            get
            {
                return imagerModel.m_camera.CameraTemperature;
            }
            set
            {
                /*if (imagerModel.m_camera.SetCoolerTemp(value))
                {
                    NotifyOfPropertyChange(() => CameraTemp);
                }*/
            }
        }

        public int CameraTempSetPoint
        {
            get { return imagerModel.m_imagingParams.CameraTemperature; }
            set
            {
                imagerModel.m_imagingParams.CameraTemperature = value;
                if (imagerModel.m_camera.SetCoolerTemp(value))
                {
                    NotifyOfPropertyChange(() => CameraTemp);
                }
            }
        }

        public CameraBinning Binning
        {
            get { return imagerModel.m_imagingParams.Binning; }
            set
            {
                imagerModel.m_imagingParams.Binning = value;
                NotifyOfPropertyChange(() => Binning);
            }
        }

        private ImagingRunMode runMode;
        public ImagingRunMode RunMode
        {
            get { return runMode; }
            set
            {
                runMode = value;
                UpdateImagerModelRunMode();
                NotifyOfPropertyChange(() => RunMode);
            }
        }

        private void UpdateImagerModelRunMode()
        {
            switch (runMode)
            {
                case ImagingRunMode.Assay :
                    imagerModel.DisplayImages(true);
                    imagerModel.SaveImages(true);
                    imagerModel.AnalyzeImages(true);
                    break;
                case ImagingRunMode.BlindAssay:
                    imagerModel.DisplayImages(false);
                    imagerModel.SaveImages(true);
                    imagerModel.AnalyzeImages(true);
                    break;
                case ImagingRunMode.BlindTestAnalysis:
                    imagerModel.DisplayImages(false);
                    imagerModel.SaveImages(false);
                    imagerModel.AnalyzeImages(true);
                    break;
                case ImagingRunMode.ImageCapture:
                    imagerModel.DisplayImages(true);
                    imagerModel.SaveImages(true);
                    imagerModel.AnalyzeImages(false);
                    break;
                case ImagingRunMode.BlindImageCapture:
                    imagerModel.DisplayImages(false);
                    imagerModel.SaveImages(true);
                    imagerModel.AnalyzeImages(false);
                    break;
                case ImagingRunMode.TestAnalysis:
                    imagerModel.DisplayImages(true);
                    imagerModel.SaveImages(false);
                    imagerModel.AnalyzeImages(true);
                    break;
                case ImagingRunMode.Video:
                default:
                    imagerModel.DisplayImages(true);
                    imagerModel.SaveImages(false);
                    imagerModel.AnalyzeImages(false);
                    break;
            }
        }

        public int Acquisitions
        {
            get { return imagerModel.m_imagingParams.NumImages; }
            set
            {
                imagerModel.m_imagingParams.NumImages = value;
                NotifyOfPropertyChange(() => Acquisitions);
            }
        }

        public int ImagingPeriod
        {
            get { return imagerModel.m_imagingParams.CycleTime[0]; }
            set
            {
                imagerModel.m_imagingParams.CycleTime[0] = value;
                NotifyOfPropertyChange(() => ImagingPeriod);
            }
        }

        private float exposure;
        public float Exposure
        {
            get { return exposure; }
            set
            {
                exposure = value;
                NotifyOfPropertyChange(() => Exposure);
            }
        }

        private int gain;
        public int Gain
        {
            get { return gain; }
            set
            {
                gain = value;
                NotifyOfPropertyChange(() => Gain);
            }
        }

        private string labelName;
        public string LabelName
        {
            get { return labelName; }
            set
            {
                labelName = value;
                NotifyOfPropertyChange(() => LabelName);
            }
        }

        private string optimizedImageOutcome;
        public string OptimizedImageOutcome
        {
            get { return optimizedImageOutcome; }
            set
            {
                optimizedImageOutcome = value;
                NotifyOfPropertyChange(() => OptimizedImageOutcome);
            }
        }

        private double optimizedImageBrightness;
        public double OptimizedImageBrightness
        {
            get { return optimizedImageBrightness; }
            set
            {
                optimizedImageBrightness = value;
                NotifyOfPropertyChange(() => OptimizedImageBrightness);
            }
        }

        public bool ClipToPlateMask
        {
            get { return clipToPlateMask; }
            set
            {
                clipToPlateMask = value;
                UpdateImagerClippingOptions();
                NotifyOfPropertyChange(() => ClipToPlateMask);
            }
        }

        public ImagingMethodViewModel AdvancedImagingSettings
        {
            get { return advancedImagingSettings; }
        }

        public Imager ImagerModel
        {
            get { return imagerModel;}
        }

        public PlateMask_Record SelectedPlateMask
        {
            get { return selectedPlateMask; }
            set
            {
                selectedPlateMask = value;
                plateMaskFactory.LoadMask(selectedPlateMask);
                UpdatePlateMaskBinning();
                NotifyOfPropertyChange(() => SelectedPlateMask);
            }
        }

        public List<PlateMask_Record> PlateMasks
        {
            get { return plateMasks; }
            set { }
        }

        public void LoadPlateMasks()
        {
            PlateMask_Record[] masks;
            string message = "";
            var nmasks = wgDB.GetAllPlateMasks(out masks, ref message);
            plateMasks.Clear();
            for (int i = 0; i < masks.Length; i++)
            {
                plateMasks.Add(masks[i]);
            }
            if (plateMasks.Count > 0)
            {
                SelectedPlateMask = plateMasks[0];
                NotifyOfPropertyChange(() => PlateMasks);
            }
        }


        //This is True after Selecting the ImagingMethod and AnalysisMethod
        //Indicates that imageOptimazation Results need to be approved
        //before starting the imaging run.
        private bool settingUpExperiment;
        public bool SettingUpExperiment
        {
            get { return settingUpExperiment; }
            set
            {
                settingUpExperiment = value;
                NotifyOfPropertyChange(() => SettingUpExperiment);
            }
        }


        //BravoRunning is true after the Exposure Parameters have been set.
        private bool bravoIsRunning;

        private bool BravoIsRunning
        {
            get { return bravoIsRunning; }
            set {
                bravoIsRunning = value;
                NotifyOfPropertyChange(() => EnableAbort);
                NotifyOfPropertyChange(() => EnableRun);
            }
        }
        public bool EnableRun
        {
            get { return (!bravoIsRunning && currentPlate.m_PlateID != 0); }
        }

        public bool EnableAbort
        {
            get { return bravoIsRunning; }
        }




        /// <summary>
        /// ReOptimize Gain Exposure should always be true when changing labelsetindices
        /// </summary>
        /// <param name="labelSetIndex"></param>
        /// <param name="cycleTimeIndex"></param>
        /// <param name="reOptimizeGainExposure"></param>
        public void SetupExperiment(int labelSetIndex, int cycleTimeIndex, bool reOptimizeGainExposure)
        {
            SettingUpExperiment=true;
            var iParams = advancedImagingSettings.GetImagingParameters(
                advancedImagingSettings.SelectedImagingMethod.m_ImagingMethodID,
                labelSetIndex,//Start with labelset 0 i.e. the first labelSet
                cycleTimeIndex);//Start with first cycleTimes
            if( reOptimizeGainExposure )
            {
                SettingUpExperiment=true;
                imagerModel.m_imagingParams = iParams;
                Snapshot(true);
                imageOptimizationResults = imagerModel.SetGainExposure(ref iParams, plateMaskFactory.PixelMask, 10, 0.2);
                imagerModel.m_imagingParams = iParams;
                SelectedOptimizationResult = 0;
            }else
            {
                var oldGains = new List<int>(imagerModel.m_imagingParams.Gain.ToArray());
                var oldExp = new List<float>(imagerModel.m_imagingParams.Exposure.ToList());
                imagerModel.m_imagingParams = iParams;
                for(int i=0; i<oldGains.Count; i++)
                {
                    imagerModel.m_imagingParams.Gain[i] = oldGains[i];
                }
                for(int i=0; i<oldExp.Count; i++)
                {
                    imagerModel.m_imagingParams.Exposure[i] = oldExp[i];
                    if ( i < imagerModel.m_imagingParams.NumLabels )
                    {
                        if( imagerModel.m_imagingParams.Exposure[i] * 1000 + 250 > imagerModel.m_imagingParams.CycleTime[i] )
                        {
                            //Need to post a warning message here.
                        }
                    }
                }
            }
        }

        private int selectedOptimizationResult;
        public int SelectedOptimizationResult
        {
            get { return selectedOptimizationResult; }
            set
            {
                selectedOptimizationResult = value;
                if (selectedOptimizationResult < imageOptimizationResults.Count)
                {
                    UpdateSelectedOptimizationResult();
                }
                NotifyOfPropertyChange(() => SelectedOptimizationResult);
            }
        }

        public void UpdateSelectedOptimizationResult()
        {
            events.Publish(new WaveGuideEvents.DisplayImageEvent(imageOptimizationResults[selectedOptimizationResult].Item2));
            Gain = imagerModel.m_imagingParams.Gain[selectedOptimizationResult];
            Exposure = imagerModel.m_imagingParams.Exposure[selectedOptimizationResult];
            LabelName = imagerModel.m_imagingParams.LabelName[selectedOptimizationResult];
            var r = imageOptimizationResults[selectedOptimizationResult].Item1;
            switch(r.Item1 )
            {
                case 2:
                    OptimizedImageOutcome = "Too Bright";
                    break;
                case 1:
                    OptimizedImageOutcome= "Too Dim";
                    break;
                default:
                    OptimizedImageOutcome = "Succeeded";
                    break;
            }

            OptimizedImageBrightness = r.Item2;
            
        }

        public void UpdateGainExposureForSelectedLabel()
        {
            if (settingUpExperiment)//this should only be used in the context of approving parameters for an experiment
            {
                imagerModel.m_imagingParams.Gain[selectedOptimizationResult] = Gain;
                imagerModel.m_imagingParams.Exposure[selectedOptimizationResult] = Exposure;
            }
        }

        public bool RunExperimentSetup(object v)
        {
            var setup = (bool)v;
            if (setup)
            {
                SetupExperiment(0, 0, true);
            }
            else
            {
                SettingUpExperiment = false;
                imageOptimizationResults.Clear();
            }
            return true;
        }

        public bool Snapshot(object args)
        {
            if (settingUpExperiment)
            {
                var iParams = imagerModel.m_imagingParams;
                int h = iParams.HorzBinning;
                int v = iParams.VertBinning;
                int sc = iParams.Image_StartCol;
                int ec = iParams.Image_EndCol;
                int sr = iParams.Image_StartRow;
                int er = iParams.Image_EndRow;
                int g = Gain;
                float exp = Exposure;
                var exF = iParams.ExcitationFilter[selectedOptimizationResult];
                var emF = iParams.EmissionFilter[selectedOptimizationResult];
                var image = imagerModel.TakeSnapShot(h, v, sc, ec, sr, er, exp, g, exF, emF);
                plateMaskFactory.UpdateMaskToImage(image);
                //events.Publish(new WaveGuideEvents.DisplayImageEvent(image));
            }
            return true;
        }

        public bool StartBravo(object args)
        {
            if (imagerModel.ThreadsStillRunning())
            {
                events.Publish(new WaveGuideEvents.ErrorMessageEvent("Imaging Thread still running. Aborting Bravo Run."));
                settingUpExperiment = false;
                BravoIsRunning = false;
                return false;
            }

            SettingUpExperiment = false;
            BravoIsRunning = true;
            imagerModel.m_imagingParams.SyncExcitationFilterWithImaging = true;
            string b = imagerModel.m_imagingParams.BravoMethodFilename;

            var ev = new WaveGuideEvents.StartBravoEvent(b, CurrentPlate.m_PlateID, 0, 0);

            events.Publish(ev);

            return true;
        }


        public void StartVideo()
        {
            RunMode = ImagingRunMode.Video;
            int methodid = advancedImagingSettings.SelectedImagingMethod.m_ImagingMethodID;
            int labelSetIndex = advancedImagingSettings.SelectedLabelSets.IndexOf(advancedImagingSettings.FocusLabelSet);
            int imagingLabelIndex = advancedImagingSettings.SelectedImagingLabels.IndexOf(advancedImagingSettings.FocusImagingLabel);
            var iParams = advancedImagingSettings.GetImagingParameters(methodid, labelSetIndex, 0, false, imagingLabelIndex);
            for (int i = 0; i < iParams.NumLabels; i++)
            {
                iParams.Gain[i] = Gain;
                iParams.Exposure[i] = Exposure;
            }
            imagerModel.m_imagingParams = iParams;
            imagerModel.StartVideo();//runs imager in video mode
        }

        public void StopVideo()
        {
            BravoIsRunning = false;
            SettingUpExperiment = false;
            imagerModel.StopVideo();
        }

        public void StartImaging(ImagingRunMode r = ImagingRunMode.Video)
        {
            RunMode = r;
            int methodid = advancedImagingSettings.SelectedImagingMethod.m_ImagingMethodID;
            int labelSetIndex = advancedImagingSettings.SelectedLabelSets.IndexOf(advancedImagingSettings.FocusLabelSet);
            int imagingLabelIndex = advancedImagingSettings.SelectedImagingLabels.IndexOf(advancedImagingSettings.FocusImagingLabel);
            var iParams = advancedImagingSettings.GetImagingParameters(methodid, labelSetIndex, 0, false, imagingLabelIndex);
            for (int i = 0; i < iParams.NumLabels; i++)
            {
                iParams.Gain[i] = Gain;
                iParams.Exposure[i] = Exposure;
            }
            iParams.NumImages = iParams.NumLabels;
            imagerModel.m_imagingParams = iParams;
            imagerModel.StartImaging();
        }

        public WG_Queue_Image CurrentImage
        {
            get { return currentImage; }
            set
            {
                currentImage = value;   
                NotifyOfPropertyChange(() => CurrentImage);
            }
        }

        private void PostErrorMessage(object sender, WaveGuideEvents.ErrorEventArgs e)
        {
            events.Publish(new WaveGuideEvents.ErrorMessageEvent(e.ErrorMessage));
        }

        public void UpdateImage( object sender, EventArgs e)
        {
            var newImage = imagerModel.DisplayQueue.Dequeue();
            if (newImage != null)
            {
                if (System.Threading.Monitor.TryEnter(lockCurrentImage))
                {
                    try
                    {
                        var displayImageTask = new System.Threading.Tasks.Task<bool>(new Func<object, bool>(DisplayImage), newImage);
                        displayImageTask.Start();
                    }
                    finally
                    {
                        System.Threading.Monitor.Exit(lockCurrentImage);
                    }

                }
                CurrentImage = newImage;
            }
        }

        private bool DisplayImage(object arg)
        {
            var image = (WGImage)arg;
            try
            {
                events.Publish(new WaveGuideEvents.DisplayImageEvent(image));//This marshalls the call to the GUI Thread
            }
            catch(Exception e)
            {
                events.Publish(new WaveGuideEvents.StatusEvent(e.Message));
            }
            return true;
        }

        public void UpdateImagerClippingOptions()
        {
            if (clipToPlateMask)
            {
                plateMaskFactory.LoadMask(SelectedPlateMask);
                //plateMaskFactory.
                //This is here for default right now since the logic hasn't been set for clipping based on the platemask.
                //Most of the machinery is there it just needs to be used and set correctly.
                imagerModel.m_imagingParams.Image_StartCol = 0;
                imagerModel.m_imagingParams.Image_EndCol = imagerModel.m_camera.YPixels;
                imagerModel.m_imagingParams.Image_StartRow = 0;
                imagerModel.m_imagingParams.Image_EndRow = imagerModel.m_camera.XPixels;
            }
            else
            {
                imagerModel.m_imagingParams.Image_StartCol = 0;
                imagerModel.m_imagingParams.Image_EndCol = imagerModel.m_camera.YPixels;
                imagerModel.m_imagingParams.Image_StartRow = 0;
                imagerModel.m_imagingParams.Image_EndRow = imagerModel.m_camera.XPixels;
            }
        }

        private void UpdatePlateMaskBinning()
        {
            var converter = new CameraBinningEnumToTupleConverter();
            var b = converter.EnumToTuple(Binning);
            double scaleX = ((double)1) / ((double)b.Item1);
            double scaleY = ((double)1) / ((double)b.Item2);
            int canvasHeight = imagerModel.m_camera.XPixels / b.Item2;
            int canvasWidth = imagerModel.m_camera.YPixels / b.Item1;
            var ok = plateMaskFactory.RebuildMask(scaleX, scaleY, canvasHeight, canvasWidth);
            if (ok && clipToPlateMask)
            {
                UpdateImagerClippingOptions();
            }
        }

        private void HandleCameraPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case "CameraTemperature":
                    NotifyOfPropertyChange(() => CameraTemp);
                    break;
            }
        }

        private void HandleImagingMethodViewModelChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case  "AvailableImagingMethods" :
                    InitImagingMethod();
                    break;
                default:
                    break;
            }
        }
    }
}
