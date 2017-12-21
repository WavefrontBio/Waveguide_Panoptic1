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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Waveguide
{
    /// <summary>
    /// Interaction logic for ReportDialog.xaml
    /// </summary>
    public partial class ReportDialog : Window
    {
        ViewModel_ReportDialog VM;
        ReportWriter m_reportWriter;
        ProjectContainer m_project;
        ExperimentContainer m_experiment;
        ObservableCollection<ExperimentIndicatorContainer> m_expIndicatorList;
        WaveguideDB m_wgDB;
        

        public ReportDialog(ProjectContainer project, ExperimentContainer experiment, 
                            ObservableCollection<ExperimentIndicatorContainer> expIndicatorList)
        {
            m_project = project;
            m_experiment = experiment;
            m_expIndicatorList = expIndicatorList;

            VM = new ViewModel_ReportDialog();
            m_wgDB = new WaveguideDB();

            m_reportWriter = new ReportWriter(m_project, m_experiment);            

            InitializeComponent();

            this.DataContext = VM;

            VM.Directory = GlobalVars.DefaultReportFileDirectory;

            VM.Filename = m_reportWriter.GetDefaultFilename();

            WaveGuideRB.IsChecked = true;
            VM.ReportFormat = REPORT_FILEFORMAT.WAVEGUIDE;

            SetAnalysisList();


            bool ok = m_reportWriter.SuccessfullyInitialized();
            if (!ok)
            {
                string errMsg = m_reportWriter.GetLastErrorString();
                System.Windows.MessageBox.Show("Error initializing the Report Writer: " +
                    errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        public void SetAnalysisList()
        {
            VM.AnalysisList.Clear();

            foreach (ExperimentIndicatorContainer expIndicator in m_expIndicatorList)
            {
                bool success = m_wgDB.GetAllAnalysesForExperimentIndicator(expIndicator.ExperimentIndicatorID);

                foreach (AnalysisContainer analCont in m_wgDB.m_analysisList)
                {
                    AnalysisListItem analItem = new AnalysisListItem();
                    analItem.AnalysisID = analCont.AnalysisID;
                    analItem.Selected = true;
                    analItem.Description = analCont.Description + "-" + 
                        expIndicator.Description + "  " + 
                        analCont.TimeStamp.ToString();                   

                    VM.AnalysisList.Add(analItem);
                }
            }            

        }





        private void BrowseForDirectoryPB_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            DialogResult result = dlg.ShowDialog();
            if (result.ToString() == "OK")
            {
                VM.Directory = dlg.SelectedPath;
                m_reportWriter.SetReportDirectory(VM.Directory);
            }
        }

        private void CancelPB_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WriteReportFilePB_Click(object sender, RoutedEventArgs e)
        {
            bool success = true;

            List<AnalysisContainer> analysisList = new List<AnalysisContainer>();
            foreach (AnalysisListItem aItem in VM.AnalysisList)
            {
                if (aItem.Selected)
                {
                    AnalysisContainer analCont;
                    success = m_wgDB.GetAnalysis(aItem.AnalysisID, out analCont);
                    if (success && analCont != null)
                        analysisList.Add(analCont);
                }
            }


            switch(VM.ReportFormat)
            {
                case REPORT_FILEFORMAT.WAVEGUIDE:
                    m_reportWriter.SetFileType(REPORT_FILEFORMAT.WAVEGUIDE);
                    success = m_reportWriter.WriteExperimentFile_WaveGuide(VM.Directory + "\\" + VM.Filename, analysisList);
                    break;
                case REPORT_FILEFORMAT.EXCEL:
                    m_reportWriter.SetFileType(REPORT_FILEFORMAT.EXCEL);
                    int i = 1;
                    foreach(AnalysisContainer analysis in analysisList)
                    {
                        string[] strs = VM.Filename.Split('.');                        
                        string filename = strs[0] + "_" + i.ToString() + "." + strs[1];
                        success = m_reportWriter.WriteExperimentFile_Excel(VM.Directory + "\\" + filename, analysis);
                        if (!success) break;
                        i++;
                    }
                    break;
            }


            if(success)
            {
                Close();
            }
            else
            {
                MessageBoxResult result = System.Windows.MessageBox.Show("Failed to write report: " + m_reportWriter.GetLastErrorString(), 
                    "Error",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        }

        private void WaveGuideRB_Checked(object sender, RoutedEventArgs e)
        {            
            if ((bool)WaveGuideRB.IsChecked) VM.ReportFormat = REPORT_FILEFORMAT.WAVEGUIDE;
        }

        private void ExcelRB_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)ExcelRB.IsChecked) VM.ReportFormat = REPORT_FILEFORMAT.EXCEL;
        }

        private void SelectedCkBx_Checked(object sender, RoutedEventArgs e)
        {

        }
    }



    /////////////////////////////////////////////////////////
    // Analysis
    public class ViewModel_ReportDialog : INotifyPropertyChanged
    {        
        private string _filename;
        private string _directory;
        private REPORT_FILEFORMAT _reportFormat;

        private ObservableCollection<AnalysisListItem> _analysisList;
        
        // constructor
        public ViewModel_ReportDialog()
        {
            _analysisList = new ObservableCollection<AnalysisListItem>();
        }

        


        public string Filename
        {
            get { return _filename; }
            set { _filename = value; NotifyPropertyChanged("Filename"); }
        }

        public string Directory
        {
            get { return _directory; }
            set { _directory = value; NotifyPropertyChanged("Directory"); }
        }

        public REPORT_FILEFORMAT ReportFormat
        {
            get { return _reportFormat; }
            set { _reportFormat = value; NotifyPropertyChanged("ReportFormat"); }
        }

        public ObservableCollection<AnalysisListItem> AnalysisList
        {
            get { return _analysisList; }
            set { _analysisList = value; NotifyPropertyChanged("AnalysisList"); }
        }
             

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }
    }


    public class AnalysisListItem : INotifyPropertyChanged
    {
        private int _analysisID;
        private string _description;
        private bool _selected;


        public int AnalysisID
        {
            get { return _analysisID; }
            set { _analysisID = value; NotifyPropertyChanged("AnalysisID"); }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; NotifyPropertyChanged("Description"); }
        }

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; NotifyPropertyChanged("Selected"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }
    }
}
