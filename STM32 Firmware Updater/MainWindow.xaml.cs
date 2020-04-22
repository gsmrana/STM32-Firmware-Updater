using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using LibUsbDotNet.DeviceNotify;
using Microsoft.Win32;

namespace STM32_Firmware_Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Data

        CommandLine _cmdLine;
        string _firmwareFilename = "";
        bool _upgradingInProgresss = false;

        string DfuSeDemo = "Tools\\DfuSeDemo.exe";
        string DfuFileMgr = "Tools\\DfuFileMgr.exe";
        string DfuSeCommand = "Tools\\DfuSeCommand.exe";
        string DriverX86 = "Drivers\\dpinst_x86.exe";
        string DriverAmd64 = "Drivers\\dpinst_amd64.exe";
        string Vcredist12 = "Environment\\vcredist12_x86.exe";

        readonly StringBuilder cmdresponse = new StringBuilder();
        readonly DispatcherTimer _deviceUpdatetimer = new DispatcherTimer();
        readonly IDeviceNotifier _usbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();

        #endregion

        #region ctor

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainApp_Initialized(object sender, EventArgs e)
        {
            try
            {
                DfuSeDemo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DfuSeDemo);
                DfuFileMgr = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DfuFileMgr);
                DfuSeCommand = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DfuSeCommand);
                DriverX86 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DriverX86);
                DriverAmd64 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DriverAmd64);
                Vcredist12 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Vcredist12);

                var args = Environment.GetCommandLineArgs();
                if (args.Length > 2)
                {
                    bool installerRun = false;
                    if (string.Compare(args[1], "vcredist12", true) == 0)
                    {
                        if (!File.Exists(Path.Combine(Environment.SystemDirectory, "mfc120.dll")) ||
                            !File.Exists(Path.Combine(Environment.SystemDirectory, "msvcr120.dll")))
                        {
                            installerRun = true;
                            Process.Start(Vcredist12);
                        }
                    }

                    if (string.Compare(args[2], "dpinst", true) == 0)
                    {
                        installerRun = true;
                        if (Environment.Is64BitOperatingSystem)
                            Process.Start(DriverAmd64);
                        else
                            Process.Start(DriverX86);
                    }
                    if (installerRun) this.Close();
                }
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void MainApp_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                RichTextBoxEventLog.Width = 500;

                _cmdLine = new CommandLine(DfuSeCommand);
                _cmdLine.OnDataReceived += CommandLine_OnDataReceived;

                _deviceUpdatetimer.Interval = TimeSpan.FromSeconds(1);
                _deviceUpdatetimer.Tick += Timer_Tick;
                _deviceUpdatetimer.Start();
                _usbDeviceNotifier.OnDeviceNotify += UsbDeviceNotifier_OnDeviceNotify;

                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    SelectDfuFile(args[1]);
                }
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void MainApp_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_upgradingInProgresss)
                {
                    e.Cancel = true;
                    return;
                }
                _cmdLine.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        #endregion

        #region Event Handlers

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                _deviceUpdatetimer.Stop();
                RunGetDfuDeviceListThread();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Timer_Tick Exception: " + ex.Message);
            }
        }

        private void UsbDeviceNotifier_OnDeviceNotify(object sender, DeviceNotifyEventArgs e)
        {
            try
            {
                Debug.WriteLine(string.Format("UsbEvent --> {0}, {1}", e.EventType, e.DeviceType));
                _deviceUpdatetimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UsbDeviceNotifier_OnDeviceNotify Exception: " + ex.Message);
            }
        }

        #endregion

        #region Helper Methods

        private void AppendEventLog(string message, bool appendNewLine = true)
        {
            if (appendNewLine) message += "\r";
            Dispatcher.Invoke(() =>
            {
                RichTextBoxEventLog.AppendText(message);
                RichTextBoxEventLog.ScrollToEnd();
            });
        }

        private void UpdateDfuDeviceState(int index, string name)
        {
            Dispatcher.Invoke(() =>
            {
                EllipseDeviceReady.Fill = index > 0 ? Brushes.LimeGreen : Brushes.Gray;
                TextBlockDeviceName.Foreground = index > 0 ? Brushes.Blue : Brushes.Black;
                TextBlockDeviceName.Text = name;
            });
        }

        private void UpdateProgress(int percent, string status = "IDLE", string timespan = "00:00")
        {
            var backcolor = Brushes.LightGray;
            if (status.Contains("SUCCESS")) backcolor = Brushes.MediumSeaGreen;
            else if (status.Contains("ERROR")) backcolor = Brushes.Orchid;
            Dispatcher.Invoke(() =>
            {
                ProgressBarPercent.Value = percent;
                TextBlockStatus.Background = backcolor;
                TextBlockStatus.Text = "Status: " + status;
                TextBlockTimespan.Text = "Timespan: " + timespan;
            });
        }

        private void PopupException(string message, string caption = "Exception")
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void SelectDfuFile(string filename)
        {
            if (string.Compare(Path.GetExtension(filename), ".dfu", true) != 0)
                throw new Exception("Only DFU (*.dfu) file format is supported!");

            _firmwareFilename = filename;
            var selectedfile = Path.GetFileName(_firmwareFilename);
            this.Title = string.Format("{0} - {1}", Application.ResourceAssembly.GetName().Name, selectedfile);
            AppendEventLog("Firmware: " + selectedfile);
        }

        #endregion

        #region Form Events

        private void MainApp_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = DragDropEffects.All;
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void MainApp_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    SelectDfuFile(files[0]);
                }
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void EllipseDeviceReady_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount > 1)
                    Process.Start(DfuSeDemo);
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void TextBlockDeviceName_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount > 1)
                    Process.Start(DfuFileMgr);
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void ButtonUpgrade_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_upgradingInProgresss) return;
                RichTextBoxEventLog.Document.Blocks.Clear();

                if (!File.Exists(_firmwareFilename))
                {
                    var ofd = new OpenFileDialog
                    {
                        Filter = "DFU files (*.dfu)|*.dfu|All files (*.*)|*.*"
                    };
                    if (ofd.ShowDialog() != true)
                        return;

                    SelectDfuFile(ofd.FileName);
                }

                if (!File.Exists(DfuSeCommand))
                    throw new Exception(DfuSeCommand + " commandline tool not found!");

                RunUpdateFirmwareThread();
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        private void RunGetDfuDeviceListThread()
        {
            if (_upgradingInProgresss) return;

            Task.Run(() =>
            {
                try
                {
                    cmdresponse.Clear();
                    _cmdLine.CmdlineExecTimeoutSec = 1;
                    _cmdLine.Execute("-c --de");
                    Thread.Sleep(100); // finish the Rx
                    var response = cmdresponse.ToString();
                    Debug.WriteLine("Cmdline Response: \r" + response);
                    ParseDeviceQueryResponse(response, out int index, out string name);
                    if (index > 0)
                    {
                        UpdateDfuDeviceState(index, name);
                        UpdateProgress(0, "READY");
                    }
                    else
                    {
                        UpdateDfuDeviceState(0, "No DFU device found!");
                        UpdateProgress(0, "IDLE");
                    }
                    _cmdLine.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("RunGetDfuDeviceListProcess Exception: " + ex.Message);
                }
            });
        }

        #endregion

        #region Firmware Update Process

        private void RunUpdateFirmwareThread()
        {
            Task.Run(() =>
            {
                try
                {
                    _upgradingInProgresss = true;
                    ResetResponseHistory();
                    UpdateProgress(0, "STARTING");
                    _cmdLine.CmdlineExecTimeoutSec = 60;
                    _cmdLine.Execute(string.Format("-c --de 0 --al 0 -d --v --fn \"{0}\"", _firmwareFilename));
                    _cmdLine.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("RunUpdateFirmwareProcess Exception: " + ex.Message);
                }
                Thread.Sleep(100); //finish the Rx
                _upgradingInProgresss = false;
            });
        }

        private void CommandLine_OnDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            try
            {
                if (!_upgradingInProgresss)
                {
                    cmdresponse.AppendLine(e.Data);
                    return;
                }

                AppendEventLog(e.Data);
                ParseUpgradingResponses(e.Data, out int percent, out string status, out string timespan);
                UpdateProgress(percent, status, timespan);
                if (status == "ERROR")
                {
                    try { _cmdLine.Close(); } catch { }
                    _upgradingInProgresss = false;
                }
            }
            catch (Exception ex)
            {
                PopupException(ex.Message);
            }
        }

        int _lastpercent;
        string _laststatus;
        string _lasttimespan;

        private void ResetResponseHistory()
        {
            _lastpercent = 0;
            _laststatus = "STARTING";
            _lasttimespan = "00:00";
        }

        private void ParseUpgradingResponses(string line, out int percent, out string status, out string timespan)
        {
            percent = _lastpercent;
            status = _laststatus;
            timespan = _lasttimespan;

            try
            {
                // ### sample response data
                //1 Device(s) found : 
                //Device[1]: STM Device in DFU Mode, having[4] alternate targets
                // Duration: 00:00:00
                //Target 00: Upgrading - Erase Phase (58)... Duration: 00:00:03
                //Target 00: Upgrading - Download Phase (80)... Duration: 00:00:25
                //Target 00: Upgrading - Download Phase (90)... Duration: 00:00:35
                //Upgrade successful !
                // Duration: 00:00:00
                //Target 00: Uploading (95)... Duration: 00:00:01
                //Target 00: Uploading(100)...
                //Verify successful !

                if (line.Contains("Download"))
                    status = "DOWNLOADING";
                else if (line.Contains("Erase"))
                    status = "ERASING";
                else if (line.Contains("Uploading"))
                    status = "VERIFYING";
                else if (line.Contains("successful"))
                    status = "SUCCESSFULL";
                else if (line.Contains("error") || line.Contains("0 Device(s) found"))
                    status = "ERROR";

                var start = line.IndexOf('(');
                var end = line.IndexOf(')');
                if (start > 34 && end > start)
                {
                    percent = int.Parse(line.Substring(start + 1, end - start - 1));
                }

                start = line.IndexOf("... Duration:");
                if (start > 38)
                {
                    timespan = line.Substring(start + 17, 5);
                }

                _lastpercent = percent;
                _laststatus = status;
                _lasttimespan = timespan;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ParseUpgradingResponses Exception: " + ex.Message);
            }
        }

        private void ParseDeviceQueryResponse(string response, out int index, out string name)
        {
            index = 0;
            name = "";

            try
            {
                // ### sample response
                //0 Device(s) found. Plug your DFU Device !
                //1 Device(s) found :
                //Device [1]: STM Device in DFU Mode, having [4] alternate targets
                //bad parameter [--de]

                // Press any key to continue ...

                if (string.IsNullOrEmpty(response))
                    return;

                if (response.Contains("error") || response.Contains("0 Device(s) found"))
                    return;

                var start = response.IndexOf("Device [");
                if (start > 20)
                {
                    index = int.Parse(response[start + 8].ToString());
                }

                var end = response.IndexOf(", having [");
                if (start > 20 && end > start)
                {
                    name = response.Substring(start + 12, end - start - 12);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ParseDeviceQueryResponse Exception: " + ex.Message);
            }
        }

        #endregion

    }
}
