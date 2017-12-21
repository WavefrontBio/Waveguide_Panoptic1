using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ATMCD64CS;
using System.Threading;

namespace Waveguide
{
    public class Camera : INotifyPropertyChanged
    {
        private bool systemInitialized;
        public bool SystemInitialized
        {
            get { return systemInitialized; }
            set
            {
                systemInitialized = value;
                NotifyPropertyChanged("SystemInitialized");
            }
        }

        private const uint NOT_INITIALIZED = 100;

        //float exposure = new float();  // image acquisition exposure time in seconds

        private int bitDepth = new int();
        public int BitDepth
        {
            get { return bitDepth; }
            set
            {
                bitDepth = value;
                NotifyPropertyChanged("BitDepth");
            }
        }

        private int numADChannels = new int();
        public int NumADChannels
        {
            get { return numADChannels; }
            set
            {
                numADChannels = value;
                NotifyPropertyChanged("NumADChannels");
            }
        }

        private int numCameras = new int();
        public int NumCameras
        {
            get { return numCameras; }
            set
            {
                numCameras = value;
                NotifyPropertyChanged("NumCameras");
            }
        }


        private int cameraInfo = new int();
        public int CameraInfo
        {
            get { return cameraInfo; }
            set
            {
                cameraInfo = value;
                NotifyPropertyChanged("CameraInfo");
            }
        }

        private int xPixels;
        public int XPixels
        {
            get { return xPixels; }
            set
            {
                xPixels = value;
                NotifyPropertyChanged("XPixels");
            }
        }


        private int yPixels;
        public int YPixels
        {
            get { return yPixels; }
            set
            {
                yPixels = value;
                NotifyPropertyChanged("YPixels");
            }
        }

        private bool isEMCCD;
        public bool IsEMCCD
        {
            get { return isEMCCD; }
            set
            {
                isEMCCD = value;
                NotifyPropertyChanged("IsEMCCD");
            }
        }

        private ushort iMax;
        public ushort IMax
        {
            get { return iMax; }
            set
            {
                iMax = value;
                NotifyPropertyChanged("IMax");
            }
        }

        private ushort iMin;
        public ushort IMin
        {
            get { return iMin; }
            set
            {
                iMin = value;
                NotifyPropertyChanged("IMin");
            }
        }

        private int cameraTemperature;
        public int CameraTemperature
        {
            get { return cameraTemperature; }
            set
            {
                cameraTemperature = value;
                NotifyPropertyChanged("CameraTemperature");
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(info)); }
        }




        public AndorSDK MyCamera = new AndorSDK();

        public uint SUCCESS = AndorSDK.DRV_SUCCESS;


        public uint DRV_SUCCESS = AndorSDK.DRV_SUCCESS;
        public uint DRV_ERROR_ACK = AndorSDK.DRV_ERROR_ACK;
        public uint DRV_ACQUIRING = AndorSDK.DRV_ACQUIRING;
        public uint DRV_IDLE = AndorSDK.DRV_IDLE;


        /////////////////////////////////////////////////////////////////////////////////////////////
        // Class Events AND Delegate Handlers

        public delegate void PostMessageEventHandler(object sender, WaveGuideEvents.StringMessageEventArgs e);
        public delegate void PostErrorEventHandler(object sender, WaveGuideEvents.ErrorEventArgs e);

        public event PostMessageEventHandler PostMessageEvent;
        public event PostErrorEventHandler PostErrorEvent;

        protected virtual void OnPostMessage(WaveGuideEvents.StringMessageEventArgs e)
        {
            if (PostMessageEvent != null) PostMessageEvent(this, e);
        }

        protected virtual void OnPostError(WaveGuideEvents.ErrorEventArgs e)
        {
            if (PostErrorEvent != null) PostErrorEvent(this, e);
        }

        public void PostMessage(string msg)
        {
            WaveGuideEvents.StringMessageEventArgs e = new WaveGuideEvents.StringMessageEventArgs(msg);
            OnPostMessage(e);
        }

        public void PostError(string errMsg)
        {
            WaveGuideEvents.ErrorEventArgs e = new WaveGuideEvents.ErrorEventArgs(errMsg);
            OnPostError(e);
        }


        //////////////////////////////////////////////////////////////////////////////////////////


       
        public Camera()  // constructor
        {
            // default number of pixels in CCD; this should be set to real value in the Initialize() method below
            XPixels = 1024;
            YPixels = 1024;
            Initialize();
        }

