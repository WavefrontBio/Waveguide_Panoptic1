using Infragistics.Windows.DataPresenter;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Interaction logic for ExperimentConfigurator.xaml
    /// </summary>
    public partial class ExperimentConfigurator : UserControl
    {
        WaveguideDB wgDB;

        ExperimentConfiguratorViewModel VM;

        Imager m_imager;

        VWorks m_vworks;

        WriteableBitmap m_controlSubtractionBitmap;


        public ExperimentConfigurator()
        {
            InitializeComponent();

            wgDB = new WaveguideDB();

            VM = new ExperimentConfiguratorViewModel();

            m_imager = null;

            m_vworks = null;

            this.DataContext = VM;

            m_controlSubtractionBitmap = BitmapFactory.New(384, 256);

            ControlSubtractionPlateImage.Source = m_controlSubtractionBitmap;
        }


        public void SetImager(Imager imager)
        {
            m_imager = imager;
        }


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshProjectList();
            PopulateMaskList();
            PopulatePlateTypeList();
        }


        private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {   
            // disable all groups below and clear combobox lists         
            if (VM.MethodList != null) VM.MethodList.Clear();
            VM.Method = null;
            VM.ShowPublicMethods = 0;
      
            VM.ExperimentPlate = null;
       
            if(VM.CompoundPlateList != null) VM.CompoundPlateList.Clear();     

            if (VM.IndicatorList != null) VM.IndicatorList.Clear();

            
            // get selection
            VM.Project = (ProjectContainer)ProjectComboBox.SelectedItem;

            // if valid selection, enable next group and populate combobox
            if (VM.Project != null)
            {
                bool showPublic = false;
                if (VM.ShowPublicMethods == 1) showPublic = true;

                LoadMethods(GlobalVars.UserID, showPublic);


            }

            VM.SetExperimentStatus();
            
        }


        private void LoadMethods(int userID, bool publicMethods)
        {
            // if publicMethods = false, get all the methods that belong to the given userID 
            // if publicMethods = true, get all the public methods that don't belong to the given userID


            if (!publicMethods)
            {
                bool success = wgDB.GetAllMethodsForUser(userID);

                if (success)
                {
                    if (VM.MethodList != null) VM.MethodList.Clear();
                    else VM.MethodList = new ObservableCollection<MethodContainer>();

                    foreach (MethodContainer method in wgDB.m_methodList)
                    {
                        VM.MethodList.Add(method);
                    }
                }
            }
            else
            {
                bool success = wgDB.GetAllPublicMethods();

                if (success)
                {
                    if (VM.MethodList != null) VM.MethodList.Clear();
                    else VM.MethodList = new ObservableCollection<MethodContainer>();

                    foreach (MethodContainer method in wgDB.m_methodList)
                    {
                        if(method.OwnerID != userID) VM.MethodList.Add(method);
                    }
                }
            }

        }


        private void Method_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            bool showPublic = false;
            if (VM.ShowPublicMethods == 1) showPublic = true;

            LoadMethods(GlobalVars.UserID, showPublic);
        }




        private void MethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {          
            if (VM.CompoundPlateList != null) VM.CompoundPlateList.Clear();
                        
            if (VM.IndicatorList != null) VM.IndicatorList.Clear();

            VM.ImagePlateBarcode = "";
 
            // get selection
            VM.Method = (MethodContainer)MethodComboBox.SelectedItem;

            if (VM.Method != null)
            {
            
                // get all the compound plates for the Method
                bool success = wgDB.GetAllCompoundPlatesForMethod(VM.Method.MethodID);

                if (success)
                {
                    if(VM.CompoundPlateList != null) VM.CompoundPlateList.Clear();
                    else VM.CompoundPlateList = new ObservableCollection<ExperimentCompoundPlateContainer>();

                    foreach (CompoundPlateContainer cpdPlate in wgDB.m_compoundPlateList)
                    {
                        ExperimentCompoundPlateContainer expCpdPlate = new ExperimentCompoundPlateContainer();
                        expCpdPlate.Barcode = "";
                        expCpdPlate.Description = cpdPlate.Description;
                        expCpdPlate.ExperimentCompoundPlateID = 0;
                        expCpdPlate.ExperimentID = 0;

                        VM.CompoundPlateList.Add(expCpdPlate);
                    }

                    // get all the indicators for the Method
                    success = wgDB.GetAllIndicatorsForMethod(VM.Method.MethodID);

                    if (success)
                    {
                        if (VM.IndicatorList != null) VM.IndicatorList.Clear();
                        else VM.IndicatorList = new ObservableCollection<ExperimentIndicatorContainer>();

                        foreach (IndicatorContainer indicator in wgDB.m_indicatorList)
                        {
                            ExperimentIndicatorContainer expIndicator = new ExperimentIndicatorContainer();
                            expIndicator.Description = indicator.Description;
                            expIndicator.EmissionFilterPos = indicator.EmissionsFilterPosition;
                            expIndicator.ExcitationFilterPos = indicator.ExcitationFilterPosition;
                            expIndicator.ExperimentID = 0;
                            expIndicator.ExperimentIndicatorID = 0;
                            expIndicator.Exposure = 100; // just something default
                            expIndicator.Gain = 1;  // just something default
                            expIndicator.MaskID = 0;
                            expIndicator.Verified = false;

                            FilterContainer filter;
                            success = wgDB.GetExcitationFilterAtPosition(indicator.ExcitationFilterPosition, out filter);
                            if (success)
                            {
                                if (filter != null)
                                {
                                    expIndicator.ExcitationFilterDesc = filter.Description;
                                }
                            }

                            success = wgDB.GetEmissionFilterAtPosition(indicator.EmissionsFilterPosition, out filter);
                            if (success)
                            {
                                if (filter != null)
                                {
                                    expIndicator.EmissionFilterDesc = filter.Description;
                                }
                            }

                            VM.IndicatorList.Add(expIndicator);
                        }
                    }
                }
            }

            VM.SetExperimentStatus();

        }




 

        private void MaskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // get selection
            VM.Mask = (MaskContainer)MaskComboBox.SelectedItem;

            if (VM.Mask != null)
            {
                // populate ROI pixel coordinates    
                VM.RoiX = 1;
                VM.RoiY = 1;

                if(m_imager != null)
                {                                    
                    VM.RoiW = m_imager.m_camera.XPixels;
                    VM.RoiH = m_imager.m_camera.YPixels;
                }
                else
                {
                    VM.RoiW = 1024;
                    VM.RoiH = 1024;
                }

                // populate ROI mask coordinates
                VM.RoiMaskStartRow = 0;
                VM.RoiMaskEndRow = VM.Mask.Rows - 1;
                VM.RoiMaskStartCol = 0;
                VM.RoiMaskEndCol = VM.Mask.Cols - 1;        
            }

            VM.SetExperimentStatus();

            DrawPlate(); 

        }


        public void PopulateMaskList()
        {
            bool success = wgDB.GetAllMasks();
            if (success)
            {
                if (VM.MaskList == null) VM.MaskList = new ObservableCollection<MaskContainer>();
                else VM.MaskList.Clear();

                foreach (MaskContainer mask in wgDB.m_maskList)
                {
                    VM.MaskList.Add(mask);
                }

            }
        }



        public void PopulatePlateTypeList()
        {
            bool success = wgDB.GetAllPlateTypes();
            if(success)
            {
                if (VM.PlateTypeList == null) VM.PlateTypeList = new ObservableCollection<PlateTypeContainer>();
                else VM.PlateTypeList.Clear();

                foreach (PlateTypeContainer ptc in wgDB.m_plateTypeList)
                {
                    VM.PlateTypeList.Add(ptc);

                    if (ptc.IsDefault)
                    {
                        VM.PlateType = ptc;  // sets the selected item in the platetype combobox

                        // populate MaskList with masks for given platetype
                        success = wgDB.GetAllMasksForPlateType(ptc.PlateTypeID);

                        if(success)
                        {
                            if (VM.MaskList == null) VM.MaskList = new ObservableCollection<MaskContainer>();
                            else VM.MaskList.Clear();

                            foreach (MaskContainer mc in wgDB.m_maskList)
                            {
                                VM.MaskList.Add(mc);

                                if (mc.IsDefault) VM.Mask = mc;
                            }
                        }
                    }
                }
            }
        }

            

        public void RefreshProjectList()
        {
            ObservableCollection<ProjectContainer> projList;

            bool success = wgDB.GetAllProjectsForUser(GlobalVars.UserID, out projList);

            if (success)
            {
                VM.ProjectList = projList;               
            }
        }



        private void VerifyPB_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            DataRecord record = btn.DataContext as DataRecord;
            ExperimentIndicatorContainer indicator = (ExperimentIndicatorContainer)record.DataItem;

            ManualImagerDialog dlg = new ManualImagerDialog(m_imager, false);



            FilterContainer exFilt, emFilt;
            exFilt = null;
            emFilt = null;

            bool success = wgDB.GetAllExcitationFilters();
            if (success)
            {
                foreach (FilterContainer fc in wgDB.m_filterList)
                {
                    if (fc.PositionNumber == indicator.ExcitationFilterPos)
                        exFilt = fc;
                }
            }

            success = wgDB.GetAllEmissionFilters();
            if (success)
            {
                foreach (FilterContainer fc in wgDB.m_filterList)
                {
                    if (fc.PositionNumber == indicator.EmissionFilterPos)
                        emFilt = fc;
                }
            }


            RunImager rc = dlg.GetRunImagerControl();
            rc.SetImager(m_imager);
            rc.SetMask(VM.Mask);

            rc.SetBinning(VM.HorzBinning, VM.VertBinning);
            if(exFilt!=null && emFilt!=null) rc.SetFilters(exFilt,emFilt);
            rc.SetIndicatorName(indicator.Description);
            rc.SetROI(VM.RoiX, VM.RoiY, VM.RoiW, VM.RoiH);
            rc.SetExposure(indicator.Exposure);
            rc.SetGain(indicator.Gain);
            rc.SetManualMode(false); // turns of abilty to change the filters selected
            
            dlg.ShowDialog();

            indicator.Exposure = rc.VM.Exposure;
            indicator.Gain = rc.VM.Gain;

            // if the binning was changed, un-verify all other indicators
            if(VM.HorzBinning != rc.VM.HorzBinning) // binning was changed
            {
                foreach(ExperimentIndicatorContainer ind in VM.IndicatorList)
                {
                    ind.Verified = false;
                }
            }


            VM.HorzBinning = rc.VM.HorzBinning;
            VM.VertBinning = rc.VM.VertBinning;

            if (rc.VM.HorzBinning == 1 && rc.VM.VertBinning == 1) Binning_1x1_RadioButton.IsChecked = true;
            else if (rc.VM.HorzBinning == 2 && rc.VM.VertBinning == 2) Binning_2x2_RadioButton.IsChecked = true;
            else if (rc.VM.HorzBinning == 4 && rc.VM.VertBinning == 4) Binning_4x4_RadioButton.IsChecked = true;
            else if (rc.VM.HorzBinning == 8 && rc.VM.VertBinning == 8) Binning_8x8_RadioButton.IsChecked = true;

            indicator.Verified = true;

            VM.SetExperimentStatus();
        }




        private void Binning_1x1_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.VertBinning = 1;
            VM.HorzBinning = 1;
        }

        private void Binning_2x2_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.VertBinning = 2;
            VM.HorzBinning = 2;
        }

        private void Binning_4x4_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.VertBinning = 4;
            VM.HorzBinning = 4;
        }

        private void Binning_8x8_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.VertBinning = 8;
            VM.HorzBinning = 8;
        }


        private void AbortPB_Click(object sender, RoutedEventArgs e)
        {
            
        }






        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //  
        // Dependency Properties


        // .NET Property wrapper
      
        //public int CurrentUserID
        //{
        //    get { return (int)GetValue(CurrentUserIDProperty); }
        //    set { SetValue(CurrentUserIDProperty, value); }
        //}



        //////////////////////////////////////////////
        //// dependency property definition
        //public static readonly DependencyProperty CurrentUserIDProperty =
        //            DependencyProperty.Register(
        //                "CurrentUserID",              // Name of the dependency property
        //                typeof(int),            // Type of the dependency property
        //                typeof(ExperimentConfigurator),        // Type of the owner
        //                new FrameworkPropertyMetadata(
        //                    0,                                            // The default value of the dependency property
        //                    new PropertyChangedCallback(OnUserIDChanged),  // Callback when the property changes
        //                    new CoerceValueCallback(CoerceUserID)),        // Callback when value coercion is required
        //                new ValidateValueCallback(IsValidUserID));     // Callback for custom validation

        //// The validation callback
        //private static bool IsValidUserID(object value) { /* Validate the set value */  return true; }

        //// The coercion callback
        //private static object CoerceUserID(DependencyObject d, object value) { /* Adjust the value without throwing an exception */ return value; }

        //// The value changed callback 
        //private static void OnUserIDChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{ /* Called every time the dependency property value changes */
        //    ExperimentConfigurator exp = (ExperimentConfigurator)d; 
        //    exp.RefreshProjectList();
        //}






        private void CompoundPlateDataGrid_CellUpdated(object sender, Infragistics.Windows.DataPresenter.Events.CellUpdatedEventArgs e)
        {
            if (e.Cell.Record.DataItem.GetType() == typeof(ExperimentCompoundPlateContainer))
            {
                bool allValidated = true;
                foreach (ExperimentCompoundPlateContainer plate in VM.CompoundPlateList)
                {
                    if (!plate.BarcodeValid) allValidated = false;
                }


                if (allValidated)
                {
                    VM.CompoundPlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;
                    VM.ImagerStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                    VM.ImagerEnabled = true;
                }
                else
                {
                    VM.CompoundPlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                    VM.ImagerStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
                }

            }            
        }

        private void StartExperimentPB_Click(object sender, RoutedEventArgs e)
        {
            bool success;

            List<ExperimentIndicatorContainer> expIndicatorList = new List<ExperimentIndicatorContainer>();
            
            // project, should already exist, so VM.Project should hold a valid project record
            ProjectContainer project = VM.Project;

            // user, get from database 
            UserContainer user;
            success = wgDB.GetUser(GlobalVars.UserID, out user);
            if(!success) 
            {
                ShowErrorDialog("Database Error: GetUser", wgDB.GetLastErrorMsg());
                return;
            } 
            else if(user==null)
            {
                ShowErrorDialog("User with database record id: " + GlobalVars.UserID.ToString() + " does not exist", "Data Error");
                return;
            }

            // method, this should already be populated
            MethodContainer method = VM.Method;            
            if(method==null)
            {
                ShowErrorDialog("Method not set", "Data Error");
                return;
            }


            // plate, may or may not already exist in database, so check first to see if it exists, and if not create it
            PlateContainer plate;
            success = wgDB.GetPlateByBarcode(VM.ImagePlateBarcode, out plate);
            if (!success)
            {
                ShowErrorDialog("Datebase Error: GetPlateByBarcode", wgDB.GetLastErrorMsg());
                return;
            }
            if(plate==null)  // plate does not exist, so create it
            {
                plate = new PlateContainer();
                plate.Barcode = VM.ImagePlateBarcode;
                plate.Description = DateTime.Now.ToString() + "/" + project.Description + "/" + method.Description + "/" + user.Lastname + ", " + user.Firstname;
                plate.IsPublic = false;
                plate.OwnerID = GlobalVars.UserID;
                plate.PlateTypeID = VM.PlateType.PlateTypeID;
                plate.ProjectID = project.ProjectID;

                success = wgDB.InsertPlate(ref plate);
                
                if(!success)
                {
                    VM.ExperimentPlate = null;
                    ShowErrorDialog("Database Error: InsertPlate", wgDB.GetLastErrorMsg());
                    return;
                }               
            }

            if (success)
            {
                VM.ExperimentPlate = plate;
            }

            if(VM.ExperimentPlate == null)
            {
                ShowErrorDialog("Experiment Error","Unable to assign Experiment Plate");
                return;
            }


            // experiment, create new
            ExperimentContainer experiment = new ExperimentContainer();

            experiment.Description = DateTime.Now.ToString() + "/" + project.Description + "/" + method.Description + "/" + user.Lastname + ", " + user.Firstname;
            experiment.HorzBinning = VM.HorzBinning;
            experiment.VertBinning = VM.VertBinning;
            experiment.MethodID = method.MethodID;
            experiment.PlateID = plate.PlateID;
            experiment.ROI_Height = VM.RoiH;
            experiment.ROI_Width = VM.RoiW;
            experiment.ROI_Origin_X = VM.RoiX;
            experiment.ROI_Origin_Y = VM.RoiY;
            experiment.TimeStamp = DateTime.Now;

            success = wgDB.InsertExperiment(ref experiment);

            if (!success)
            {
                ShowErrorDialog("Datebase Error: InsertExperiment", wgDB.GetLastErrorMsg());
                return;
            }



            // experiment compound plate(s), create new record for each compound plate defined by the selected method
            for (int i = 0; i < VM.CompoundPlateList.Count; i++)
            {
                ExperimentCompoundPlateContainer cPlate = VM.CompoundPlateList[i];
                cPlate.ExperimentID = experiment.ExperimentID;
                success = wgDB.InsertExperimentCompoundPlate(ref cPlate);
                if (!success)
                {
                    ShowErrorDialog("Datebase Error: InsertExperimentCompoundPlate", wgDB.GetLastErrorMsg());
                    return;
                }
            }



            // experiment indicator(s), create new record for each indicator defined by the method
            //  also builds a list of experiment indicators that will be passed to protocol execution method
            for (int i = 0; i < VM.IndicatorList.Count; i++)
            {
                ExperimentIndicatorContainer indicator = VM.IndicatorList[i];
                indicator.ExperimentID = experiment.ExperimentID;
                indicator.MaskID = VM.Mask.MaskID;
                FilterContainer filter;
                success = wgDB.GetExcitationFilterAtPosition(indicator.ExcitationFilterPos,out filter);
                indicator.ExcitationFilterDesc = filter.Description;
                success = wgDB.GetEmissionFilterAtPosition(indicator.EmissionFilterPos, out filter);
                indicator.EmissionFilterDesc = filter.Description;

                success = wgDB.InsertExperimentIndicator(ref indicator);
                if (!success)
                {
                    ShowErrorDialog("Datebase Error: InsertExperimentIndicator", wgDB.GetLastErrorMsg());
                    return;
                }
                
                expIndicatorList.Add(indicator);
            }


            TaskScheduler uiTask = TaskScheduler.FromCurrentSynchronizationContext();

            //if (m_vworks == null)
            //{
            //    m_vworks = new VWorks();
            //}

            

            RunExperiment runDlg = new RunExperiment(m_imager, uiTask);

           

            runDlg.Configure(VM.BuildImagingParameters(),
                VM.Project, VM.ExperimentPlate, experiment, VM.Method, VM.Mask,
                VM.PlateType, VM.IndicatorList, VM.CompoundPlateList,
                VM.ControlSubtractionWellList,
                VM.NumFoFrames,
                VM.DynamicRatioNumeratorIndicator,
                VM.DynamicRatioDenominatorIndicator);



            runDlg.ShowDialog();


            // reset experiment configurator
            ResetExperimentConfigurator();


            // make sure that VWorks is killed
            Kill_VWorks_Process();

        } // END StartExperimentPB_Click()




        private void Kill_VWorks_Process()
        {
            // make sure the VWorks Process is stopped

            try
            {
                foreach (Process proc in Process.GetProcesses()) 
                {
                    if (proc.ProcessName.Contains("VWorks"))
                    {
                        proc.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }



        private void ResetExperimentConfigurator()
        {
            ImagePlateBarcodeTextBox.Text = ""; // clear barcode for image plate

            VM.CompoundPlateList.Clear();  // clear compound plate list

            VM.IndicatorList.Clear(); // clear indicator list

            MethodComboBox.SelectedIndex = -1;  // clear method combobox selection

            VM.ControlSubtractionWellList.Clear();  // clear control subtraction well list
            DrawPlate();            
        }


        private void ShowErrorDialog(string title, string errMsg)
        {
            // this is just a convenience function
            MessageBox.Show(errMsg,title,MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void PlateTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cbox = (ComboBox)sender;

            if (cbox.SelectedItem == null)
            {                
                return;
            }

            if(cbox.SelectedItem.GetType() == typeof(PlateTypeContainer))
            {
                PlateTypeContainer ptc = (PlateTypeContainer)cbox.SelectedItem;

                if (wgDB == null) wgDB = new WaveguideDB();

                bool success = wgDB.GetAllMasksForPlateType(ptc.PlateTypeID);

                if(success)
                {
                    VM.MaskList.Clear();
                    VM.Mask = null;

                    foreach (MaskContainer mc in wgDB.m_maskList)
                    {
                        VM.MaskList.Add(mc);

                        if (mc.IsDefault) VM.Mask = mc;
                    }
                }
            }
        }



        private void TemperatureEdit_ValueChanged(object sender, EventArgs e)
        {
            GlobalVars.CameraTargetTemperature = VM.Temperature;

            if(m_imager!=null)
                m_imager.m_camera.SetCoolerTemp(VM.Temperature);
        }



        public void DrawPlate()
        {
            int colWidth = (int)(m_controlSubtractionBitmap.Width / VM.PlateType.Cols);
            int rowHeight = (int)(m_controlSubtractionBitmap.Height / VM.PlateType.Rows);

            int x1;
            int x2;
            int y1;
            int y2;

            m_controlSubtractionBitmap.Lock();

            m_controlSubtractionBitmap.Clear();

            for (int r = 0; r < VM.PlateType.Rows; r++)
                for (int c = 0; c < VM.PlateType.Cols; c++)
                {
                    x1 = (c * colWidth);
                    x2 = x1 + colWidth;
                    y1 = (r * rowHeight);
                    y2 = y1 + rowHeight;
                    m_controlSubtractionBitmap.DrawRectangle(x1, y1, x2, y2, Colors.Black);
                }

            foreach(Tuple<int,int> well in VM.ControlSubtractionWellList)
            {
                int r = well.Item1;
                int c = well.Item2;

                x1 = (c * colWidth);
                x2 = x1 + colWidth;
                y1 = (r * rowHeight);
                y2 = y1 + rowHeight;

                m_controlSubtractionBitmap.FillRectangle(x1 + 2, y1 + 2, x2 - 2, y2 - 2, Colors.Red);
            }

            m_controlSubtractionBitmap.Unlock();

        }




        private void DynamicRatioNumeratorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VM.SetExperimentStatus();
        }


        private void DynamicRatioDenominatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VM.SetExperimentStatus();
        }


        private void ControlSubtractionPlateImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WellSelectionDialog dlg = new WellSelectionDialog(VM.PlateType.Rows, VM.PlateType.Cols,
                VM.ControlSubtractionWellList);

            dlg.ShowDialog();

            DrawPlate();

            VM.SetExperimentStatus();
        }
    

    }





    public class ExperimentConfiguratorViewModel : IDataErrorInfo, INotifyPropertyChanged
    {

        public enum STEP_STATUS { WAITING_FOR_PREDECESSOR, NEEDS_INPUT, READY };
        

        private bool _plateEnabled;
        private bool _compoundPlateEnabled;
        private bool _methodEnabled;
        private bool _imagerEnabled;
        private bool _runtimeAnalysisEnabled;
        private bool _runEnabled;
        private bool _plateBarcodeValid;
        private bool _dynamicRatioGroupEnabled;

        private ProjectContainer _project;
        private PlateContainer _experimentPlate;
        private MethodContainer _method;
        private MaskContainer _mask;
        private PlateTypeContainer _plateType;
        private string _imagePlateBarcode;

        private STEP_STATUS _projectStatus;
        private STEP_STATUS _methodStatus;
        private STEP_STATUS _plateStatus;
        private STEP_STATUS _compoundPlateStatus;
        private STEP_STATUS _imagerStatus;
        private STEP_STATUS _runtimeAnalysisStatus;
        private STEP_STATUS _staticRatioStatus;
        private STEP_STATUS _controlSubtractionStatus;
        private STEP_STATUS _dynamicRatioStatus;


        public bool ProjectImage { get { return _runEnabled; } set { _runEnabled = value; NotifyPropertyChanged("RunEnabled"); } }

        private int _showPublicMethods;

        private ObservableCollection<ProjectContainer> _projectList;     
        private ObservableCollection<MethodContainer> _methodList;
        private ObservableCollection<MaskContainer> _maskList;
        private ObservableCollection<ExperimentIndicatorContainer> _indicatorList;
        private ObservableCollection<ExperimentCompoundPlateContainer> _compoundPlateList;
        private ObservableCollection<PlateTypeContainer> _plateTypeList;

        private int _cycleTime;
        private int _temperature;
        private int _vertBinning;
        private int _horzBinning;
        private int _roiX;  // pixel coordinates
        private int _roiY;
        private int _roiW;
        private int _roiH;
        private int _roiMaskStartRow;
        private int _roiMaskEndRow;
        private int _roiMaskStartCol;
        private int _roiMaskEndCol;

        private int _numFoFrames;
        private ObservableCollection<Tuple<int, int>> _controlSubtractionWellList;
        private ExperimentIndicatorContainer _dynamicRatioNumeratorIndicator;
        private ExperimentIndicatorContainer _dynamicRatioDenominatorIndicator;

        
        public string RedArrowFileUri;
        public string GreenCheckFileUri;
        public string BlankFileUri;

        public bool PlateEnabled { get { return _plateEnabled; } set { _plateEnabled = value; NotifyPropertyChanged("PlateEnabled"); } }
        public bool CompoundPlateEnabled { get { return _compoundPlateEnabled; } set { _compoundPlateEnabled = value; NotifyPropertyChanged("CompoundPlateEnabled"); } }
        public bool MethodEnabled { get { return _methodEnabled; } set { _methodEnabled = value; NotifyPropertyChanged("MethodEnabled"); } }
        public bool ImagerEnabled { get { return _imagerEnabled; } set { _imagerEnabled = value; NotifyPropertyChanged("ImagerEnabled"); } }
        public bool RuntimeAnalysisEnabled { get { return _runtimeAnalysisEnabled; } set { _runtimeAnalysisEnabled = value; NotifyPropertyChanged("RuntimeAnalysisEnabled"); } }
        public bool RunEnabled { get { return _runEnabled; } set { _runEnabled = value; NotifyPropertyChanged("RunEnabled"); } }
        public bool PlateBarcodeValid { get { return _plateBarcodeValid; } set { _plateBarcodeValid = value; NotifyPropertyChanged("PlateBarcodeValid"); } }
        public bool DynamicRatioGroupEnabled { get { return _dynamicRatioGroupEnabled; } set { _dynamicRatioGroupEnabled = value; NotifyPropertyChanged("DynamicRatioGroupEnabled"); } }

        
        public ProjectContainer Project { get { return _project; } set { _project = value; NotifyPropertyChanged("Project"); } }
        public PlateContainer ExperimentPlate { get { return _experimentPlate; } set { _experimentPlate = value; NotifyPropertyChanged("ExperimentPlate"); } }
        public MethodContainer Method { get { return _method; } set { _method = value; NotifyPropertyChanged("Method"); } }
        public MaskContainer Mask { get { return _mask; } set { _mask = value; NotifyPropertyChanged("Mask"); } }
        public PlateTypeContainer PlateType { get { return _plateType; } set { _plateType = value; NotifyPropertyChanged("PlateType"); } }
        public string ImagePlateBarcode { get { return _imagePlateBarcode; } set { _imagePlateBarcode = value; NotifyPropertyChanged("ImagePlateBarcode"); } }

        public STEP_STATUS ProjectStatus { get { return _projectStatus; } set { _projectStatus = value; NotifyPropertyChanged("ProjectStatus"); } }
        public STEP_STATUS MethodStatus { get { return _methodStatus; } set { _methodStatus = value; NotifyPropertyChanged("MethodStatus"); } }
        public STEP_STATUS PlateStatus { get { return _plateStatus; } set { _plateStatus = value; NotifyPropertyChanged("PlateStatus"); } }
        public STEP_STATUS CompoundPlateStatus { get { return _compoundPlateStatus; } set { _compoundPlateStatus = value; NotifyPropertyChanged("CompoundPlateStatus"); } }
        public STEP_STATUS ImagerStatus { get { return _imagerStatus; } set { _imagerStatus = value; NotifyPropertyChanged("ImagerStatus"); } }
        public STEP_STATUS RuntimeAnalysisStatus { get { return _runtimeAnalysisStatus; } set { _runtimeAnalysisStatus = value; NotifyPropertyChanged("RuntimeAnalysisStatus"); } }
        public STEP_STATUS StaticRatioStatus { get { return _staticRatioStatus; } set { _staticRatioStatus = value; NotifyPropertyChanged("StaticRatioStatus"); } }
        public STEP_STATUS ControlSubtractionStatus { get { return _controlSubtractionStatus; } set { _controlSubtractionStatus = value; NotifyPropertyChanged("ControlSubtractionStatus"); } }
        public STEP_STATUS DynamicRatioStatus { get { return _dynamicRatioStatus; } set { _dynamicRatioStatus = value; NotifyPropertyChanged("DynamicRatioStatus"); } }


        public int ShowPublicMethods { get { return _showPublicMethods; } set { _showPublicMethods = value; NotifyPropertyChanged("ShowPublicMethods"); } }

        public ObservableCollection<ProjectContainer> ProjectList { get { return _projectList; } set { _projectList = value; NotifyPropertyChanged("ProjectList"); } }        
        public ObservableCollection<ExperimentCompoundPlateContainer> CompoundPlateList { get { return _compoundPlateList; } set { _compoundPlateList = value; NotifyPropertyChanged("CompoundPlateList"); } }
        public ObservableCollection<MethodContainer> MethodList { get { return _methodList; } set { _methodList = value; NotifyPropertyChanged("MethodList"); } }
        public ObservableCollection<MaskContainer> MaskList { get { return _maskList; } set { _maskList = value; NotifyPropertyChanged("MaskList"); } }
        public ObservableCollection<ExperimentIndicatorContainer> IndicatorList { get { return _indicatorList; } set { _indicatorList = value; NotifyPropertyChanged("IndicatorList"); } }
        public ObservableCollection<PlateTypeContainer> PlateTypeList { get { return _plateTypeList; } set { _plateTypeList = value; NotifyPropertyChanged("PlateTypeList"); } }


        public int CycleTime { get { return _cycleTime; } set { _cycleTime = value; NotifyPropertyChanged("CycleTime"); } }
        public int Temperature { get { return _temperature; } set { _temperature = value; NotifyPropertyChanged("Temperature"); } }
        public int VertBinning { get { return _vertBinning; } set { _vertBinning = value; NotifyPropertyChanged("VertBinning"); } }
        public int HorzBinning { get { return _horzBinning; } set { _horzBinning = value; NotifyPropertyChanged("HorzBinning"); } }
        public int RoiX { get { return _roiX; } set { _roiX = value; NotifyPropertyChanged("RoiX"); } }
        public int RoiY { get { return _roiY; } set { _roiY = value; NotifyPropertyChanged("RoiY"); } }
        public int RoiW { get { return _roiW; } set { _roiW = value; NotifyPropertyChanged("RoiW"); } }
        public int RoiH { get { return _roiH; } set { _roiH = value; NotifyPropertyChanged("RoiH"); } }
        public int RoiMaskStartRow { get { return _roiMaskStartRow; } set { _roiMaskStartRow = value; NotifyPropertyChanged("RoiMaskStartRow"); } }
        public int RoiMaskEndRow { get { return _roiMaskEndRow; } set { _roiMaskEndRow = value; NotifyPropertyChanged("RoiMaskEndRow"); } }
        public int RoiMaskStartCol { get { return _roiMaskStartCol; } set { _roiMaskStartCol = value; NotifyPropertyChanged("RoiMaskStartCol"); } }
        public int RoiMaskEndCol { get { return _roiMaskEndCol; } set { _roiMaskEndCol = value; NotifyPropertyChanged("RoiMaskEndCol"); } }

        public int NumFoFrames { get { return _numFoFrames; } set { _numFoFrames = value; NotifyPropertyChanged("NumFoFrames"); } }
        public ObservableCollection<Tuple<int, int>> ControlSubtractionWellList { get { return _controlSubtractionWellList; } set { _controlSubtractionWellList = value; NotifyPropertyChanged("ControlSubtractionWellList"); } }
        public ExperimentIndicatorContainer DynamicRatioNumeratorIndicator { get { return _dynamicRatioNumeratorIndicator; } set { _dynamicRatioNumeratorIndicator = value; NotifyPropertyChanged("DynamicRatioNumeratorIndicator"); } }
        public ExperimentIndicatorContainer DynamicRatioDenominatorIndicator { get { return _dynamicRatioDenominatorIndicator; } set { _dynamicRatioDenominatorIndicator = value; NotifyPropertyChanged("DynamicRatioDenominatorIndicator"); } }



        public ExperimentConfiguratorViewModel()
        {
            PlateEnabled = false;
            CompoundPlateEnabled = false;
            MethodEnabled = false;
            ImagerEnabled = false;
            RuntimeAnalysisEnabled = false;
            RunEnabled = false;

            CycleTime = GlobalVars.CameraDefaultCycleTime;
            Temperature = GlobalVars.CameraTargetTemperature;
            VertBinning = 1;
            HorzBinning = 1;
            RoiX = 0;
            RoiY = 0;
            RoiW = 0;
            RoiH = 0;

            RoiMaskStartRow = 0;
            RoiMaskEndRow = 0;
            RoiMaskStartCol = 0;
            RoiMaskEndCol = 0;

            NumFoFrames = 5;

            ControlSubtractionWellList = new ObservableCollection<Tuple<int, int>>();

            RedArrowFileUri = "pack://application:,,,/Waveguide;component/Images/red_arrow_2.png";
            GreenCheckFileUri = "pack://application:,,,/Waveguide;component/Images/green_check_1.png";
            BlankFileUri = "pack://application:,,,/Waveguide;component/Images/blank.png";

            // Status: 0 = not yet enabled, i.e. something before it needs input (blank)
            //         1 = needs input (red arrow) 
            //         2 = properly completed (green check)
            ProjectStatus = STEP_STATUS.NEEDS_INPUT;
            MethodStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            PlateStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            CompoundPlateStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            ImagerStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            RuntimeAnalysisStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            StaticRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            ControlSubtractionStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            DynamicRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;


        }


        public ImagingParameters BuildImagingParameters()
        {
            ImagingParameters iParams = new ImagingParameters();

            iParams.maxPixelValue = GlobalVars.MaxPixelValue;
            iParams.imageWidth = GlobalVars.PixelWidth / HorzBinning;
            iParams.imageHeight = GlobalVars.PixelHeight / VertBinning;
            iParams.Image_StartCol = RoiX;
            iParams.Image_EndCol = RoiX + RoiW - 1;
            iParams.Image_StartRow = RoiY;
            iParams.Image_EndRow = RoiY + RoiH - 1;
            iParams.BravoMethodFilename = Method.BravoMethodFile;
            iParams.CameraTemperature = GlobalVars.CameraTargetTemperature;
            iParams.HorzBinning = HorzBinning;
            iParams.VertBinning = VertBinning;
            iParams.EmissionFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            iParams.ExcitationFilterChangeSpeed = GlobalVars.FilterChangeSpeed;
            iParams.LightIntensity = 100;
            iParams.NumImages = 1000000; // artificial limit on number of images
            iParams.NumIndicators = IndicatorList.Count;
            iParams.SyncExcitationFilterWithImaging = true;

            iParams.CycleTime = new int[IndicatorList.Count];
            iParams.EmissionFilter = new byte[IndicatorList.Count];
            iParams.ExcitationFilter = new byte[IndicatorList.Count];
            iParams.Exposure = new float[IndicatorList.Count];
            iParams.Gain = new int[IndicatorList.Count];
            iParams.ExperimentIndicatorID = new int[IndicatorList.Count];
            iParams.IndicatorName = new string[IndicatorList.Count];
            iParams.LampShutterIsOpen = new bool[IndicatorList.Count];
            

            int i = 0;
            foreach(ExperimentIndicatorContainer ind in IndicatorList)
            {
                iParams.CycleTime[i] = CycleTime; 
                iParams.EmissionFilter[i] = (byte)ind.EmissionFilterPos;
                iParams.ExcitationFilter[i] = (byte)ind.ExcitationFilterPos;
                iParams.Exposure[i] = (float)ind.Exposure / 1000;
                iParams.Gain[i] = ind.Gain;
                iParams.ExperimentIndicatorID[i] = 0; // created by the RunExperiment object when the experiment is run
                iParams.IndicatorName[i] = ind.Description;
                iParams.LampShutterIsOpen[i] = true;
                iParams.ExperimentIndicatorID[i] = ind.ExperimentIndicatorID;

                i++;
            }       

            return iParams;
        }


        public string Error
        {
            get { throw new NotImplementedException(); }
        }

        public string this[string columnName]
        {
            get
            {
                string result = null;
                if (columnName == "ImagePlateBarcode")
                {
                    if (string.IsNullOrEmpty(ImagePlateBarcode))
                    {
                        PlateBarcodeValid = false;
                        result = "Please enter a Barcode for the Image Plate";
                        SetExperimentStatus();
                    }
                    //else if (ImagePlateBarcode.Length != 8)
                    //{
                    //    PlateBarcodeValid = false;
                    //    result = "Barcode must be exactly 8 characters";
                    //    SetExperimentStatus();
                    //}
                    else
                    {
                        PlateBarcodeValid = true;
                        SetExperimentStatus();
                    }
                }
                return result;
            }
        }

    
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }





        public void SetExperimentStatus()
        {
            ProjectStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
            MethodStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
            PlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
            CompoundPlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
            ImagerStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
            RuntimeAnalysisStatus = ExperimentConfiguratorViewModel.STEP_STATUS.WAITING_FOR_PREDECESSOR;
            StaticRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            ControlSubtractionStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
            DynamicRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;

            MethodEnabled = false;
            PlateEnabled = false;
            CompoundPlateEnabled = false;
            ImagerEnabled = false;
            RuntimeAnalysisEnabled = false;

            RunEnabled = false;

            // set Method Status
            if (Project != null)
            {
                ProjectStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                MethodStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                MethodEnabled = true;

                // set Plate Status
                if (Method != null)
                {
                    MethodStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                    PlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                    PlateEnabled = true;

                    if (PlateBarcodeValid)
                    {
                        PlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                        CompoundPlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                        CompoundPlateEnabled = true;

                        // set Compound Plate Status
                        bool allCompoundPlatesHaveBarcode = true;
                        // check to see if all compound plates have a valid barcode
                        foreach (ExperimentCompoundPlateContainer plate in CompoundPlateList)
                        {
                            if (!plate.BarcodeValid) allCompoundPlatesHaveBarcode = false;
                        }

                        if (allCompoundPlatesHaveBarcode)
                        {
                            CompoundPlateStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                            ImagerStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                            ImagerEnabled = true;

                            // set Imager Status
                            bool allIndicatorsVerified = true;
                            // check to see if all indicators have been verified
                            foreach (ExperimentIndicatorContainer eic in IndicatorList)
                            {
                                if (!eic.Verified) allIndicatorsVerified = false;
                            }
                            if (allIndicatorsVerified)
                            {
                                ImagerStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                                RuntimeAnalysisStatus = ExperimentConfiguratorViewModel.STEP_STATUS.NEEDS_INPUT;
                                RuntimeAnalysisEnabled = true;

                                // set Runtime Analysis Status
                                if (true) // set this as ready as soon as imager status is ready, since
                                          // nothing is required
                                {
                                    RuntimeAnalysisStatus = ExperimentConfiguratorViewModel.STEP_STATUS.READY;

                                    if (IndicatorList.Count < 2) DynamicRatioGroupEnabled = false;                                    
                                    else DynamicRatioGroupEnabled = true;

                                    RunEnabled = true;

                                    // set status of StaticRatio
                                    if (RuntimeAnalysisStatus == STEP_STATUS.READY)
                                        StaticRatioStatus = STEP_STATUS.READY;
                                    else
                                        StaticRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;


                                    // set status of ControlSubtraction                                    
                                    if (ControlSubtractionWellList.Count > 0)
                                        ControlSubtractionStatus = STEP_STATUS.READY;
                                    else
                                        ControlSubtractionStatus = STEP_STATUS.NEEDS_INPUT;
                                   

                                    // set status of DynamicRatio
                                    if(IndicatorList.Count<2)
                                    {
                                        DynamicRatioStatus = STEP_STATUS.WAITING_FOR_PREDECESSOR;
                                    }
                                    else if (DynamicRatioNumeratorIndicator == null ||
                                        DynamicRatioDenominatorIndicator == null ||
                                        DynamicRatioNumeratorIndicator == DynamicRatioDenominatorIndicator)
                                    {
                                        DynamicRatioStatus = STEP_STATUS.NEEDS_INPUT;
                                    }
                                    else
                                        DynamicRatioStatus = STEP_STATUS.READY;
                                }

                            }

                        }
                    }

                }
            }

        }





    }






}
