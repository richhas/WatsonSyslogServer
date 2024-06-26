﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonSyslog
{
    /// <summary>
    /// Settings.
    /// </summary>
    public class Settings
    {
        #region Public-Members

        /// <summary>
        /// UDP port on which to listen.
        /// </summary>
        public int UdpPort
        {
            get
            {
                return _UdpPort;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(UdpPort));
                _UdpPort = value;
            }
        }

        /// <summary>
        /// Flag to enable or disable displaying timestamps.
        /// </summary>
        public bool DisplayTimestamps { get; set; } = false;

        /// <summary>
        /// Directory in which to write log files.
        /// </summary>
        public string LogFileDirectory { get; set; } = "./logs/";

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename { get; set; } = "log.txt";

        /// <summary>
        /// Number of seconds between each log file update.
        /// </summary>
        public int LogWriterIntervalSec
        {
            get
            {
                return _LogWriterIntervalSec;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(LogWriterIntervalSec));
                _LogWriterIntervalSec = value;
            }
        }

        /// <summary>
        /// Maximum log file age in days.
        /// </summary>
        public int MaxLogFileAgeInDays { 
            get {
                return _MaxLogFileAgeInDays;
            }
            set {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxLogFileAgeInDays));
                _MaxLogFileAgeInDays = value;
            }
        }

        #endregion

        #region Private-Members

        private int _UdpPort = 514;
        private int _LogWriterIntervalSec = 10;

        private int _MaxLogFileAgeInDays = 7;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Settings()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human readable representation of the object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  UDP port              : " + UdpPort + Environment.NewLine);
            sb.Append("  Display timestamps    : " + DisplayTimestamps + Environment.NewLine);
            sb.Append("  Log file directory    : " + LogFileDirectory + Environment.NewLine);
            sb.Append("  Log filename          : " + LogFilename + Environment.NewLine);
            sb.Append("  Writer interval (sec) : " + LogWriterIntervalSec + Environment.NewLine);
            sb.Append("  Max log age (days)    : " + MaxLogFileAgeInDays + Environment.NewLine);
            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
