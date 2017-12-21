﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Configuration;

namespace Waveguide
{
    public class GlobalVars
    {
        public enum USER_ROLE_ENUM
        {
            ADMIN,
            USER,
            OPERATOR
        }

        private static USER_ROLE_ENUM _userRole;  // assigned role of this user
        public static USER_ROLE_ENUM UserRole
        {
            get { return _userRole; }
            set { _userRole = value; }
        }

        private static int _userID;  // database record ID for user
        public static int UserID
        {
            get { return _userID; }
            set { _userID = value; }
        }

        private static string _userDisplayName;  // display name for user, taken from DB after login
        public static string UserDisplayName
        {
            get { return _userDisplayName; }
            set { _userDisplayName = value; }
        }

        private static string _defaultReportFileDirectory;  // display name for user, taken from DB after login
        public static string DefaultReportFileDirectory
        {
            get { return _defaultReportFileDirectory; }
            set { _defaultReportFileDirectory = value; }
        }

        private static int _maxPixelValue;  // maximum pixel value from camera
        public static int MaxPixelValue
        {
            get { return _maxPixelValue; }
            set { _maxPixelValue = value; }
        }

        private static int _pixelWidth;  // pixel width of camera CCD
        public static int PixelWidth
        {
            get { return _pixelWidth; }
            set { _pixelWidth = value; }
        }

        private static int _pixelHeight;  // pixel height of camera CCD
        public static int PixelHeight
        {
            get { return _pixelHeight; }
            set { _pixelHeight = value; }
        }

        private static int _cameraTargetTemperature;
        public static int CameraTargetTemperature
        {
            get { return _cameraTargetTemperature; }
            set { _cameraTargetTemperature = value; }
        }

        private static int _cameraDefaultCycleTime;
        public static int CameraDefaultCycleTime
        {
            get { return _cameraDefaultCycleTime; }
            set { _cameraDefaultCycleTime = value; }
        }

        private static byte _filterChangeSpeed;
        public static byte FilterChangeSpeed
        {
            get { return _filterChangeSpeed; }
            set { _filterChangeSpeed = value; }
        }


        private static string _imageFileSaveLocation;
        public static string ImageFileSaveLocation
        {
            get { return _imageFileSaveLocation; }
            set { _imageFileSaveLocation = value; }
        }

        private static List<Color> _defaultTraceColorList;
        public static List<Color> DefaultTraceColorList
        {
            get { return _defaultTraceColorList; }
            set { _defaultTraceColorList = value; }
        }

        private static COMPRESSION_ALGORITHM _compressionAlgorithm;
        public static COMPRESSION_ALGORITHM CompressionAlgorithm
        {
            get { return _compressionAlgorithm; }
            set { _compressionAlgorithm = value; }
        }


        private static string _databaseConnectionString;
        public static string DatabaseConnectionString
        {
            get { return _databaseConnectionString; }
            set { _databaseConnectionString = value; }
        }

        private static string _vworksUsername;
        public static string VWorksUsername
        {
            get { return _vworksUsername; }
            set { _vworksUsername = value; }
        }

        private static string _vworksPassword;
        public static string VWorksPassword
        {
            get { return _vworksPassword; }
            set { _vworksPassword = value; }
        }

        private static string _vworksProtocolFileDirectory;  // display name for user, taken from DB after login
        public static string VWorksProtocolFileDirectory
        {
            get { return _vworksProtocolFileDirectory; }
            set { _vworksProtocolFileDirectory = value; }
        }

        private static int _upSignalOptimizePercentCountThreshold;
        public static int UpSignalOptimizePercentCountThreshold
        {
            get { return _upSignalOptimizePercentCountThreshold; }
            set { _upSignalOptimizePercentCountThreshold = value; }
        }

        private static int _downSignalOptimizePercentCountThreshold;
        public static int DownSignalOptimizePercentCountThreshold
        {
            get { return _downSignalOptimizePercentCountThreshold; }
            set { _downSignalOptimizePercentCountThreshold = value; }
        }

