﻿using Emgu.CV;
using Emgu.CV.Structure;
using Foxconn.Editor.Camera;
using Foxconn.Editor.Configuration;
using Foxconn.Editor.Controls;
using Foxconn.Editor.Enums;
using Foxconn.Editor.OpenCV;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Foxconn.Editor.Dialogs
{
    /// <summary>
    /// Interaction logic for AutoRun.xaml
    /// </summary>
    public partial class AutoRun : Window, INotifyPropertyChanged
    {
        public static AutoRun Current;
        private Worker _loopWorker;

        private Board _program
        {
            get => ProgramManager.Current.Program;
            set => ProgramManager.Current.Program = value;
        }

        private DataBase _database
        {
            get => ProgramManager.Current.Database;
            set => ProgramManager.Current.Database = value;
        }
        private MachineParams _param = MachineParams.Current;
        private DeviceManager _device = DeviceManager.Current;

        private SetSpeedRobotControl _setSpeed = new SetSpeedRobotControl();
        private List<FOVResult> _FOVResults = new List<FOVResult>();
        FOVResult fovResult = new FOVResult();
        private Image<Bgr, byte> _image { get; set; }
        private VideoCapture _capture = null;
        private Mat _frame;

        private string _SN { get; set; }
        public string CycleTime { get; set; }
        public string BoardName { get; set; }

        private int _totalPass { get; set; }
        private int _totalFail { get; set; }
        private int _totalChecked { get; set; }
        private float _yeildRate { get; set; }

        public int TotalPass
        {
            get => _totalPass;
            set
            {
                _totalPass = value;
                NotifyPropertyChanged(nameof(TotalPass));
            }
        }

        public int TotalFail
        {
            get => _totalFail;
            set
            {
                _totalFail = value;
                NotifyPropertyChanged(nameof(TotalFail));
            }
        }

        public int TotalChecked
        {
            get => _totalChecked;
            set
            {
                _totalChecked = value;
                NotifyPropertyChanged(nameof(TotalChecked));
            }
        }

        public float YeildRate
        {
            get => _yeildRate;
            set
            {
                _yeildRate = value;
                NotifyPropertyChanged(nameof(YeildRate));
            }
        }

        #region Binding Property
        // Declare event
        public event PropertyChangedEventHandler PropertyChanged;
        // NotifyPropertyChanged method to update property value in binding
        public virtual void NotifyPropertyChanged(string info = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
        #endregion

        public AutoRun()
        {
            InitializeComponent();
            DataContext = this;
            _loopWorker = new Worker(new ThreadStart(AutoRunProcess));


            BoardName = _program.Name;
            TotalPass = Properties.Settings.Default.PASS;
            TotalFail = Properties.Settings.Default.FAIL;
            TotalChecked = Properties.Settings.Default.TotalChecked;
            YeildRate = Properties.Settings.Default.YeildRate;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogInfo("AutoRun ========> Start AutoRun");
            StartUp();
            UpdateStatusControl("Running...", 0);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _database.Save();
            Stop();
            LogInfo("AutoRun ========> Stop AutoRun");
        }

        public bool StartUp()
        {
            try
            {
                _loopWorker.Start();
                if (!Prepare())
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }
        }

        private bool Prepare()
        {
            bool bRet = true;
            try
            {
                int nRet = _device.Ping();
                if (nRet != 1)
                {
                    bRet = false;
                }
            }
            catch (Exception ex)
            {
                bRet = false;
                LogError(ex.Message);
            }
            return bRet;
        }

        public void Stop()
        {
            if (_capture != null)
            {
                _capture.Dispose();
            }

            MainWindow.Current.Show();
        }

        private void mnuiOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
        }

        private void mnuiSetSpeedRobot_Click(object sender, RoutedEventArgs e)
        {
            dpnSetup.Children.Clear();
            dpnSetup.Children.Add(_setSpeed);
        }

        private void mnuiResetRate_Click(object sender, RoutedEventArgs e)
        {
            switch (IsAdmin())
            {
                case 1:
                    ResetRate();
                    break;
                case 2:
                    ResetFailRate();
                    break;

            }
            Properties.Settings.Default.Save();
            NotifyPropertyChanged();
        }

        private bool GetSignal(string device)
        {
            if (_device.PLC1.GetFlag(device) == 1)
            {
                _device.PLC1.SetBitDevice(device, 0);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void SetResultToPLC(FOVType pCheck, int pResult) // set pass fail voi PLC
        {
            string pass = "S3";
            string fail = "S4";
            string step1 = "S8";
            string step2 = "S9";
            FOVResult rLABEL = _FOVResults.Find(x => x.FOVType == pCheck);
            FOVResult rCOLOR = _FOVResults.Find(x => x.FOVType == pCheck);
            FOVResult rSOLDER_CAP = _FOVResults.Find(x => x.FOVType == pCheck);
            switch (pCheck)
            {
                case FOVType.Unknow:
                    break;
                case FOVType.L1_LABEL1:
                    {
                        // neu pass ->
                        var item = _database.Data.Find(x => x.SN == rLABEL.SN && x.IsStep1 == true);

                        if (rLABEL.SN != string.Empty && item != null)
                        {
                            if (SendTerminalCheck(pCheck) == 2) // overed sttation
                            {
                                _device.PLC1.SetDevice(step1, 1);
                                _device.PLC1.SetDevice(pass, pResult);
                                return;
                            }
                        }
                        if (rLABEL.SN != string.Empty && item == null)
                        {
                            if (SendTerminalCheck(pCheck) == 1) // in station
                            {
                                _device.PLC1.SetDevice(pass, pResult);
                                _database.AddData(rLABEL.SN);  // Add SN
                                return;
                            }
                            else if (SendTerminalCheck(pCheck) == -1) // timeout
                            {
                                _device.PLC1.SetDevice(fail, pResult);
                                return;
                            }
                        }
                        else
                        {
                            _device.PLC1.SetDevice(fail, pResult);
                        }
                        // neu fail ->
                    }
                    break;
                case FOVType.L1_LABEL2:
                    {
                        // neu pass ->
                        var item = _database.Data.Find(x => x.SN == rLABEL.SN && x.IsStep1 == true);

                        if (rLABEL.SN != string.Empty && item != null)
                        {
                            if (SendTerminalCheck(pCheck) == 2)
                            {
                                _device.PLC1.SetDevice(step1, 1);
                                _device.PLC1.SetDevice(pass, pResult);
                                return;
                            }
                        }
                        if (rLABEL.SN != string.Empty && item == null)
                        {
                            if (SendTerminalCheck(pCheck) == 1)
                            {
                                _device.PLC1.SetDevice(pass, pResult);

                                _database.AddData(rLABEL.SN);  ///////Add SN
                                return;
                            }
                            else if (SendTerminalCheck(pCheck) == -1)
                            {
                                _device.PLC1.SetDevice(fail, pResult);
                            }
                        }
                        else
                        {
                            _device.PLC1.SetDevice(fail, pResult);
                        }
                        // neu fail ->
                    }
                    break;
                case FOVType.L1_COLOR1:
                    {
                        if (rCOLOR.Result == 1)
                        {
                            _device.PLC1.SetDevice(pass, pResult);
                        }
                        else
                        {
                            _device.PLC1.SetDevice(fail, pResult);
                        }
                    }
                    break;
                case FOVType.L1_COLOR2:
                    {
                        if (rCOLOR.Result == 1)
                        {
                            _device.PLC1.SetDevice(pass, pResult);
                        }
                        else
                        {
                            _device.PLC1.SetDevice(fail, pResult);
                        }
                    }
                    break;
                case FOVType.L1_SOLDER_CAP1:
                    {
                        if (rSOLDER_CAP.Result == 1)
                        {
                            _device.PLC1.SetDevice(step1, pResult);
                            _device.PLC1.SetDevice(pass, pResult);
                            _database.Data.Find(x => x.SN == rLABEL.SN).IsStep1 = true;

                        }
                        else
                        {
                            _device.PLC1.SetDevice(step1, pResult);
                            if(WorkerConfirm(pCheck) ==1)
                            {
                                _device.PLC1.SetDevice(pass, pResult);
                            }
                            else
                            {
                                _device.PLC1.SetDevice(fail, pResult);
                            }
                            _database.Data.Find(x => x.SN == rLABEL.SN).IsStep1 = true;
                        }
                    }
                    break;

                case FOVType.L1_SOLDER_CAP2:
                    {
                        if (rSOLDER_CAP.Result == 1)
                        {
                            _device.PLC1.SetDevice(step1, pResult);
                            _device.PLC1.SetDevice(pass, pResult);
                            _database.Data.Find(x => x.SN == rLABEL.SN).IsStep1 = true;

                        }
                        else
                        {
                            _device.PLC1.SetDevice(step1, pResult);
                            if (WorkerConfirm(pCheck) == 1)
                            {
                                _device.PLC1.SetDevice(pass, pResult);
                            }
                            else
                            {
                                _device.PLC1.SetDevice(fail, pResult);
                            }
                            _database.Data.Find(x => x.SN == rLABEL.SN).IsStep1 = true;
                        }

                        break;
                    }
                    

                default:
                    break;
            }
        }

        private void CheckFOVResults(FOVType pCheck)
        {

            switch (pCheck)
            {
                case FOVType.Unknow:
                    break;
                case FOVType.L1_SOLDER_CAP1:
                    {
                        var f = _FOVResults.FirstOrDefault(x => x.FOVType == pCheck);
                        if (f != null)
                        {
                            //  SetResultToPLC(pCheck, f.Result);
                        }
                        break;
                    }
                case FOVType.L1_SOLDER_CAP2:
                    {
                        var f = _FOVResults.FirstOrDefault(x => x.FOVType == pCheck);
                        if (f != null)
                        {
                            //   SetResultToPLC(pCheck, f.Result);

                        }
                        break;
                    }
                case FOVType.L2_SOLDER_CAP1:
                    {
                        var f = _FOVResults.FirstOrDefault(x => x.FOVType == pCheck);
                        if (f != null)
                        {
                            //   SetResultToPLC(pCheck, f.Result);
                        }
                        break;
                    }
                case FOVType.L2_SOLDER_CAP2:
                    {
                        var f = _FOVResults.FirstOrDefault(x => x.FOVType == pCheck);
                        if (f != null)
                        {
                            //   SetResultToPLC(pCheck, f.Result);
                        }
                        break;
                    }
            }
        }

        public int WorkerConfirm(FOVType pSOLDER_CAP)
        {
            FOVResult result = _FOVResults.Find(x => x.FOVType == pSOLDER_CAP);
            if (_param.WorkerConfirm2)
            {
                string msg = "Đây có phải lỗi ảo không?\r\nNhấn 'OK' nếu lỗi ảo\r\rNhấn 'Cancel' nếu lỗi thật";
                MessageBoxResult mbr = MessageBox.Show(msg, "Xác nhận", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                if (mbr == MessageBoxResult.OK)
                {
                    result.WorkerConfirm = "WPASS";
                    //PASS-WORKER
                    return 1;
                }
                else
                {
                    result.WorkerConfirm = "WFAIL";
                    //FAIL-WORKER
                    return 0;
                }
            }
            return -1;
           
        }

        public void AutoRunProcess()
        {
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Name = "Auto run thread.";
            FOVType pCheck = FOVType.Unknow;
            string flagLane1 = "S200";
            string flagLane2 = "S202";
            string flagL1_LABLE1 = "S50";
            string flagL1_LABLE2 = "S51";
            string flagL1_COLOR1 = "S52";
            string flagL1_COLOR2 = "S53";
            string flagL1_SOLDER_CAP1 = "S100";
            string flagL1_SOLDER_CAP2 = "S101";

            string flagL2_LABLE1 = "S250";
            string flagL2_LABLE2 = "S251";
            string flagL2_COLOR1 = "S252";
            string flagL2_COLOR2 = "S253";
            string flagL2_SOLDER_CAP1 = "S300";
            string flagL2_SOLDER_CAP2 = "S301";

            string flagL1_End = "S201";
            string flagL2_End = "S203";

            while (_loopWorker.WaitStopSignal(100))
            {
                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    UpdateStatusControl("Processing...", -1);
                    ShowFOVImage(CameraMode.Top, null, FOVType.Unknow);
                    int step = 100 / 12;

                    //pCheck = FOVType.L1_LABLE1;
                    if (GetSignal(flagL1_LABLE1))
                    {
                        LogInfo($"AutoRunProcess =====> {FOVType.L2_LABEL1}");
                        UpdateStatusControl($"{FOVType.L2_LABEL1}", step * 1);
                        int nRet = CheckType(FOVType.L2_LABEL1);
                        string message = nRet == 1 ? $"{FOVType.L2_LABEL1}: Pass" : $"{FOVType.L2_LABEL1}: Fail";
                        // 
                        CheckFOVResult(FOVType.L1_LABEL1, FOVType.L1_COLOR1);
                       // CheckFOVResults(pCheck);
                    }

                    if (GetSignal(flagL1_LABLE2))
                    {
                        int nRet = CheckType(FOVType.L1_LABEL2);
                        CheckFOVResult(FOVType.L1_LABEL2, FOVType.L1_COLOR2);
                    }

                    if (GetSignal(flagL1_COLOR1))
                    {
                        int nRet = CheckType(FOVType.L1_COLOR1);
                        CheckFOVResult(FOVType.L1_LABEL1, FOVType.L1_COLOR1);
                    }

                    if (GetSignal(flagL1_COLOR2))
                    {
                        int nRet = CheckType(FOVType.L1_COLOR2);
                        CheckFOVResult(FOVType.L1_LABEL2, FOVType.L1_COLOR2);
                    }

                    if (GetSignal(flagL1_SOLDER_CAP1))
                    {
                        int nRet = CheckType(FOVType.L1_SOLDER_CAP1);
                    }

                    if (GetSignal(flagL1_SOLDER_CAP2))
                    {
                        int nRet = CheckType(FOVType.L1_SOLDER_CAP2);
                    }

                    if (GetSignal(flagL2_LABLE1))
                    {
                        int nRet = CheckType(FOVType.L2_LABEL1);
                    }

                    if (GetSignal(flagL2_LABLE2))
                    {
                        int nRet = CheckType(FOVType.L2_LABEL2);
                    }

                    if (GetSignal(flagL2_COLOR1))
                    {
                        int nRet = CheckType(FOVType.L2_COLOR1);
                    }

                    if (GetSignal(flagL2_COLOR2))
                    {
                        int nRet = CheckType(FOVType.L2_COLOR2);
                    }

                    if (GetSignal(flagL2_SOLDER_CAP1))
                    {
                        int nRet = CheckType(FOVType.L2_SOLDER_CAP1);
                    }

                    if (GetSignal(flagL2_SOLDER_CAP2))
                    {
                        int nRet = CheckType(FOVType.L2_SOLDER_CAP2);
                    }


                }
                catch (Exception ex)
                {
                    LogError(ex.Message);
                }
            }

        }



        private void imbImageBox1_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (imbImageBox1_L1.Image != null)
            {
                double newZoom = imbImageBox1_L1.ZoomScale * (e.Delta > 0 ? 1.2 : 0.8);
                newZoom = Math.Max(0.5, Math.Min(5, newZoom)); // gioi han ti le zoom
                imbImageBox1_L1.SetZoomScale(newZoom, e.Location);
            }
        }


        private void imbImageBox1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                imbImageBox1_L1.SetZoomScale(1, e.Location);
                imbImageBox1_L1.AutoScrollOffset = new System.Drawing.Point(0, 0);
            }
        }

        private void imbImageBox2_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (imbImageBox2_L1.Image != null)
            {
                double newZoom = imbImageBox2_L1.ZoomScale * (e.Delta > 0 ? 1.2 : 0.8);
                newZoom = Math.Max(0.5, Math.Min(5, newZoom)); // gioi han ti le zoom
                imbImageBox1_L1.SetZoomScale(newZoom, e.Location);
            }
        }

        private void imbImageBox2_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                imbImageBox2_L1.SetZoomScale(1, e.Location);
                imbImageBox2_L1.AutoScrollOffset = new System.Drawing.Point(0, 0);
            }
        }



        public int SendTerminalCheck(FOVType pLABLE)
        {
            FOVResult rLABEL = _FOVResults.Find(x => x.FOVType == pLABLE);
            if (_param.Terminal.IsEnabled)
            {
                string SN = rLABEL.SN;
                string data = SN.PadRight(25) + "CHECK";
                _device.Terminal.SerialWriteData(data);
                bool isTimeout = false;
                int timeout = 10000;
                for (int i = 0; i < timeout / 400; i++)
                {
                    string responseData = _device.Terminal.DataReceived;
                    if (responseData.Contains("Station1") && responseData.Contains("PASS"))
                    {
                        isTimeout = true;

                        return 1;
                    }
                    else if (responseData.Contains("Station2") && responseData.Contains("PASS"))
                    {
                        isTimeout = true;
                        return 2;
                    }
                    else if (responseData.Contains("ERRO"))
                    {
                        isTimeout = true;
                        return 0;
                    }
                    Thread.Sleep(25);
                }
                if (!isTimeout)
                {
                    return -1;
                }
            }
            return -2;
        }

        public int SendTerminalOK(FOVType pSOLDER_CAP)
        {
            string SN = string.Empty;
            FOVType type;
            if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP1)
            {
                type = FOVType.L1_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP2)
            {
                type = FOVType.L1_LABEL2;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP1)
            {
                type = FOVType.L2_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP2)
            {
                type = FOVType.L2_LABEL2;
            }
            else
            {
                type = FOVType.Unknow;
            }

            FOVResult temp = _FOVResults.Find(x => x.FOVType == type);
            if( temp != null)
            {
                SN = temp.SN;
            }

            if (_param.Terminal.IsEnabled)
            {
               
                string data = SN.PadRight(25) + "OK";
                _device.Terminal.SerialWriteData(data);
                bool isTimeout = false;
                int timeout = 10000;

                for (int i = 0; i < timeout / 400; i++)
                {
                    string respone = _device.Terminal.DataReceived;
                    if (respone == data + "PASS")
                    {
                        isTimeout = true;
                        return 1;
                    }
                    else if (respone == data + "ERRO")
                    {
                        isTimeout = true;
                        // FAIL
                        return 0;
                    }
                    Thread.Sleep(25);
                }

                if (!isTimeout)
                {

                    //FAIL-TIMEOUT
                    return -1;
                }
            }
            return -2;
        }

        public int SendTerminalNG(FOVType pSOLDER_CAP)
        {
            string SN = string.Empty;
            FOVType type;
            if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP1)
            {
                type = FOVType.L1_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP2)
            {
                type = FOVType.L1_LABEL2;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP1)
            {
                type = FOVType.L2_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP2)
            {
                type = FOVType.L2_LABEL2;
            }
            else
            {
                type = FOVType.Unknow;
            }


            FOVResult temp = _FOVResults.Find(x => x.FOVType == type);
            if(temp !=null)
            {
                SN = temp.SN;
            }
            if (_param.Terminal.IsEnabled)
            {
                string errorCode = "PCB001";
                string data = SN.PadRight(25) + errorCode.PadRight(10) + "NG";
                _device.Terminal.SerialWriteData(data);
                int timeout = 10000;
                for (int i = 0; i < timeout / 400; i++)
                {
                    string responseData = _device.Terminal.DataReceived;
                    if (responseData == data + "PASS" || responseData == data + "ERRO")
                        break;
                    Thread.Sleep(25);
                }
            }
            return -2;
        }




        public void CheckFOVResult(FOVType pLABLE, FOVType pCOLOR)
        {
            string pass = "S3"; // Pass
            string fail = "S4"; // Fail
            string step1 = "S8"; // Over Step1
            string step2 = "S9"; // Over Step2
            FOVResult rLABEL = _FOVResults.Find(x => x.FOVType == pLABLE);
            FOVResult rCOLOR = _FOVResults.Find(x => x.FOVType == pCOLOR);

            if (rLABEL != null && rCOLOR != null)
            {
                if (rLABEL.Result == 1 && rCOLOR.Result == 1)
                {
                    //Pass LABLE && PCB


                    //var item = _database.Data.Find(x => x.SN == rLABEL.SN && x.IsStep1 == true);
                    //if (item != null)
                    //{
                    //    _device.PLC1.SetDevice(fail, 1);
                    //    // STEP1 OVERED
                    //    return;
                    //}

                    if (_param.Terminal.IsEnabled)
                    {
                        string SN = rLABEL.SN;
                        if (SN == string.Empty)
                        {
                            _device.PLC1.SetDevice(fail, 1);
                            return;
                        }
                        string data = SN.PadRight(25) + "CHECK";
                        _device.Terminal.SerialWriteData(data);
                        bool isTimeout = false;
                        int timeout = 4000;
                        for (int i = 0; i < timeout / 400; i++)
                        {
                            string responeData = _device.Terminal.DataReceived;
                            if (responeData.Contains("SOLDER_CAP") && responeData.Contains("PASS"))
                            {
                                isTimeout = true;
                                _device.PLC1.SetDevice(pass, 1);
                                // SOLDER_CAP
                                return;
                            }
                            else if (responeData.Contains("") && responeData.Contains("PASS"))
                            {
                                isTimeout = true;
                                //Send overstep before send pass/fail
                                if (pLABLE == FOVType.L1_LABEL1 || pLABLE == FOVType.L1_LABEL2)
                                {
                                    _device.PLC1.SetDevice(step1, 1);
                                }
                                else if (pLABLE == FOVType.L2_LABEL1 || pLABLE == FOVType.L2_LABEL2)
                                {
                                    _device.PLC1.SetDevice(step2, 1);
                                }

                                _device.PLC1.SetDevice(pass, 1);
                                //  """
                                return;
                            }

                            else if (responeData.Contains("ERRO"))
                            {
                                isTimeout = true;
                                _device.PLC1.SetDevice(fail, 1);
                                // ERROR
                                return;
                            }
                            Thread.Sleep(25);
                        }

                        if (!isTimeout)
                        {
                            _device.PLC1.SetDevice(fail, 1);
                            // FAIL_TIMEOUT
                        }
                    }
                    else
                    {
                        _device.PLC1.SetDevice(pass, 1);
                        // PASS
                    }
                }
                else
                {

                    //Fail Lable && COLOR
                    if (_param.PLC1.IsEnabled)
                    {
                        string SN = rLABEL.SN;
                        if (SN == string.Empty)
                        {
                            _device.PLC1.SetDevice(fail, 1);
                            //Fail-SN
                            return;
                        }
                        _device.PLC1.SetDevice(fail, 1);
                        // Fail
                    }

                    else
                    {
                        _device.PLC1.SetDevice(fail, 1);
                        //FAIL
                    }
                }
            }
            else
            {
                if (rLABEL != null)
                {
                    _device.PLC1.SetDevice(rLABEL.Result == 1 ? pass : fail, 1);
                }
                if (rCOLOR != null)
                {
                    _device.PLC1.SetDevice(rCOLOR.Result == 1 ? pass : fail, 1);
                }
            }
        }


        public void CheckFOVResult(FOVType pSOLDER_CAP)
        {
            string SN = string.Empty;
            FOVType type;
            if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP1)
            {
                type = FOVType.L1_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L1_SOLDER_CAP2)
            {
                type = FOVType.L1_LABEL2;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP1)
            {
                type = FOVType.L2_LABEL1;
            }
            else if (pSOLDER_CAP == FOVType.L2_SOLDER_CAP2)
            {
                type = FOVType.L1_LABEL2;
            }
            else
            {
                type = FOVType.Unknow;
            }

            FOVResult temp = _FOVResults.Find(x => x.FOVType == type);
            if (temp != null)
            {
                SN = temp.SN;
            }

            string pass = "S3";
            string fail = "S4";
            string step1 = "S8"; // Over Step1
            string step2 = "S9"; // Over Step2
            if (SN == string.Empty)
            {
                _device.PLC1.SetDevice(fail, 1);
                // FAIL-SN
                return;
            }

            FOVResult result = _FOVResults.Find(x => x.FOVType == pSOLDER_CAP);
            if (result != null)
            {
                if (result.Result == 1)
                {
                    //PASS SOLDER_CAP
                    if (_param.Terminal.IsEnabled)
                    {
                        string data1 = SN.PadRight(25) + "OK";
                        string data2 = SN.PadRight(25) + "CHECK";
                        if (_database.Data.Find(x => x.SN == SN) != null)
                        {
                            _device.Terminal.SerialWriteData(data1);
                            bool isTimeout = false;
                            int timeout = 10000;
                            for (int i = 0; i < timeout / 400; i++)
                            {
                                string respone = _device.Terminal.DataReceived;
                                if (respone == data1 + "PASS")
                                {
                                    isTimeout = true;
                                    _device.PLC1.SetDevice(step2, 1);
                                    _device.PLC1.SetDevice(pass, 1);
                                    // PASS
                                    _database.RemoveData(_database.Data.Find(x => x.SN == SN)); // Delete SN in database
                                    return;
                                }
                                else if (respone == data1 + "ERRO")
                                {
                                    isTimeout = true;
                                    _device.PLC1.SetDevice(fail, 1);
                                    // FAIL
                                    return;
                                }
                                Thread.Sleep(25);
                            }

                            if (!isTimeout)
                            {
                                _device.PLC1.SetDevice(fail, 1);
                                //FAIL-TIMEOUT
                            }
                        }
                        else
                        {
                            _device.Terminal.SerialWriteData(data2);
                            bool isTimeout = false;
                            int timeout = 10000;
                            for (int i = 0; i < timeout / 400; i++)
                            {
                                string responeData = _device.Terminal.DataReceived;
                                if (responeData.Contains("SOLDER_CAP") && responeData == data1 + "PASS")
                                {
                                    isTimeout = true;
                                    _device.PLC1.SetDevice(step1, 1);
                                    _device.PLC1.SetDevice(pass, 1);
                                    // PASS "SOLDER_CAP"
                                    return;
                                }
                                else if (responeData.Contains("Station2") && responeData == data1 + "PASS")
                                {
                                    isTimeout = true;
                                    //Send overstep before send pass/fail
                                    if (type == FOVType.L1_LABEL1 || type == FOVType.L1_LABEL2)
                                    {
                                        _device.PLC1.SetDevice(step1, 1);
                                    }
                                    else if (type == FOVType.L2_LABEL1 || type == FOVType.L2_LABEL2)
                                    {
                                        _device.PLC1.SetDevice(step2, 1);
                                    }

                                    _device.PLC1.SetDevice(pass, 1);
                                    //  """
                                    return;
                                }
                                Thread.Sleep(25);
                            }
                            if (!isTimeout)
                            {
                                _device.PLC1.SetDevice(fail, 1);
                                // FAIL-TIMEOUT
                            }
                        }
                    }
                    else
                    {
                        _device.PLC1.SetDevice(pass, 1);
                        //PASS
                    }
                }
                else
                {
                    //FAIL SOLDER_CAP
                    if (_param.WorkerConfirm2)
                    {
                        string msg = "Đây có phải lỗi ảo không?\r\n Nhấn 'OK' nếu lỗi ảo\r\r Nhấn 'Cancel' nếu lỗi thật";
                        MessageBoxResult mbr = MessageBox.Show(msg, "Xác nhận", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                        if (mbr == MessageBoxResult.OK)
                        {
                            result.WorkerConfirm = "WPASS";
                            _device.PLC1.SetDevice(pass, 1);
                            //PASS-WORKER
                            return;
                        }

                        else

                        {
                            result.WorkerConfirm = "WFAIL";
                            _device.PLC1.SetDevice(fail, 1);
                            // WFAIL
                            if (_param.Terminal.IsEnabled)
                            {
                                string errorCode = "PCB001";
                                string data = SN.PadRight(25) + errorCode.PadRight(10) + "NG";
                                _device.Terminal.SerialWriteData(data);
                                int timeout = 10000;
                                for (int i = 0; i < timeout / 400; i++)
                                {
                                    string responseData = _device.Terminal.DataReceived;
                                    if (responseData == data + "PASS" || responseData == data + "ERRO")
                                        break;
                                    Thread.Sleep(25);
                                }
                            }
                        }
                    }
                    else
                    {
                        _device.PLC1.SetDevice(fail, 1);
                        //FAIL
                    }
                }
            }
        }



        private int CheckType(FOVType type, FOVType targetType = FOVType.Unknow)
        {
            try
            {
                FOVType tempType = targetType != FOVType.Unknow ? targetType : type;
                LogInfo($"CheckType: {tempType}");
                int fRet = 1;
                FOV pFOV = _program.FOVs.FirstOrDefault(x => x.IsEnable == true && x.FOVType == type);
                if (pFOV == null)
                {
                    fRet = -1;
                }
                else
                {
                    int nRet = CheckFOV(pFOV);
                    if (nRet != 1)
                    {
                        fRet = -1;
                    }
                }
                return fRet;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return -1;
            }
        }

        private int CheckFOV(FOV pFOV)
        {
            int fRet = 1;
            ShowFOVImage(pFOV.CameraMode, null, pFOV.FOVType);

            ICamera pCamera = GetFOVParams(pFOV.CameraMode);
            if (pCamera != null)
            {
                using (Bitmap bmp = GetFOVBitmap(pCamera, pFOV))
                {
                    if (bmp != null)
                    {
                        using (Image<Bgr, byte> src = bmp.ToImage<Bgr, byte>())
                        using (Image<Bgr, byte> dst = bmp.ToImage<Bgr, byte>())
                        {
                            IEnumerable<SMD> pSMDs = pFOV.SMDs.Where(x => x.IsEnabled = true);
                            if (pSMDs == null)
                            {
                                fRet = -1;
                            }
                            else
                            {
                                System.Windows.Point offset = GetOffsetROI(pFOV, src);
                                foreach (var SMD in pSMDs)
                                {
                                    Rectangle offsetROI = new Rectangle
                                    {
                                        X = SMD.ROI.Rectangle.X,
                                        Y = SMD.ROI.Rectangle.Y,
                                        Width = SMD.ROI.Rectangle.Width,
                                        Height = SMD.ROI.Rectangle.Height
                                    };

                                    src.ROI = offsetROI;
                                    dst.ROI = offsetROI;
                                    CvResult cvRet = CheckSMD(SMD, src.Copy(), dst);
                                    if (!cvRet.Result)
                                    {
                                        fRet = -1;
                                    }
                                    else
                                        fRet = 1;
                                    src.ROI = new Rectangle();
                                    dst.ROI = new Rectangle();
                                }
                            }
                            UpdateFOVResult(pFOV.FOVType, fRet, _SN, dst.Clone());
                            ShowFOVImage(pFOV.CameraMode, dst.Clone(), pFOV.FOVType);
                        }
                    }
                    else
                    {
                        fRet = -1;
                        string message = $"CheckFOV: No bitmap ({pFOV.CameraMode},{pFOV.FOVType}";
                        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                        LogError(message);
                    }
                }
            }
            else
            {
                fRet = -1;
                string message = $"CheckFOV: No Data ({pFOV.CameraMode}, {pFOV.FOVType})";
                LogError(message);
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
            return fRet;
        }

        private CvResult CheckSMD(SMD pSMD, Image<Bgr, byte> src, Image<Bgr, byte> dst)
        {
            CvResult cvRet = new CvResult();
            switch (pSMD.Algorithm)
            {
                case SMDAlgorithm.Unknow:
                    break;

                case SMDAlgorithm.TemplateMatching:
                    {
                        cvRet = pSMD.TemplateMatching.Run(src, dst, pSMD.ROI.Rectangle);
                        break;
                    }
                case SMDAlgorithm.CodeRecognition:
                    {
                        cvRet = pSMD.CodeRecognition.Run(src, dst, pSMD.ROI.Rectangle);
                        if (cvRet.Result)
                        {
                            _SN = cvRet.Content;
                            Console.WriteLine("SN= " + _SN);
                            LogsAutoRun("SN= " + _SN);
                        }
                        else
                        {
                            _SN = string.Empty;
                            Console.WriteLine("SN = Empty");
                            LogsAutoRun("SN = Empty");
                        }
                        break;
                    }
                case SMDAlgorithm.HSVExtraction:
                    {
                        cvRet = pSMD.HSVExtraction.Run(src, dst, pSMD.ROI.Rectangle);
                        break;
                    }
                default:
                    break;
            }
            return cvRet;
        }

        private Bitmap GetFOVBitmap(ICamera pCamera, FOV pFOV)
        {
            pCamera.SetParameter(KeyName.ExposureTime, pFOV.ExposureTime);
            pCamera.ClearImageBuffer();
            pCamera.SetParameter(KeyName.TriggerSoftware, 1);
            using (Bitmap bmp = pCamera.GrabFrame())
            {
                if (bmp != null)
                {
                    return (Bitmap)bmp.Clone();
                }
            }
            return null;
        }



        private System.Windows.Point GetOffsetROI(FOV pFOV, Image<Bgr, byte> image)
        {
            try
            {
                System.Windows.Point offset = new System.Windows.Point();
                using (Image<Bgr, byte> src = image.Clone())
                using (Image<Bgr, byte> dst = image.Clone())
                {
                    SMD pSMD = pFOV.SMDs.FirstOrDefault(x => x.IsEnabled == true && x.SMDType == SMDType.Mark);
                    if (pSMD != null)
                    {
                        src.ROI = pSMD.ROI.Rectangle;
                        dst.ROI = pSMD.ROI.Rectangle;
                        CvResult cvRet;
                        switch (pSMD.Algorithm)
                        {
                            case SMDAlgorithm.Unknow:
                                break;
                            case SMDAlgorithm.TemplateMatching:
                                {
                                    cvRet = pSMD.TemplateMatching.Run(src.Copy(), dst, pSMD.ROI.Rectangle);
                                    offset.X = (int)cvRet.Center.X - (int)pSMD.TemplateMatching.Center.X;
                                    offset.Y = (int)cvRet.Center.Y - (int)pSMD.TemplateMatching.Center.Y;
                                    break;
                                }
                            case SMDAlgorithm.CodeRecognition:
                                break;
                            case SMDAlgorithm.HSVExtraction:
                                break;
                            default:
                                break;
                        }
                        src.ROI = new Rectangle();
                        dst.ROI = new Rectangle();
                        ShowFOVImage(pFOV.CameraMode, dst.Clone(), pFOV.FOVType);
                    }
                }
                return offset;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return new System.Windows.Point();
            }
        }

        public void ShowFOVImage(CameraMode cameraMode, Image<Bgr, byte> image, FOVType fovtype)
        {
            if (image != null)
            {
                if (cameraMode != CameraMode.Unknow)
                {
                    if (fovtype == FOVType.L1_LABEL1 || fovtype == FOVType.L1_COLOR1 || fovtype == FOVType.L1_SOLDER_CAP1)
                    {
                        imbImageBox1_L1.Image = image;
                    }
                    else if (fovtype == FOVType.L1_LABEL2 || fovtype == FOVType.L1_COLOR2 || fovtype == FOVType.L1_SOLDER_CAP2)
                    {
                        imbImageBox2_L1.Image = image;
                    }
                    else if (fovtype == FOVType.L2_LABEL1 || fovtype == FOVType.L2_COLOR1 || fovtype == FOVType.L2_SOLDER_CAP1)
                    {
                        imbImageBox1_L2.Image = image;
                    }
                    else
                    {
                        imbImageBox2_L2.Image = image;
                    }
                }
            }
            else
            {
                if (fovtype == FOVType.L1_LABEL1 || fovtype == FOVType.L1_COLOR1 || fovtype == FOVType.L1_SOLDER_CAP1)
                {
                    imbImageBox1_L1.Image = null;
                }
                else if (fovtype == FOVType.L1_LABEL2 || fovtype == FOVType.L1_COLOR2 || fovtype == FOVType.L1_SOLDER_CAP2)
                {
                    imbImageBox2_L1.Image = null;
                }
                else if (fovtype == FOVType.L1_LABEL1 || fovtype == FOVType.L1_COLOR1 || fovtype == FOVType.L1_SOLDER_CAP1)
                {
                    imbImageBox1_L2.Image = null;
                }
                else
                {
                    imbImageBox2_L2.Image = null;
                }
            }
        }

        private ICamera GetFOVParams(CameraMode mode)
        {
            DeviceManager device = DeviceManager.Current;
            ICamera camera;
            if (mode == CameraMode.Top)
            {
                camera = device.Camera1;
            }
            else if (mode == CameraMode.Bottom)
            {
                camera = device.Camera2;
            }
            else
            {
                camera = null;
            }
            return camera;
        }


        #region StreamCamera
        private void OpenWebcam()
        {
            Task.Run(() =>
            {
                CvInvoke.UseOpenCL = false;
                try
                {
                    _frame = new Mat();
                    _capture = new VideoCapture();
                    _capture.ImageGrabbed += ProcessFrame;
                    _capture.Start();
                }
                catch (NullReferenceException excpt)
                {
                    MessageBox.Show(excpt.Message);
                }
            });
        }

        private void ProcessFrame(object sender, EventArgs arg)
        {
            if (_capture != null && _capture.Ptr != IntPtr.Zero)
            {
                _capture.Retrieve(_frame, 0);
                Dispatcher.Invoke(() => imbImageBox1_L1.Image = _frame);
            }
        }


        #endregion
        //=======================================================================================
        private void mnuiTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FOV pFOV = _program.FOVs[0];
                int nRet = CheckFOV0(pFOV);
                //lblResultPCB1.Content = nRet == 1 ? "PASS" : "FAIL";
                //lblResultPCB1.FontWeight = FontWeights.Bold;
                //lblResultPCB1.FontSize = 50;
                //lblResultPCB1.Foreground = nRet == 1 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
                //borderResultPCB1.Background = nRet == 1 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                SetRate();
                NotifyPropertyChanged();
                UpdateStatusControl("Complete!", 100);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }



        public int CheckFOV0(FOV pFOV)
        {
            int fRet = 1;
            string filepath = _program.ImageBoard.Blocks[0].Filename;
            Bitmap bmp = new Bitmap(filepath);
            if (bmp != null)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                using (Image<Bgr, byte> src = bmp.ToImage<Bgr, byte>())
                using (Image<Bgr, byte> dst = bmp.ToImage<Bgr, byte>())
                {
                    IEnumerable<SMD> pSMDs = pFOV.SMDs.Where(x => x.IsEnabled == true);
                    if (pSMDs == null)
                    {
                        fRet = -1;
                    }

                    else
                    {
                        System.Windows.Point offset = GetOffsetROI(pFOV, src);
                        foreach (var SMD in pSMDs)
                        {
                            Rectangle offsetROI = new Rectangle
                            {
                                X = (int)(SMD.ROI.Rectangle.X + offset.X),
                                Y = (int)(SMD.ROI.Rectangle.Y + offset.Y),
                                Width = SMD.ROI.Rectangle.Width,
                                Height = SMD.ROI.Rectangle.Height
                            };

                            src.ROI = offsetROI;
                            dst.ROI = offsetROI;
                            CvResult cvRet = CheckSMD(SMD, src.Copy(), dst);
                            if (cvRet.Result)
                            {
                                Console.WriteLine($"Check SMD_{SMD.Id} : OK");
                                LogsAutoRun($"Check SMD_{SMD.Id} : OK");

                            }
                            else
                            {
                                Console.WriteLine($"Check SMD_{SMD.Id} : FAIL");
                                LogsAutoRun($"Check SMD_{SMD.Id} : FAIL");
                                fRet = -1;
                            }

                            src.ROI = new Rectangle();
                            dst.ROI = new Rectangle();


                        }
                        stopwatch.Stop();
                        CycleTime = stopwatch.Elapsed.ToString();
                        txbCycleTime.Text = CycleTime;
                    }
                    UpdateFOVResult(pFOV.FOVType, fRet, _SN, dst.Clone());
                    ShowFOVImage(pFOV.CameraMode, dst.Clone(), pFOV.FOVType);

                    //===================================TEST==============================
                    _database.AddData(_SN);
                    //_database.RemoveData(_database.Data.First(x => x.SN == _SN));

                }
            }
            return fRet;
        }
        // ===============================================================================

        #region logs
        public void LogInfo(string message)
        {
            Logger.Current.Info(message);
        }

        public void LogError(string message)
        {
            Logger.Current.Error(message);
        }

        #endregion


        public void UpdateFOVResult(FOVType fovtype, int result, string SN, Image<Bgr, byte> image)
        {
            //fovResult.FOVType = fovtype;
            //fovResult.Result = result;
            //fovResult.SN = SN;
            //fovResult.Image = image;

            FOVResult item = _FOVResults.Find(x => x.FOVType == fovtype);
            if (item != null)
            {
                item.FOVType = fovtype;
                item.Result = result;
                item.SN = SN;
                item.Image = image;
            }
            else
            {
                FOVResult temp = new FOVResult
                {
                    FOVType = fovtype,
                    Result = result,
                    SN = SN,
                    Image = image
                };
                _FOVResults.Add(temp);
            }


        }

        public void LogsAutoRun(string message)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            int MAX_LINES = 500;
            Dispatcher.Invoke(() =>
            {
                // Thêm dòng mới vào Textbox
                txtLogsAutoRun.AppendText(currentTime + "   " + "[INFO]" + "   " + message + Environment.NewLine);
                // Kiểm tra số lượng dòng và xóa các dòng cũ hơn nếu vượt quá giới hạn
                int lineCount = txtLogsAutoRun.LineCount;
                if (lineCount > MAX_LINES)
                {
                    int indexFirstLineToRemove = txtLogsAutoRun.GetCharacterIndexFromLineIndex(0);
                    int indexLastLineToRemove = txtLogsAutoRun.GetCharacterIndexFromLineIndex(lineCount - MAX_LINES);
                    txtLogsAutoRun.Select(indexFirstLineToRemove, indexLastLineToRemove);
                    txtLogsAutoRun.SelectedText = "";

                }
                txtLogsAutoRun.ScrollToEnd();
            });

        }

        public void UpdateStatusControl(string text, int progress)
        {
            txbStatusBar.Text = text;
            prbStatus.Value = progress;
        }


        public void SetRate()
        {
            var p = Properties.Settings.Default;
            if (fovResult.Result == 1)
            {
                p.PASS++;
                TotalPass = p.PASS;
            }
            else
            {
                p.FAIL++;
                TotalFail = p.FAIL;
            }

            p.TotalChecked = p.PASS + p.FAIL;
            TotalChecked = p.TotalChecked;

            p.YeildRate = (float)p.PASS / p.TotalChecked * 100;
            YeildRate = p.YeildRate;

            p.Save();
            NotifyPropertyChanged();
        }



        public void ResetRate()
        {
            var p = Properties.Settings.Default;
            p.Reset();
            TotalPass = p.PASS;
            TotalFail = p.FAIL;
            TotalChecked = p.TotalChecked;
            YeildRate = p.YeildRate;
            MessageBox.Show("Reseted Rate", "Reset", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }

        public void ResetFailRate()
        {
            var p = Properties.Settings.Default;
            p.FAIL = 0;
            TotalFail = p.FAIL;

            p.TotalChecked = p.PASS + p.FAIL;
            TotalChecked = p.TotalChecked;

            p.YeildRate = (float)p.PASS / p.TotalChecked * 100;
            YeildRate = p.YeildRate;
            MessageBox.Show("Reseted FailRate", "Reset", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }


        public int IsAdmin()
        {
            LoginDialog loginDialog = new LoginDialog();
            loginDialog.ShowDialog();
            if (loginDialog.txtUsername.Text == "admin" && loginDialog.Password.Password == "admin" && !loginDialog.Cancel())
            {
                return 1;
            }
            else if (loginDialog.txtUsername.Text == "at" && loginDialog.Password.Password == "foxconnat" && !loginDialog.Cancel())
            {
                return 2;
            }
            else if (loginDialog.Cancel())
            {
                loginDialog.Close();
                return 0;
            }
            else
                return -1;

        }

        public class FOVResult
        {
            public FOVType FOVType { get; set; }
            public int Result { get; set; }
            public string SN { get; set; }
            public Image<Bgr, byte> Image { get; set; }
            public string WorkerConfirm { get; set; }


            public FOVResult()
            {
                FOVType = FOVType.Unknow;
                Result = -1;
                SN = string.Empty;
                Image = null;
                WorkerConfirm = string.Empty;
            }
        }

        private void txtLogsAutoRun_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {

        }
    }

}
