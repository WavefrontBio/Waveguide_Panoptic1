using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace Waveguide
{

    public enum REPORT_FILEFORMAT
        {            
            WAVEGUIDE,
            EXCEL
        };

    class ReportWriter
    {        

        WaveguideDB m_wgDB;        
        ProjectContainer m_project;
        ExperimentContainer m_experiment;        
        MethodContainer m_method;
        PlateContainer m_plate;
        PlateTypeContainer m_plateType;
        UserContainer m_user;
        
        string m_reportDirectory;
        REPORT_FILEFORMAT m_format;

        bool m_initializationSuccess;
        string m_lastErrorString;
        

        public ReportWriter(ProjectContainer project, ExperimentContainer experiment)
        {
            m_initializationSuccess = false;
            m_lastErrorString = "";
            m_reportDirectory = GlobalVars.DefaultReportFileDirectory;
            m_format = REPORT_FILEFORMAT.EXCEL;

            m_wgDB = new WaveguideDB();
            m_project = project;
            m_experiment = experiment;
            

            bool success = m_wgDB.GetMethod(m_experiment.MethodID, out m_method);
            if (m_method == null) success = false;
            if(success)
            {                
                success = m_wgDB.GetPlate(m_experiment.PlateID, out m_plate);
                if (m_plate == null) success = false;
                if(success)
                {
                    success = m_wgDB.GetUser(m_plate.OwnerID, out m_user);
                    if (m_user == null) success = false;
                    if(success)
                    {
                        success = m_wgDB.GetPlateType(m_plate.PlateTypeID, out m_plateType);
                        if (m_plateType == null) success = false;                        
                    }
                }
            }

            m_initializationSuccess = success;
        }


        public bool SuccessfullyInitialized()
        {
            return m_initializationSuccess;
        }

        public string GetLastErrorString()
        {
            return m_lastErrorString;
        }

        public void SetReportDirectory(string path)
        {
            StringBuilder sb = new StringBuilder();

            // make sure it's not empty
            if(sb.Length < 3)
            {
                m_reportDirectory = "C:\\";
                return;
            }

            // make sure it has a "\" on the end of it
            Char ch = sb[sb.Length-1];
            if (ch != '\\') sb.Append("\\");
        }


        public void SetFileType(REPORT_FILEFORMAT format)
        {
            m_format = format;
        }

        public string GetDefaultFilename()
        {
            string fileTypeString = ".txt";
            switch(m_format)
            {
                case REPORT_FILEFORMAT.EXCEL:
                    fileTypeString = ".txt";
                    break;
                case REPORT_FILEFORMAT.WAVEGUIDE:
                    fileTypeString = ".dat";
                    break;                
            }

            string filename = m_project.Description.Replace(" ","_") + "_" + 
               // m_experiment.Description + "_" + 
                m_plate.Barcode + "_" + 
                m_experiment.TimeStamp.ToString("yyyy-MM-dd_HH-mm") + 
                fileTypeString;

            return filename;
        }

        public bool WriteExperimentFile_WaveGuide(string filename, List<AnalysisContainer> analysisList)
        {
            bool success = true;

            if (File.Exists(filename))
            {
                MessageBoxResult result = MessageBox.Show("File: " + filename + " already exists! Do you want to over write it?", "File Already Exists",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        File.Delete(filename);
                        break;
                    case MessageBoxResult.No:
                        success = false;
                        m_lastErrorString = "File already exists";
                        break;
                }
            }

            

            if (success)
            {
                try
                {
                    string delimiter = "\t";   // \t = tab

                    FileStream fs = new FileStream(filename, FileMode.CreateNew, FileAccess.Write);

                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        //  Start writing HEADER

                        sw.WriteLine("<HEADER>");
                        DateTime dt = m_experiment.TimeStamp;
                        sw.WriteLine("Date" + delimiter + dt.Year.ToString() + "-" + dt.Month.ToString() + "-" + dt.Day.ToString());
                        sw.WriteLine("Time" + delimiter + dt.Hour.ToString() + ":" + dt.Minute.ToString() + ":" + dt.Second.ToString());
                        sw.WriteLine("Instrument" + delimiter + "Panoptic");
                        sw.WriteLine("ProtocolName" + delimiter + m_method.Description);
                        sw.WriteLine("AssayPlateBarcode" + delimiter + m_plate.Barcode);

                        success = m_wgDB.GetAllExperimentCompoundPlatesForExperiment(m_experiment.ExperimentID);
                        if (success)
                        {
                            foreach (ExperimentCompoundPlateContainer ecPlate in m_wgDB.m_experimentCompoundPlateList)
                            {
                                sw.WriteLine("AddPlateBarcode" + delimiter + ecPlate.Barcode);
                            }
                        }

                        ObservableCollection<ExperimentIndicatorContainer> expIndicatorList = new ObservableCollection<ExperimentIndicatorContainer>();
                        foreach(AnalysisContainer ac in analysisList)
                        {
                            ExperimentIndicatorContainer expIndicator;
                            success = m_wgDB.GetExperimentIndicator(ac.ExperimentIndicatorID,out expIndicator);
                            if(success && expIndicator!=null)
                            {
                                // make sure this experiment indicator isn't already in the list
                                bool alreadyInList = false;
                                foreach(ExperimentIndicatorContainer expCont in expIndicatorList)
                                {
                                    if(expIndicator.ExperimentIndicatorID == expCont.ExperimentIndicatorID)
                                    {
                                        alreadyInList = true;
                                        break;
                                    }
                                }

                                if(!alreadyInList)
                                {
                                  sw.WriteLine("Indicator" + delimiter +
                                                expIndicator.Description + delimiter +
                                                expIndicator.ExcitationFilterDesc + delimiter +
                                                expIndicator.EmissionFilterDesc + delimiter +
                                                expIndicator.Exposure.ToString() + delimiter +
                                                expIndicator.Gain.ToString());

                                  expIndicatorList.Add(expIndicator);
                                }
                            }
                        }


                        success = m_wgDB.GetAllExperimentIndicatorsForExperiment(m_experiment.ExperimentID, out expIndicatorList);
                        if (success)
                        {
                            foreach (ExperimentIndicatorContainer expIndicator in expIndicatorList)
                            {
                                
                            }
                        }

                        sw.WriteLine("NumRows" + delimiter + m_plateType.Rows.ToString());
                        sw.WriteLine("NumCols" + delimiter + m_plateType.Cols.ToString());

                        List<EventMarkerContainer> eventMarkerList;
                        success = m_wgDB.GetAllEventMarkersForExperiment(m_experiment.ExperimentID, out eventMarkerList);
                        if (success)
                        {
                            foreach (EventMarkerContainer eventMarker in eventMarkerList)
                            {
                                string timeString = String.Format("{0:0.000}", (float)eventMarker.SequenceNumber / 1000);
                                sw.WriteLine("Event" + delimiter + eventMarker.Name + delimiter +
                                                eventMarker.Description + delimiter +
                                                timeString);
                            }
                        }

                        sw.WriteLine("Operator" + delimiter + m_user.Username + delimiter +
                                        m_user.Lastname + delimiter +
                                        m_user.Firstname);

                        sw.WriteLine("Project" + delimiter + m_project.Description);

                        sw.WriteLine("</HEADER>");

                        // END writing HEADER


                        if (success)
                        {
                            foreach (AnalysisContainer analysis in analysisList)
                            {
                                ExperimentIndicatorContainer expIndicator;
                                success = m_wgDB.GetExperimentIndicator(analysis.ExperimentIndicatorID, out expIndicator);

                                sw.WriteLine("<INDICATOR_DATA" + delimiter + expIndicator.Description + delimiter + ">");

                                // START write column headers
                                sw.Write("Time" + delimiter);
                                
                                StringBuilder builder = new StringBuilder();
                                for (int r = 0; r < m_plateType.Rows; r++)
                                    for (int c = 0; c < m_plateType.Cols; c++)
                                    {
                                        builder.Append((char)(65 + r)).Append(c + 1).Append(delimiter);                                        
                                    }
                                builder.Remove(builder.Length - delimiter.Length, delimiter.Length); // remove last delimiter
                                sw.WriteLine(builder.ToString());
                                // END write column headers

                                // START writing data frames
                                success = m_wgDB.GetAllAnalysisFramesForAnalysis(analysis.AnalysisID);
                                if (success)
                                {
                                    foreach (AnalysisFrameContainer aFrame in m_wgDB.m_analysisFrameList)
                                    {
                                        string timeString = String.Format("{0:0.000}", (float)aFrame.SequenceNumber / 1000);
                                        sw.Write(timeString + delimiter);

                                        string[] values = aFrame.ValueString.Split(',');
                                        foreach (string val in values)
                                        {
                                            sw.Write(val + delimiter);
                                        }

                                        sw.WriteLine("");
                                    }
                                }
                                // END writing data frames

                                sw.WriteLine("</INDICATOR_DATA>");
                            }
                        }
                    }

                    if (!success) m_lastErrorString = m_wgDB.GetLastErrorMsg();

                } // end try
                catch (Exception e)
                {
                    success = false;
                    m_lastErrorString = e.Message;
                }
            }

            return success;

        } // end function

       


        public bool WriteExperimentFile_Excel(string filename, AnalysisContainer analysis)
        {
            if (filename == null) { m_lastErrorString = "Filename == null"; return false; }

            bool success = true;

            if(File.Exists(filename))
            {
                MessageBoxResult result = MessageBox.Show("Files for: " +  filename + " already exists! Do you want to over write it?", "File Already Exists",
                    MessageBoxButton.YesNo,MessageBoxImage.Exclamation);

                switch(result)
                {
                    case MessageBoxResult.Yes:
                        File.Delete(filename);         
                        break;
                    case MessageBoxResult.No:
                        success = false;
                        m_lastErrorString = "File already exists";                        
                        break;
                }
            }


            if (success)
            {

                try
                {
                    string delimiter = "\t";   // \t = tab
                    

                            using (FileStream fs = new FileStream(filename, FileMode.CreateNew, FileAccess.Write))
                            {

                                using (StreamWriter sw = new StreamWriter(fs))
                                {
                                    // START write column headers
                                    sw.Write("Time" + delimiter);
                                    
                                    StringBuilder builder = new StringBuilder();
                                    for (int r = 0; r < m_plateType.Rows; r++)
                                        for (int c = 0; c < m_plateType.Cols; c++)
                                        {
                                            builder.Append((char)(65 + r)).Append(c + 1).Append(delimiter);
                                        }
                                    builder.Remove(builder.Length - delimiter.Length, delimiter.Length); // remove last delimiter
                                    sw.WriteLine(builder.ToString());
                                    // END write column headers

                                    // START writing data frames
                                    success = m_wgDB.GetAllAnalysisFramesForAnalysis(analysis.AnalysisID);
                                    if (success)
                                    {
                                        foreach (AnalysisFrameContainer aFrame in m_wgDB.m_analysisFrameList)
                                        {
                                            string timeString = aFrame.SequenceNumber.ToString();
                                            sw.Write(timeString + delimiter);

                                            string[] values = aFrame.ValueString.Split(',');
                                            foreach (string val in values)
                                            {
                                                sw.Write(val + delimiter);
                                            }

                                            sw.WriteLine("");
                                        }
                                    }
                                    // END writing data frames

                                } // END using StreamWriter

                            } // END using FileStream                

                } // end try            
                catch (Exception e)
                {
                    success = false;
                    m_lastErrorString = e.Message;
                }
            }

            return success;

        } // end function

    }
    
}
