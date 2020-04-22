using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace STM32_Firmware_Updater
{
    public class CommandLine
    {
        #region Data

        Process _process;
        readonly string _cmdlinetoolname;
        public int CmdlineExecTimeoutSec { get; set; } = 10;

        public event DataReceivedEventHandler OnDataReceived;

        #endregion

        public CommandLine(string cmdlinetool)
        {
            _cmdlinetoolname = cmdlinetool;
        }

        public void Execute(string args)
        {
            var startInfo = new ProcessStartInfo(_cmdlinetoolname)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            _process = new Process
            {
                StartInfo = startInfo
            };

            _process.StartInfo.Arguments = args;
            _process.ErrorDataReceived += OnDataReceived;
            _process.OutputDataReceived += OnDataReceived;

            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            var starttime = DateTime.Now;
            while (!_process.HasExited)
            {
                Thread.Sleep(500);
                if (DateTime.Now.Subtract(starttime).TotalSeconds > CmdlineExecTimeoutSec)
                {
                    _process.Kill();
                    //throw new Exception("CLI Execution Timeout!");
                }
            }
        }

        public void Close()
        {
            if (_process != null)
            {
                if (!_process.WaitForExit(1000))
                    _process.Kill();
                _process.Close();
            }
        }
    }
}
