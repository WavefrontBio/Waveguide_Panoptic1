using Infragistics.Windows.DataPresenter;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Waveguide
{
    /// <summary>
    /// Interaction logic for PlateTypeManager.xaml
    /// </summary>
    public partial class PlateTypeManager : UserControl
    {

        WaveguideDB wgDB;

        PlateTypeManagerViewModel VM;


        public static readonly RoutedCommand EditMaskCommand = new RoutedCommand();
        public static readonly RoutedCommand MaskIsDefaultCheckBoxCommand = new RoutedCommand();
        public static readonly RoutedCommand PlateTypeIsDefaultCheckBoxCommand = new RoutedCommand();




        void MaskIsDefaultCheckBoxCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // The ShowInChartCommand command can execute if the parameter references a Customer.
            e.CanExecute = e.Parameter is MaskContainer;
        }

        void MaskIsDefaultCheckBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var mask = e.Parameter as MaskContainer;
            if (mask != null)
                this.SetMaskIsDefault(mask, mask.IsDefault);
        }




        void PlateTypeIsDefaultCheckBoxCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // The ShowInChartCommand command can execute if the parameter references a Customer.
            e.CanExecute = e.Parameter is PlateTypeItem;
        }

        void PlateTypeIsDefaultCheckBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var pti = e.Parameter as PlateTypeItem;
            if (pti != null)
                this.SetPlateTypeIsDefault(pti, pti.PlateType.IsDefault);
        }




        void EditMaskCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // The ShowInChartCommand command can execute if the parameter references a Customer.
            e.CanExecute = e.Parameter is MaskContainer;
        }

        void EditMaskCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var mask = e.Parameter as MaskContainer;
            if (mask != null)
                this.EditMask(mask);
        }







        public PlateTypeManager()
        {
            wgDB = new WaveguideDB();
            VM = new PlateTypeManagerViewModel();

            InitializeComponent();

            //Initialize data in the XamDataGrid - NOTE: A blank record is added FIRST, this is key to this approach for the XamDataGrid
            PlateTypeItem blank = new PlateTypeItem();
            blank.PlateType.Description = "";
            blank.PlateType.Rows = 16;
            blank.PlateType.Cols = 24;
            blank.PlateType.PlateTypeID = 0;
            blank.PlateType.IsDefault = false;
            VM.PlateTypeList.Add(blank);

            // load all plate types in database
            bool success = wgDB.GetAllPlateTypes();
            if(success)
            {
                foreach(PlateTypeContainer ptc in wgDB.m_plateTypeList)
                {
                    PlateTypeItem pti = new PlateTypeItem();
                    pti.PlateType.PlateTypeID = ptc.PlateTypeID;
                    pti.PlateType.Description = ptc.Description;
                    pti.PlateType.Cols = ptc.Cols;
                    pti.PlateType.Rows = ptc.Rows;
                    pti.PlateType.IsDefault = ptc.IsDefault;

                    VM.PlateTypeList.Add(pti);


                    // add a blank mask for this platetype
                    MaskContainer mask = new MaskContainer();
                    mask.Angle = 0;
                    mask.Cols = blank.PlateType.Cols;
                    mask.Description = "";
                    mask.IsDefault = false;
                    mask.MaskID = 0;
                    mask.PlateTypeID = pti.PlateType.PlateTypeID;
                    mask.ReferenceImageID = 0;
                    mask.Rows = blank.PlateType.Rows;
                    mask.Shape = 0;
                    mask.XOffset = 50;
                    mask.YOffset = 50;
                    mask.XSize = 50;
                    mask.YSize = 50;
                    mask.XStep = 50;
                    mask.YStep = 50;

                    pti.MaskList.Add(mask);

                    success = wgDB.GetAllMasksForPlateType(pti.PlateType.PlateTypeID);
                    if(success)
                    {
                        foreach(MaskContainer mc in wgDB.m_maskList)
                        {
                            pti.MaskList.Add(mc);
                        }
                    }


                }
            }


            this.DataContext = VM;
        }




        void SetMaskIsDefault(MaskContainer mask, bool isDefault)
        {
            bool success; 

            if (wgDB == null) wgDB = new WaveguideDB();


            if (isDefault)  // if setting to true, set any others are true to false (to make sure we don't have multiple defaults)
            {
                PlateTypeItem ptItem = null;

                // find parent PlateTypeItem
                foreach (PlateTypeItem pti in VM.PlateTypeList)
                {
                    if (mask.PlateTypeID == pti.PlateType.PlateTypeID)
                    {
                        ptItem = pti;
                        break;
                    }
                }

                if(ptItem != null)
                {
                    foreach(MaskContainer mc in ptItem.MaskList)
                    {
                        if(mc.IsDefault)
                        {
                            mc.IsDefault = false;
                            success = wgDB.UpdateMask(mc);
                        }
                    }
                }

            }

            mask.IsDefault = isDefault;

            success = wgDB.UpdateMask(mask);

            if(!success)
            {
                MessageBox.Show("Failed to Update Mask", wgDB.GetLastErrorMsg(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        void SetPlateTypeIsDefault(PlateTypeItem pti, bool isDefault)
        {
            bool success;

            if (wgDB == null) wgDB = new WaveguideDB();

            if(isDefault)  //  setting isDefault to true, need to set any other PlateTypeItem.PlateType.IsDefault that is true to false, so
                           //  that there's only on of them set to default
            {
                foreach(PlateTypeItem ptItem in VM.PlateTypeList)
                {
                    if(ptItem.PlateType.IsDefault)
                    {
                        ptItem.PlateType.IsDefault = false;

                        success = wgDB.UpdatePlateType(ptItem.PlateType);
                    }
                }
            }
            
            // setting isDefault to false, do just have to update the PlateType
            
            pti.PlateType.IsDefault = isDefault;

            success = wgDB.UpdatePlateType(pti.PlateType);

            if (!success)
            {
                MessageBox.Show("Failed to Update PlateType", wgDB.GetLastErrorMsg(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            
        }





        void EditMask(MaskContainer mask)
        {
            //MessageBox.Show("not yet implemented","Edit Mask: " + mask.Description, MessageBoxButton.OK, MessageBoxImage.Error);

            MaskManager mm = new MaskManager();
            mm.Owner = Window.GetWindow(this);

            mm.m_mask.SetupMaskFromContainer(mask);

            if(mask.ReferenceImageID != 0)
                mm.LoadReferenceImage(mask.ReferenceImageID);

            mm.DrawMask();

            mm.ShowDialog();


            if (wgDB == null) wgDB = new WaveguideDB();

            MaskContainer mc;
            bool success = wgDB.GetMask(mask.MaskID, out mc);

            if(success)
            {
                mask.Angle = mc.Angle;
                mask.Cols = mc.Cols;
                mask.Description = mc.Description;
                mask.IsDefault = mc.IsDefault;
                mask.MaskID = mc.MaskID;
                mask.PlateTypeID = mc.PlateTypeID;
                mask.ReferenceImageID = mc.ReferenceImageID;
                mask.Rows = mc.Rows;
                mask.Shape = mc.Shape;
                mask.XOffset = mc.XOffset;
                mask.XSize = mc.XSize;
                mask.XStep = mc.XStep;
                mask.YOffset = mc.YOffset;
                mask.YSize = mc.YSize;
                mask.YStep = mc.YStep;
            }
        }




        private void MarkAddNewRecord(DataRecord record)
        {
            record.IsFixed = true;
            record.Tag = "AddRecord";
        }

        private void UnMarkAddNewRecord(DataRecord record)
        {
            record.IsFixed = false;
            record.Tag = null;
        }



        private void xamDataGrid_EditModeEnded(object sender, Infragistics.Windows.DataPresenter.Events.EditModeEndedEventArgs e)
        {
            // use this method to update a record after one of the cells of the record has been edited         

            if (((string)e.Cell.Record.Tag) == "AddRecord") return;  // not updating the AddRecord here

            if (e.Cell.Record.DataItem.GetType() == typeof(PlateTypeItem))
            {
                PlateTypeItem pti = (PlateTypeItem)e.Cell.Record.DataItem;

                if (pti.PlateType.PlateTypeID != 0)
                {
                    bool success = wgDB.UpdatePlateType(pti.PlateType);
                }

            }
            else if (e.Cell.Record.DataItem.GetType() == typeof(MaskContainer))
            {
                MaskContainer mc = (MaskContainer)e.Cell.Record.DataItem;

                if (mc.MaskID != 0)
                {
                    bool succcess = wgDB.UpdateMask(mc);
                }
            }           
        }




        private void xamDataGrid_RecordUpdated(object sender, Infragistics.Windows.DataPresenter.Events.RecordUpdatedEventArgs e)
        {
            if (e.Record.Tag == null) return;

            if (((string)e.Record.Tag).Equals("AddRecord"))  // is this the "AddRecord"?
            {
                if (e.Record.DataItem.GetType() == typeof(PlateTypeItem))
                {
                    DataRecord plateTypeRecord = (DataRecord)e.Record;

                    PlateTypeItem pti = ((PlateTypeItem)(plateTypeRecord.DataItem));

                    PlateTypeContainer ptc = pti.PlateType;

                    bool success = wgDB.InsertPlateType(ref ptc);
                    if (success)
                    {
                        UnMarkAddNewRecord(plateTypeRecord);

                        // add new blank plate type
                        PlateTypeItem newPti = new PlateTypeItem();
                        newPti.PlateType.Description = "";
                        newPti.PlateType.Cols = ptc.Cols;
                        newPti.PlateType.Rows = ptc.Rows;
                        newPti.PlateType.IsDefault = false;
                        newPti.PlateType.PlateTypeID = 0;

                        VM.PlateTypeList.Insert(0, newPti);
                        
                        // mark the new PlateType as the AddRecord
                        RecordCollectionBase coll = e.Record.ParentCollection;
                        DataRecord newPlateTypeRecord = (DataRecord)coll.ElementAt(0);
                        MarkAddNewRecord(newPlateTypeRecord);

                        // add the blank AddRecord Mask for this new platetype
                        MaskContainer newMask = new MaskContainer();
                        newMask.Description = "";
                        newMask.Angle = 0.0;
                        newMask.Cols = ptc.Cols;
                        newMask.IsDefault = false;
                        newMask.MaskID = 0;
                        newMask.PlateTypeID = ptc.PlateTypeID;
                        newMask.ReferenceImageID = 0;
                        newMask.Rows = ptc.Rows;
                        newMask.Shape = 0;
                        newMask.XOffset = 50;
                        newMask.XSize = 50;
                        newMask.XStep = 50;
                        newMask.YOffset = 50;
                        newMask.YSize = 50;
                        newMask.YStep = 50;

                        newPti.MaskList.Add(newMask);

                        // mark the new Mask as the AddRecord  
                        ExpandableFieldRecord expRecord = (ExpandableFieldRecord)newPlateTypeRecord.ChildRecords[0];
                        DataRecord maskRecord = (DataRecord)expRecord.ChildRecords[0];

                        if (maskRecord.DataItem.GetType() == typeof(MaskContainer))
                        {
                            MarkAddNewRecord(maskRecord);
                        }

                    }
                }
                else if (e.Record.DataItem.GetType() == typeof(MaskContainer))
                {
                    MaskContainer mc = (MaskContainer)(e.Record.DataItem);

                    bool success = wgDB.InsertMask(ref mc);

                    if (success)
                    {
                        UnMarkAddNewRecord(e.Record);

                        // add new blank mask for AddRecord
                        MaskContainer newMask = new MaskContainer();
                        newMask.Description = "";
                        newMask.Angle = 0.0;
                        newMask.Cols = mc.Cols;
                        newMask.IsDefault = false;
                        newMask.MaskID = 0;
                        newMask.PlateTypeID = mc.PlateTypeID;
                        newMask.ReferenceImageID = 0;
                        newMask.Rows = mc.Rows;
                        newMask.Shape = 0;
                        newMask.XOffset = 50;
                        newMask.XSize = 50;
                        newMask.XStep = 50;
                        newMask.YOffset = 50;
                        newMask.YSize = 50;
                        newMask.YStep = 50;

                        PlateTypeItem pti = (PlateTypeItem)(((DataRecord)e.Record.ParentRecord.ParentRecord).DataItem);

                        pti.MaskList.Insert(0, newMask);

                        DataRecord newMaskRecord = (DataRecord)e.Record.ParentCollection[0];
                        MarkAddNewRecord(newMaskRecord);
                    }
                }

            }
        }




        private void xamDataGrid_RecordsDeleting(object sender, Infragistics.Windows.DataPresenter.Events.RecordsDeletingEventArgs e)
        {
            bool success;

            foreach (DataRecord record in e.Records)
            {
                if (record.DataItem.GetType() == typeof(PlateTypeItem))
                {
                    PlateTypeItem pti = (PlateTypeItem)record.DataItem;
                    foreach (MaskContainer mc in pti.MaskList)
                    {
                        success = wgDB.DeleteMask(mc.MaskID);
                        if (!success) break;
                    }

                    success = wgDB.DeletePlateType(pti.PlateType.PlateTypeID);
                    if (!success) break;
                }
                else if (record.DataItem.GetType() == typeof(MaskContainer))
                {
                    MaskContainer mc = (MaskContainer)record.DataItem;
                    success = wgDB.DeleteMask(mc.MaskID);                    
                    if (!success) break;
                }               
            }
        }



        private void xamDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            XamDataGrid xdg = xamDataGrid;

            MarkAddNewRecord((DataRecord)xamDataGrid.Records[0]);

            // mark AddRecord for each Mask
            foreach (DataRecord rec in xamDataGrid.Records)  // step through all methods
            {
                if (rec.HasChildren)  // if the method has children
                {
                    foreach (ExpandableFieldRecord expRecord in rec.ChildRecords)
                    {
                        // get first record and mark as "AddRecord"
                        DataRecord addRecord = (DataRecord)(expRecord.ChildRecords[0]);

                        if (addRecord.DataItem.GetType() == typeof(MaskContainer))
                        {
                            MarkAddNewRecord(addRecord);
                        }                        
                    }
                }
            }
        }



    }



    class PlateTypeItem : INotifyPropertyChanged
    {
        private PlateTypeContainer _plateType;
        private ObservableCollection<MaskContainer> _maskList;

        public PlateTypeContainer PlateType
        { get { return _plateType; } set { _plateType = value; NotifyPropertyChanged("PlateType"); } }

        public ObservableCollection<MaskContainer> MaskList
        { get { return _maskList; } set { _maskList = value; NotifyPropertyChanged("MaskList"); } }

        public PlateTypeItem()
        {
            PlateType = new PlateTypeContainer();
            MaskList = new ObservableCollection<MaskContainer>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }

    }





    class PlateTypeManagerViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<PlateTypeItem> _plateTypeList;

        public ObservableCollection<PlateTypeItem> PlateTypeList
        { get { return _plateTypeList; } set { _plateTypeList = value; NotifyPropertyChanged("PlateTypeList"); } }

        public PlateTypeManagerViewModel()
        {
            PlateTypeList = new ObservableCollection<PlateTypeItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }
    }


}