        ~Camera() // destructor
        {
            try
            {
                MyCamera.CoolerOFF();
            }
            catch
            {
            }
        }

        public bool CoolerON(bool turnOn)
        {
            if (!systemInitialized)
            {
                PostError("ERROR: Attempted to turn on Camera Cooler; Camera Not Initialized");
                return false;
            }

            uint ec;
            bool success;
            string errMsg = "";
            if (turnOn)
            {
                ec = MyCamera.CoolerON();
                success = CheckCameraResult(ec, ref errMsg);
                if (success)
                    PostMessage("Camera Cooler ON");
                else
                {
                    PostError("Camera Cooler: " + errMsg);
                    return false;
                }
            }
            else
            {
                ec = MyCamera.CoolerOFF();
                success = CheckCameraResult(ec, ref errMsg);
                if (success)
                    PostMessage("Camera Cooler OFF");
                else
                {
                    PostError("Camera Cooler: " + errMsg);
                    return false;
                }
            }

            return true;
        }

        public bool SetCoolerTemp(int temp)
        {
            if (!systemInitialized)
            {
                PostError("ERROR: Attempted to set Camera temperature; Camera Not Initialized");
                return false;
            }

            uint ec;
            bool success;
            string errMsg = "";
            ec = MyCamera.SetTemperature(temp);
            success = CheckCameraResult(ec, ref errMsg);
            if (success)
                PostMessage("Camera Cooler Temperature Set to " + temp.ToString());
            else
            {
                PostError("Camera Cooler: " + errMsg);
                return false;
            }

            return true;
        }

        public bool GetCoolerTemp(ref int temp)
        {
            if (!systemInitialized)
            {
                PostError("Camera not initialized");
                return false;
            }

            bool success;
            string errMsg = "";

            uint ec = MyCamera.GetTemperature(ref temp);
            CameraTemperature = temp;
            //success = CheckCameraResult(ec, ref errMsg);
            //if (!success)
            //{
            //    PostMessage("Camera Cooler: " + errMsg);
            //    return false;
            //}

            return true;
        }



        public bool Initialize() // configure the camera to capture a single, full image from the camera
        {
            uint uiErrorCode;
            string errMsg = "";

            SystemInitialized = false;

            // initialize the camera
            try
            {
                uiErrorCode = MyCamera.Initialize("");
            }
            catch (Exception e)
            {
                PostError("Andor SDK did not load: " + e.Message);
                return false;
            }
            bool success = CheckCameraResult(uiErrorCode, ref errMsg);
            if (!success)
            {
                PostError("Camera: " + errMsg);
                return false;
            }

            // get capabilities

            AndorSDK.AndorCapabilities caps = new AndorSDK.AndorCapabilities();
            caps.ulSize = 256; // had to guess at the size since sizeof(AndorSDK.AndorCapabilities) isn't allowed
            uiErrorCode = MyCamera.GetCapabilities(ref caps);
            success = CheckCameraResult(uiErrorCode, ref errMsg);
            if (!success)
            {
                PostError("Camera: " + errMsg);
                return false;
            }

            uiErrorCode = MyCamera.GetBitDepth(0, ref bitDepth);
            success = CheckCameraResult(uiErrorCode, ref errMsg);
            if (!success)
            {
                PostError("Camera: " + errMsg);
                return false;
            }

            // Get Detector Format, returns size of sensor in xPixels and yPixels
            uiErrorCode = MyCamera.GetDetector(ref xPixels, ref yPixels);
            success = CheckCameraResult(uiErrorCode, ref errMsg);
            if (!success)
            {
                PostError("Camera: " + errMsg);
                return false;
            }
            
            if (xPixels == 1024 && yPixels == 1024)
            {
                isEMCCD = true;
            }
            else
            {
                isEMCCD = false;
            }

            // Set Camera EM Gain Mode
            if (isEMCCD)
            {
                uiErrorCode = MyCamera.SetEMGainMode(3);
                success = CheckCameraResult(uiErrorCode, ref errMsg);
                if (!success)
                {
                    PostError("Camera: " + errMsg);
                    return false;
                }
            }

            SystemInitialized = true;
            PostMessage("Camera initialized");

            // ADDED by BG, 26 Mar 2014, Want camera to start cooling right away.  Also added MyCamera.CoolerOFF() in destructor
            CameraTemperature = GlobalVars.CameraTargetTemperature;
            SetCoolerTemp(CameraTemperature);
            MyCamera.SetImageRotate(1); // 0 = no rotate, 1 = 90 degs CW, 2 = 90 CCW

            return true;
        }