        private static int _upDownSignalOptimizePercentCountThreshold;
        public static int UpDownSignalOptimizePercentCountThreshold
        {
            get { return _upDownSignalOptimizePercentCountThreshold; }
            set { _upDownSignalOptimizePercentCountThreshold = value; }
        }

        private static int _maxTemperatureThresholdDeviation;
        public static int MaxTemperatureThresholdDeviation
        {
            get { return _maxTemperatureThresholdDeviation; }
            set { _maxTemperatureThresholdDeviation = value; }
        }

        private static string _enclosureCameraIPAddress;
        public static string EnclosureCameraIPAddress
        {
            get { return _enclosureCameraIPAddress; }
            set { _enclosureCameraIPAddress = value; }
        }





        public static void LoadConfiguration()
        {
            try
            { 
                var appSettings = ConfigurationManager.AppSettings;

                DefaultTraceColorList = new List<Color>();
                Color color;

                if (appSettings.Count == 0)
                {
                    // App Settings are empty
                }
                else
                {
                    foreach (var key in appSettings.AllKeys)
                    {
                        Console.WriteLine("Key: {0} Value: {1}", key, appSettings[key]);

                        switch (key)
                        {
                            case "MaxPixelValue":
                                MaxPixelValue = Convert.ToInt32(appSettings[key]);
                                break;
                            case "CameraSensorPixelWidth":
                                PixelWidth = Convert.ToInt32(appSettings[key]);
                                break;
                            case "CameraSensorPixelHeight":
                                PixelHeight = Convert.ToInt32(appSettings[key]);
                                break;
                            case "DatabaseConnectionString":
                                DatabaseConnectionString = appSettings[key];
                                break;
                            case "CompressionAlgorithm":
                                switch(appSettings[key].ToUpper())
                                {                                    
                                    case "GZIP":
                                        CompressionAlgorithm = COMPRESSION_ALGORITHM.GZIP;
                                        break;
                                    case "NONE":
                                    default:
                                        CompressionAlgorithm = COMPRESSION_ALGORITHM.NONE;
                                        break;
                                }
                                break;
                            case "DefaultReportFileDirectory":
                                DefaultReportFileDirectory = appSettings[key];
                                break;
                            case "CameraTargetTemperature":
                                CameraTargetTemperature = Convert.ToInt32(appSettings[key]);
                                break;
                            case "CameraDefaultCycleTime":
                                CameraDefaultCycleTime = Convert.ToInt32(appSettings[key]);
                                break;
                            case "FilterChangeSpeed":
                                FilterChangeSpeed = Convert.ToByte(appSettings[key]);
                                break;
                            case "ImageFileSaveLocation":
                                ImageFileSaveLocation = appSettings[key];
                                break;
                            case "Color1":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color2":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color3":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color4":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color5":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color6":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color7":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "Color8":
                                color = (Color)ColorConverter.ConvertFromString(appSettings[key]);
                                if(color!=null) DefaultTraceColorList.Add(color);
                                break;
                            case "VWorksUsername":
                                VWorksUsername = appSettings[key];
                                break;
                            case "VWorksPassword":
                                VWorksPassword = appSettings[key];
                                break;
                            case "VWorksProtocolFileDirectory":
                                VWorksProtocolFileDirectory = appSettings[key];
                                break;
                            case "UpSignalOptimizePercentCountThreshold":
                                UpSignalOptimizePercentCountThreshold = Convert.ToInt32(appSettings[key]);
                                break;
                            case "DownSignalOptimizePercentCountThreshold":
                                DownSignalOptimizePercentCountThreshold = Convert.ToInt32(appSettings[key]);
                                break;
                            case "UpDownSignalOptimizePercentCountThreshold":
                                UpDownSignalOptimizePercentCountThreshold = Convert.ToInt32(appSettings[key]);
                                break;
                            case "MaxTemperatureThresholdDeviation":
                                MaxTemperatureThresholdDeviation = Convert.ToInt32(appSettings[key]);
                                break;
                            case "EnclosureCameraIPAddress":
                                EnclosureCameraIPAddress = appSettings[key];
                                break;
                        }
                    }
                }
            }
            catch (ConfigurationErrorsException)
            {
                // Error reading app settings
            }
        }


    }
}
