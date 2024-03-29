﻿using Foxconn.Editor.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Foxconn.Editor
{
    public class MachineParams
    {
        private string _filePath = @$"{AppDomain.CurrentDomain.BaseDirectory}params\MachineParams.json";
        private CameraParams _camera1 = new CameraParams();
        private CameraParams _camera2 = new CameraParams();
        private LightParams _light1 = new LightParams();
        private LightParams _light2 = new LightParams();
        private SocketParams _plc1 = new SocketParams();
        private SocketParams _plc2 = new SocketParams();
        private SocketParams _robot = new SocketParams();
        private ScannerParams _scanner1 = new ScannerParams();
        private ScannerParams _scanner2 = new ScannerParams();
        private TerminalParams _terminalIT = new TerminalParams();
        private TerminalParams _terminalBoard = new TerminalParams();
        private MachineCalibrationParams _machineCalibration = new MachineCalibrationParams();
        private AxisLimitParams _axislimit = new AxisLimitParams();
        private FTPParams _ftp = new FTPParams();
        private FTPLogParams _ftpLogs = new FTPLogParams();

        private CheckStationCable _stationCable = new CheckStationCable();
        private CheckStationAdapter _stationAdapter = new CheckStationAdapter();
        private CheckStationMAC _stationMAC = new CheckStationMAC();
        private CheckBoxSN _boxSN = new CheckBoxSN();
        private List<string> _labelAdapter = new List<string>()
        {
            "381.00622.005AB",
        };
        private List<string> _labelCable = new List<string>()
        {
            "371.01176.005AB",
        };
        private int _delayCaptures = 0;
        private bool _workerCofirm1 = false;
        private bool _workerConfirm = false;
        private bool _debugMode = false;

        public CameraParams Camera1 => _camera1;
        public CameraParams Camera2 => _camera2;
        public LightParams Light1 => _light1;
        public LightParams Light2 => _light2;
        public SocketParams PLC1 => _plc1;
        public SocketParams PLC2 => _plc2;
        public SocketParams Robot => _robot;
        public ScannerParams Scanner1 => _scanner1;
        public ScannerParams Scanner2 => _scanner2;
        public TerminalParams TerminalIT => _terminalIT;
        public TerminalParams TerminalBoard => _terminalBoard;
        public MachineCalibrationParams MachineCalibration => _machineCalibration;
        public AxisLimitParams AxisLimit => _axislimit;
        public FTPParams FTP => _ftp;
        public FTPLogParams FTPLogs => _ftpLogs;

        public CheckStationCable StationCable => _stationCable;
        public CheckStationAdapter StationAdapter => _stationAdapter;
        public CheckStationMAC StationMAC => _stationMAC;
        public CheckBoxSN BoxSN => _boxSN;

        public List<string> LabelAdapter => _labelAdapter;
        public List<string> LabelCable => _labelCable;
        public int DelayCaptures
        {
            get => _delayCaptures;
            set => _delayCaptures = value;
        }
        public bool WorkerConfirm1
        {
            get => _workerCofirm1;
            set => _workerCofirm1 = value;
        }
        public bool WorkerConfirm
        {
            get => _workerConfirm;
            set => _workerConfirm = value;
        }
        public bool DebugMode
        {
            get => _debugMode;
            set => _debugMode = value;
        }

        public static MachineParams Current => __current;
        private static MachineParams __current = new MachineParams();
        public MachineParams() { }
        static MachineParams() { }

        public static void Reload()
        {
            MachineParams machineParams = new MachineParams();
            MachineParams loaded = machineParams.Load();
            if (loaded != null)
            {
                __current = loaded;
            }
            else
            {
                machineParams.Save();
            }
        }
        public MachineParams Load()
        {
            MachineParams machineParams = null;
            if (File.Exists(_filePath))
            {
                string contents = File.ReadAllText(_filePath);
                machineParams = JsonConvert.DeserializeObject<MachineParams>(contents);
            }
            return machineParams;
        }

        public void Save()
        {
            string contents = JsonConvert.SerializeObject(__current, Formatting.Indented);
            File.WriteAllText(_filePath, contents);
        }

        public class CameraParams
        {
            private bool _isEnabled = false;
            private string _userDefinedName = string.Empty;
            private CameraType _type = CameraType.Unknow;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string UserDefinedName
            {
                get => _userDefinedName;
                set => _userDefinedName = value;
            }

            public CameraType Type
            {
                get => _type;
                set => _type = value;
            }
        }

        public class LightParams
        {
            private bool _isEnabled = false;
            private string _portName = string.Empty;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string PortName
            {
                get => _portName;
                set => _portName = value;
            }
        }

        public class SocketParams
        {
            private bool _isEnabled = false;
            private string _host = string.Empty;
            private int _port = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string Host
            {
                get => _host;
                set => _host = value;
            }

            public int Port
            {
                get => _port;
                set => _port = value;
            }
        }

        public class ScannerParams
        {
            private bool _isEnabled = false;
            private string _portName = string.Empty;
            private int _dataLength = 0;
            private bool _isSubstring = false;
            private int _startIndex = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string PortName
            {
                get => _portName;
                set => _portName = value;
            }

            public int DataLength
            {
                get => _dataLength;
                set => _dataLength = value;
            }

            public bool IsSubstring
            {
                get => _isSubstring;
                set => _isSubstring = value;
            }

            public int StartIndex
            {
                get => _startIndex;
                set => _startIndex = value;
            }
        }

        public class TerminalParams
        {
            private bool _isEnabled = false;
            private string _portName = string.Empty;
            private string _undo = "UNDO";
            private string _user = "125H";
            private string _format = string.Empty;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string PortName
            {
                get => _portName;
                set => _portName = value;
            }

            public string Undo
            {
                get => _undo;
                set => _undo = value;
            }

            public string User
            {
                get => _user;
                set => _user = value;
            }

            public string Format
            {
                get => _format;
                set => _format = value;
            }
        }

        public class MachineCalibrationParams
        {
            private double _c1 = 0;
            private double _c2 = 0;
            private double _dx = 0;
            private double _dy = 0;
            private double _dz = 0;
            private double _dr = 0;

            public double C1
            {
                get => _c1;
                set => _c1 = value;
            }

            public double C2
            {
                get => _c2;
                set => _c2 = value;
            }

            public double X
            {
                get => _dx;
                set => _dx = value;
            }

            public double Y
            {
                get => _dy;
                set => _dy = value;
            }

            public double Z
            {
                get => _dz;
                set => _dz = value;
            }

            public double R
            {
                get => _dr;
                set => _dr = value;
            }
        }

        public class AxisLimitParams
        {
            private bool _isEnabled = false;
            private double _x = 0;
            private double _y = 0;
            private double _z = 0;
            private double _r = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public double X
            {
                get => _x;
                set => _x = value;
            }

            public double Y
            {
                get => _y;
                set => _y = value;
            }

            public double Z
            {
                get => _z;
                set => _z = value;
            }

            public double R
            {
                get => _r;
                set => _r = value;
            }
        }

        public class FTPParams
        {
            private bool _isEnabled = false;
            private string _host = string.Empty;
            private string _user = string.Empty;
            private string _password = string.Empty;
            private string _versionFile = string.Empty;
            private string _updateFile = string.Empty;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string Host
            {
                get => _host;
                set => _host = value;
            }

            public string User
            {
                get => _user;
                set => _user = value;
            }

            public string Password
            {
                get => _password;
                set => _password = value;
            }

            public string VersionFile
            {
                get => _versionFile;
                set => _versionFile = value;
            }

            public string UpdateFile
            {
                get => _updateFile;
                set => _updateFile = value;
            }
        }

        public class FTPLogParams
        {
            private bool _isEnabled = false;
            private string _host = string.Empty;
            private string _user = string.Empty;
            private string _password = string.Empty;
            private string _logDirectory = string.Empty;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public string Host
            {
                get => _host;
                set => _host = value;
            }

            public string User
            {
                get => _user;
                set => _user = value;
            }

            public string Password
            {
                get => _password;
                set => _password = value;
            }

            public string LogDirectory
            {
                get => _logDirectory;
                set => _logDirectory = value;
            }
        }

        public class CheckStationCable
        {
            private bool _isEnable = false;
            private bool _isSubstring = false;
            private int _startIndex = 0;
            private int _length = 0;

            public bool IsEnabled
            {
                get => _isEnable;
                set => _isEnable = value;
            }

            public bool IsSubstring
            {
                get => _isSubstring;
                set => _isSubstring = value;
            }

            public int StartIndex
            {
                get => _startIndex;
                set => _startIndex = value;
            }

            public int Length
            {
                get => _length;
                set => _length = value;
            }
        }

        public class CheckStationAdapter
        {
            private bool _isEnabled = false;
            private bool _isSubstring = false;
            private int _startIndex = 0;
            private int _length = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public bool IsSubstring
            {
                get => _isSubstring;
                set => _isSubstring = value;
            }

            public int StartIndex
            {
                get => _startIndex;
                set => _startIndex = value;
            }

            public int Length
            {
                get => _length;
                set => _length = value;
            }
        }

        public class CheckStationMAC
        {
            private bool _isEnabled = false;
            private bool _isSubstring = false;
            private int _startIndex = 0;
            private int _length = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public bool IsSubstring
            {
                get => _isSubstring;
                set => _isSubstring = value;
            }

            public int StartIndex
            {
                get => _startIndex;
                set => _startIndex = value;
            }

            public int Length
            {
                get => _length;
                set => _length = value;
            }
        }

        public class CheckBoxSN
        {
            private bool _isEnabled = false;
            private bool _isSubstring = false;
            private int _startIndex = 0;
            private int _length = 0;

            public bool IsEnabled
            {
                get => _isEnabled;
                set => _isEnabled = value;
            }

            public bool IsSubstring
            {
                get => _isSubstring;
                set => _isSubstring = value;
            }

            public int StartIndex
            {
                get => _startIndex;
                set => _startIndex = value;
            }

            public int Length
            {
                get => _length;
                set => _length = value;
            }
        }
    }
}