        public bool AbortAcquisition()
        {
            if (!systemInitialized)
            {
                PostError("Camera not initialized");
                return false;
            }

            uint uiErrorCode;

            uiErrorCode = MyCamera.AbortAcquisition();
            PostMessage("Acquisition Aborted");

            return true;
        }


        public uint AcquireImage(float exp, float maxWaitSeconds, out ushort[] image)
        { // exp is the exposure time in seconds

            if (!systemInitialized)
            {
                image = null;
                return NOT_INITIALIZED;
            }

            uint uiErrorCode;
            int status = 0;

            // set exposure time in seconds
            if (MyCamera.SetExposureTime(exp / 1000) == AndorSDK.DRV_SUCCESS)
            {
                uiErrorCode = MyCamera.PrepareAcquisition();

                // start the image acquisition
                if (MyCamera.StartAcquisition() == AndorSDK.DRV_SUCCESS)
                {
                    while (status != AndorSDK.DRV_IDLE)
                    {
                        uiErrorCode = MyCamera.GetStatus(ref status);
                    }

                    // uiErrorCode = MyCamera.WaitForAcquisition();                   
                }
            }

            // if good acquisition occurred, get image
            uiErrorCode = MyCamera.GetStatus(ref status);
            if (status == AndorSDK.DRV_IDLE)
            {
                uint TotalPixels;

                TotalPixels = (uint)(XPixels * YPixels);

                image = new ushort[TotalPixels];

                //for (int j = 0; j < TotalPixels; j++) img.image[j] = 10000;

                uiErrorCode = MyCamera.GetAcquiredData16(image, TotalPixels);
                //uiErrorCode = MyCamera.GetOldestImage16(img.image, TotalPixels);
            }
            else
            {
                image = null;
            }


            return uiErrorCode;
        }


        public uint EnableAdvancedGainMode(bool enable)
        {
            uint uiErrorCode;
            int state = 0;

            if (enable) state = 1;

            uiErrorCode = MyCamera.SetEMAdvanced(state);

            return uiErrorCode;
        }


        public uint SetOutputAmplifier(int amp)
        {
            uint uiErrorCode;

            uiErrorCode = MyCamera.SetOutputAmplifier(amp);

            return uiErrorCode;
        }


        public uint SetCameraEMGainMode(int mode)
        {   // values for mode:
            // 0 = (default) controlled by DAC 0-255, 1 = controlled by DAC 0-4095, 2 = Linear mode, 3 = Real EM gain

            // if (!SystemInitialized) return NOT_INITIALIZED;

            uint uiErrorCode;
            uiErrorCode = MyCamera.SetEMGainMode(mode);
            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                //System.Diagnostics.Trace.WriteLine("ERROR: setting EM Gain Mode.");
                return uiErrorCode;
            }

