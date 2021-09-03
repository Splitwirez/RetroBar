﻿using ManagedShell.Common.Logging;
using ManagedShell.Common.Logging.Observers;
using System;
using System.IO;

namespace RetroBar.Utilities
{
    class ManagedShellLogger : IDisposable
    {
        private string _logPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar"), "Logs");
        private string _logName = DateTime.Now.ToString("yyyy-MM-dd_HHmmssfff");
        private string _logExt = "log";
        private LogSeverity _logSeverity = LogSeverity.Debug;
        private TimeSpan _logRetention = new TimeSpan(7, 0, 0);
        private FileLog _fileLog;

        public ManagedShellLogger()
        {
            SetupLogging();
        }

        private void SetupLogging()
        {
            ShellLogger.Severity = _logSeverity;

            SetupFileLog();

            ShellLogger.Attach(new ConsoleLog());
        }

        private void SetupFileLog()
        {
            DeleteOldLogFiles();

            _fileLog = new FileLog(Path.Combine(_logPath, $"{_logName}.{_logExt}"));
            _fileLog?.Open();

            ShellLogger.Attach(_fileLog);
        }

        private void DeleteOldLogFiles()
        {
            try
            {
                // look for all of the log files
                DirectoryInfo info = new DirectoryInfo(_logPath);
                FileInfo[] files = info.GetFiles($"*.{_logExt}", SearchOption.TopDirectoryOnly);

                // delete any files that are older than the retention period
                DateTime now = DateTime.Now;
                foreach (FileInfo file in files)
                {
                    if (now.Subtract(file.LastWriteTime) > _logRetention)
                    {
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                ShellLogger.Debug($"Unable to delete old log files: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _fileLog?.Dispose();
        }
    }
}
