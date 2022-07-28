﻿using System;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules.Logging
{
    internal class Logger
    {
        private enum LogLevel : byte
        {
            TRACE = 0,
            DEBUG = 1,
            INFO = 2,
            WARN = 3,
            ERROR = 4,
            FATAL = 5,
            OFF = 6
        }

        internal struct TargetConfig
        {
            public string path;
            public string layout;
            public bool keepOldFiles;

            internal TargetConfig(string path = null, string layout = null, bool keepOldFiles = false)
            {
                this.path = path;
                this.layout = layout;
                this.keepOldFiles = keepOldFiles;
            }
        }

        public object logger;
        public readonly byte minLoggingLevel;
        public readonly string loggerID;
        public readonly string path = null;
        public readonly string layout = null;
        public readonly bool keepOldFiles = false;

        internal string logPath = "";

        internal Logger(string loggerID, TargetConfig config = default, byte defaultLogLevel = (byte)LogLevel.INFO)
        {
            this.loggerID = loggerID;
            this.minLoggingLevel = defaultLogLevel;

            // Setup targeting configs
            this.path = config.path;
            this.layout = config.layout;
            this.keepOldFiles = config.keepOldFiles;

            // Read in configured logging level
            string loggingLevelStr = null;
            string[] commandLineArgs = CommandLineReader.GetCommandLineArgs();
            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                Console.WriteLine($"Checking command line arg {commandLineArgs[i]}");
                if (commandLineArgs[i] == "+log_level" && i < commandLineArgs.Length - 1)
                {
                    if (loggingLevelStr == null)
                    {
                        Console.WriteLine($"[{loggerID}] General log level of {commandLineArgs[i + 1]} read");
                        loggingLevelStr = commandLineArgs[i + 1];
                    }
                }
                else if (commandLineArgs[i] == $"+log_level_{loggerID}" && i < commandLineArgs.Length - 1)
                {
                    string overrideStatement = loggingLevelStr == null ? "" : $", overriding general log level of {loggingLevelStr}";
                    Console.WriteLine($"[{loggerID}] Custom log level of {commandLineArgs[i + 1]} read{overrideStatement}");
                    loggingLevelStr = commandLineArgs[i + 1];
                }
            }

            // Assign the correct level to the logger
            try
            {
                LogLevel loggingLevel = (LogLevel)Enum.Parse(typeof(LogLevel), loggingLevelStr, true);
                this.minLoggingLevel = (byte) loggingLevel;
                Console.WriteLine($"[{loggerID}] Logging {loggingLevel} and up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing log level of {loggingLevelStr}");
                Console.WriteLine(ex.ToString());
                Console.WriteLine($"[{loggerID}] {loggingLevelStr} is unrecognized logging level. Defaulting to {this.minLoggingLevel}");
            }

            // Perform any injected setup
            this.Setup();
        }

        public void Setup() { }

        private void Log(byte level, string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")} | {(LogLevel) level} | {loggerID} | {message}");
        }

        private void LogException(byte level, Exception exception)
        {
            Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")} | {(LogLevel)level} | {loggerID} | {exception.ToString()}");
        }

        private void LogException(byte level, Exception exception, string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")} | {(LogLevel)level} | {loggerID} | {message}:\n {exception.ToString()}");
        }

        internal void Trace(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.TRACE)
            {
                Log((byte)LogLevel.TRACE, message);
            }
        }

        internal void Debug(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.DEBUG)
            {
                Log((byte)LogLevel.DEBUG, message);
            }
        }

        internal void Info(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.INFO)
            {
                Log((byte)LogLevel.INFO, message);
            }
        }

        internal void Fatal(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.FATAL)
            {
                Log((byte)LogLevel.FATAL, message);
            }
        }

        internal void Fatal(Exception exception, string message = null)
        {
            if (minLoggingLevel <= (byte)LogLevel.FATAL)
            {
                if (message != null)
                {
                    LogException((byte)LogLevel.FATAL, exception, message);
                }
                else
                {
                    LogException((byte)LogLevel.FATAL, exception);
                }
            }
        }

        internal void Error(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.ERROR)
            {
                Log((byte)LogLevel.ERROR, message);
            }
        }

        internal void Error(Exception exception, string message = null)
        {
            if (minLoggingLevel <= (byte)LogLevel.ERROR)
            {
                if (message != null)
                {
                    LogException((byte) LogLevel.ERROR, exception, message);
                }
                else
                {
                    LogException((byte)LogLevel.ERROR, exception);
                }
            }
        }

        internal void Warn(string message)
        {
            if (minLoggingLevel <= (byte)LogLevel.WARN)
            {
                Log((byte)LogLevel.WARN, message);
            }
        }
    }
}