            return AndorSDK.DRV_SUCCESS;
        }

        public uint SetCameraEMGain(int gain)
        {
            if (!SystemInitialized) return NOT_INITIALIZED;

            uint uiErrorCode;

            int lowGain = 0;
            int highGain = 0;
            MyCamera.GetEMGainRange(ref lowGain, ref highGain);
            if (gain > highGain) gain = highGain;
            if (gain < lowGain) gain = lowGain;

            uiErrorCode = MyCamera.SetEMCCDGain(gain);

            return uiErrorCode;
        }

        public uint GetCameraEMGainRange(ref int lowVal, ref int hiVal)
        {
            // valid EM gain range is dependent upon the EM Gain Mode

            if (!systemInitialized) return NOT_INITIALIZED;

            uint ec = MyCamera.GetEMGainRange(ref lowVal, ref hiVal);

            return ec;
        }


        public uint SetCameraBinning(int horzBinning, int vertBinning)
        {
            if (!systemInitialized) return NOT_INITIALIZED;

            uint uiErrorCode;

            // Set Readout
            //// set to full resolution for the full image
            //// params: horizontal binning, vertical binning,horz start pixel, horz end pixel, vert start pixel, vert end pixel 
            uiErrorCode = MyCamera.SetImage(horzBinning, vertBinning, 1, xPixels, 1, yPixels);

            return uiErrorCode;
        }




        public uint SendSoftwareTrigger()
        {
            uint ui_ret = DRV_ERROR_ACK;
            int count = 0;
            while (ui_ret == DRV_ERROR_ACK && count < 5)
            {
                ui_ret = MyCamera.SendSoftwareTrigger();
                if (ui_ret == DRV_ERROR_ACK)
                {
                    Thread.Sleep(1);
                    count++;
                }
            }
            return ui_ret;
        }





        public void SynthesizeImage(out ushort[] image, int width, int height)
        {
            ushort hiValue = 4095;
            ushort loValue = 2000;

            int NumPixels = width * height * 2;
            image = new ushort[NumPixels];

            bool high = true;

            for (int i = 0; i < NumPixels; i++)
            {
                if (i % 64 == 0) high = !high;
                if (high) { image[i] = hiValue; }
                else { image[i] = loValue; }
            }
        }





        public bool CheckCameraResult(uint code, ref string errorMsg)
        {
            bool ok = true;

            errorMsg = "SUCCESS";

            if (code != AndorSDK.DRV_SUCCESS)
            {
                ok = false;
                switch (code)
                {
                    case NOT_INITIALIZED: errorMsg = "Camera Not Initialized"; break;
                    case 20001: errorMsg = "ERROR_CODES"; break;
                    case 20002: errorMsg = "SUCCESS"; break;
                    case 20003: errorMsg = "VXD NOT INSTALLED"; break;
                    case 20004: errorMsg = "ERROR_SCAN"; break;
                    case 20005: errorMsg = "ERROR_CHECK_SUM"; break;
                    case 20006: errorMsg = "ERROR_FILELOAD"; break;
                    case 20007: errorMsg = "UNKNOWN_FUNCTION"; break;
                    case 20008: errorMsg = "ERROR_VXD_INIT"; break;
                    case 20009: errorMsg = "ERROR_ADDRESS"; break;
                    case 20010: errorMsg = "ERROR_PAGELOCK"; break;
                    case 20011: errorMsg = "ERROR_PAGE_UNLOCK"; break;
                    case 20012: errorMsg = "ERROR_BOARDTEST"; break;
                    case 20013: errorMsg = "ERROR_ACK"; break;
                    case 20014: errorMsg = "ERROR_UP_FIFO"; break;
                    case 20015: errorMsg = "ERROR_PATTERN"; break;
                    case 20017: errorMsg = "ACQUISITION_ERRORS"; break;
                    case 20018: errorMsg = "ACQ_BUFFER"; break;
                    case 20019: errorMsg = "ACQ_DOWNFIFO_FULL"; break;
                    case 20020: errorMsg = "PROC_UNKNOWN_INSTRUCTION"; break;
                    case 20021: errorMsg = "ILLEGAL_OP_CODE"; break;
                    case 20022: errorMsg = "KINETIC_TIME_NOT_MET"; break;
                    case 20023: errorMsg = "ACCUM_TIME_NOT_MET"; break;
                    case 20024: errorMsg = "NO_NEW_DATA"; break;
                    case 20026: errorMsg = "SPOOLERROR"; break;
                    case 20033: errorMsg = "TEMPERATURE_CODES"; break;
                    case 20034: errorMsg = "TEMPERATURE_OFF"; break;
                    case 20035: errorMsg = "TEMPERATURE_NOT_STABILIZED"; break;
                    case 20036: errorMsg = "TEMPERATURE_STABILIZED"; break;
                    case 20037: errorMsg = "TEMPERATURE_NOT_REACHED"; break;
                    case 20038: errorMsg = "TEMPERATURE_OUT_RANGE"; break;
                    case 20039: errorMsg = "TEMPERATURE_NOT_SUPPORTED"; break;
                    case 20040: errorMsg = "TEMPERATURE_DRIFT"; break;
                    case 20049: errorMsg = "GENERAL_ERRORS"; break;
                    case 20050: errorMsg = "INVALID_AUX"; break;
                    case 20051: errorMsg = "COF_NOTLOADED"; break;
                    case 20052: errorMsg = "FPGAPROG"; break;
                    case 20053: errorMsg = "FLEXERROR"; break;
                    case 20054: errorMsg = "GPIBERROR"; break;
                    case 20064: errorMsg = "DATATYPE"; break;
                    case 20065: errorMsg = "DRIVER_ERRORS"; break;
                    case 20066: errorMsg = "P1INVALID"; break;
                    case 20067: errorMsg = "P2INVALID"; break;
                    case 20068: errorMsg = "P3INVALID"; break;
                    case 20069: errorMsg = "P4INVALID"; break;
                    case 20070: errorMsg = "INIERROR"; break;
                    case 20071: errorMsg = "COFERROR"; break;
                    case 20072: errorMsg = "ACQUIRING"; break;
                    case 20073: errorMsg = "IDLE"; break;
                    case 20074: errorMsg = "TEMPCYCLE"; break;
                    case 20075: errorMsg = "NOT_INITIALIZED"; break;
                    case 20076: errorMsg = "P5INVALID"; break;
                    case 20077: errorMsg = "P6INVALID"; break;
                    case 20078: errorMsg = "INVALID_MODE"; break;
                    case 20079: errorMsg = "INVALID_FILTER"; break;
                    case 20080: errorMsg = "I2CERRORS"; break;
                    case 20081: errorMsg = "I2CDEVNOTFOUND"; break;
                    case 20082: errorMsg = "I2CTIMEOUT"; break;
                    case 20083: errorMsg = "P7INVALID"; break;
                    case 20089: errorMsg = "USBERROR"; break;
                    case 20090: errorMsg = "IOCERROR"; break;
                    case 20091: errorMsg = "NOT_SUPPORTED"; break;
                    case 20093: errorMsg = "USB_INTERRUPT_ENDPOINT_ERROR"; break;
                    case 20094: errorMsg = "RANDOM_TRACK_ERROR"; break;
                    case 20095: errorMsg = "INVALID_TRIGGER_MODE"; break;
                    case 20096: errorMsg = "LOAD_FIRMWARE_ERROR"; break;
                    case 20097: errorMsg = "DIVIDE_BY_ZERO_ERROR"; break;
                    case 20098: errorMsg = "INVALID_RINGEXPOSURES"; break;
                    case 20099: errorMsg = "BINNING_ERROR"; break;
                    case 20100: errorMsg = "INVALID_AMPLIFIER"; break;
                    case 20115: errorMsg = "ERROR_MAP"; break;
                    case 20116: errorMsg = "ERROR_UNMAP"; break;
                    case 20117: errorMsg = "ERROR_MDL"; break;
                    case 20118: errorMsg = "ERROR_UNMDL"; break;
                    case 20119: errorMsg = "ERROR_BUFFSIZE"; break;
                    case 20121: errorMsg = "ERROR_NOHANDLE"; break;
                    case 20130: errorMsg = "GATING_NOT_AVAILABLE"; break;
                    case 20131: errorMsg = "FPGA_VOLTAGE_ERROR"; break;
                    case 20990: errorMsg = "ERROR_NOCAMERA"; break;
                    case 20991: errorMsg = "NOT_SUPPORTED"; break;
                    case 20992: errorMsg = "NOT_AVAILABLE"; break;
                }
            }

            return ok;
        }

        public void StartKineticImaging(float exposure, int numberOfImages, float cycleTime, int horzBin, int vertBin, int acqMode, int numExpGroups, float[] exp)
        {
            uint uiErrorCode;

            uiErrorCode = MyCamera.SetReadMode(4); // 0 Full Vertical Binning, 1 Multi-Track, 2 Random-Track, 3 Single-Track, 4 Image 

            uiErrorCode = MyCamera.SetImage(horzBin, vertBin, 1, xPixels, 1, yPixels); // horizontal binning, vertical binning,horz start pixel, horz end pixel, vert start pixel, vert end pixel 

            uiErrorCode = MyCamera.SetFrameTransferMode(0);  // 0 = OFF, 1 = ON

            MyCamera.SetAcquisitionMode(acqMode);  // 3 = Kinetic Mode, 4 = Fast Kinetic, 5 = Run til Abort

            MyCamera.SetTriggerMode(10);  // Software Trigger

            if (numExpGroups > 1)
                MyCamera.SetExposureTime(exp[0]);   // Exposure Time in seconds
            else
                MyCamera.SetRingExposureTimes(numExpGroups, exp);  // set the exposure times that will be cycled through


            // Move Filter to first position


            MyCamera.StartAcquisition();

            MyCamera.SendSoftwareTrigger();

            MyCamera.SetNumberAccumulations(1); // how many scans for each image (no accummulation here...just a single scan per image)
            MyCamera.SetNumberKinetics(numberOfImages); // number of images to take in this series of images
            MyCamera.SetKineticCycleTime(cycleTime);  // time between the start of consectutive images, in seconds. MUST be larger than exposure time

        }
    }
}
