using Infragistics.Windows.DataPresenter;
using Infragistics.Windows.Editors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class PlateTypeManager_old : UserControl
    {

        ObservableCollection<PlateTypeContainer> source = new ObservableCollection<PlateTypeContainer>();

        WaveguideDB wgDB;

        public PlateTypeManager_old()
        {
            InitializeComponent();


            // Initialize data in the XamDataGrid - NOTE: A blank record is added FIRST, this is key to this approach for the XamDataGrid
            wgDB = new WaveguideDB();

            bool success = wgDB.GetAllPlateTypes();
            if (success)
            {
                PlateTypeContainer blank = new PlateTypeContainer();
                source.Add(blank);  // this is needed to hold the item to be added to the list, This is the "AddRecord"
                blank.Rows = 16;
                blank.Cols = 24;
                blank.Description = "";
                blank.PlateTypeID = 0;
                blank.IsDefault = false;

                for (int i = 0; i < wgDB.m_plateTypeList.Count(); i++)
                {
                    source.Add(wgDB.m_plateTypeList[i]);
                }            
            }

            xamDataGrid.DataSource = source;
        }

        private void xamDataGrid_EditModeEnded(object sender, Infragistics.Windows.DataPresenter.Events.EditModeEndedEventArgs e)
        {
            // use this method to update a record after one of the cells of the record has been edited         

            if (((string)e.Cell.Record.Tag) == "AddRecord") return;  // not updating the AddRecord here

            PlateTypeContainer pt = (PlateTypeContainer)e.Cell.Record.DataItem;

            if (ValidateData(pt))
            {
                if (pt.PlateTypeID != 0) // if PlateTypeID != 0 then we know that this PlateType is already in the database, so we can call Update
                {
                    bool success = wgDB.UpdatePlateType(pt);
                }
            }
        }
               

        void xamDataGrid_RecordUpdated(object sender, Infragistics.Windows.DataPresenter.Events.RecordUpdatedEventArgs e)
        {
            if (e.Record.IsFixed)  // is this the "AddRecord"?
            {  
                // new record was added to list, so insert it into database
                PlateTypeContainer pt = (PlateTypeContainer)e.Record.DataItem;
                bool success = wgDB.InsertPlateType(ref pt);

                if (success)
                {
                    UnMarkAddNewRecord();
                    PlateTypeContainer c = new PlateTypeContainer();

                    // set default values
                    c.Rows = 16;
                    c.Cols = 24;
                    c.Description = "";
                    c.PlateTypeID = 0;
                    c.IsDefault = false;

                    // insert new AddRecord
                    source.Insert(0, c);
                    MarkAddNewRecord();
                }
            }
        }


        private void xamDataGrid_RecordsDeleting(object sender, Infragistics.Windows.DataPresenter.Events.RecordsDeletingEventArgs e)
        {
            foreach (DataRecord record in e.Records)
            {
                PlateTypeContainer pc = (PlateTypeContainer)record.DataItem;
                bool success = wgDB.DeletePlateType(pc.PlateTypeID);
                if (!success) break;
            }
        }

        private void MarkAddNewRecord()
        {
            if (xamDataGrid.Records.Count > 0)
            {
                xamDataGrid.Records[0].IsFixed = true;
                xamDataGrid.Records[0].Tag = "AddRecord";
            }
        }

        private void UnMarkAddNewRecord()
        {
            if (xamDataGrid.Records.Count > 0)
            {
                xamDataGrid.Records[0].IsFixed = false;
                xamDataGrid.Records[0].Tag = "";
            }
        }

        void xamDataGrid_Loaded(object sender, RoutedEventArgs e)
        {            
            MarkAddNewRecord();
        }


        void IsDefaultValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            XamCheckEditor editor = sender as XamCheckEditor;
            editor.EndEditMode(true, true);
        }
       

        private bool ValidateData(PlateTypeContainer pt)
        {
            // This function performs custom validation of data of a object

            bool dataOK = true;

            if (!dataOK)
            {
                string MsgStr = "Data values for this record not acceptable";
                MessageBoxResult result =
                      MessageBox.Show(MsgStr, "Data Validation Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return dataOK;
        }

        

       
    }
}
