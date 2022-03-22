using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules
{
    internal static class LoggingWrapper
    {
        private static bool LogManagerAvailable = false;
        private static object logger = null;

        private static object NLogConfig = null;
        private static readonly string LogsDir = Path.Combine(NuterraMod.TTSteamDir, "Logs");

        private static FieldInfo debugField;
        private static FieldInfo errorField;
        private static FieldInfo fatalField;
        private static FieldInfo infoField;
        private static FieldInfo offField;
        private static FieldInfo traceField;
        private static FieldInfo warnField;

        private static object debugLevel;
        private static object errorLevel;
        private static object fatalLevel;
        private static object infoLevel;
        private static object offLevel;
        private static object traceLevel;
        private static object warnLevel;

        private static MethodInfo debug;
        private static MethodInfo error;
        private static MethodInfo errorException;
        private static MethodInfo errorParams;
        private static MethodInfo fatal;
        private static MethodInfo fatalException;
        private static MethodInfo fatalParams;
        private static MethodInfo info;
        private static MethodInfo trace;
        private static MethodInfo warn;

        private enum LogLevel : byte
        {
            Trace = 0,
            Debug = 1,
            Info = 2,
            Warn = 3,
            Error = 4,
            Fatal = 5,
            Off = 6
        }

        private static byte loggingLevel = (byte) LogLevel.Warn;

        private static bool inited = false;

        private static void InitNLogIntegration(Assembly nlog)
        {
            Console.WriteLine("[NuterraSteam] FAILED to find LogManager! - Setting up NLog ourselves");
            Type nLogConfigType = nlog.GetType("NLog.Config.LoggingConfiguration", true);
            NLogConfig = Activator.CreateInstance(nLogConfigType);

            // Rule for default console
            Type consoleTarget = nlog.GetType("NLog.Targets.ConsoleTarget", true);
            PropertyInfo consoleLayout = consoleTarget.GetProperty("Layout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var logconsole = Activator.CreateInstance(consoleTarget, "logconsole");
            consoleLayout.SetValue(logconsole, "[${logger:shortName=true}] ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${time} | ${message}  ${exception}");

            // Targets where to log to: File and Console
            Type fileTarget = nlog.GetType("NLog.Targets.FileTarget", true);
            PropertyInfo fileLayout = fileTarget.GetProperty("FileName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo fileName = fileTarget.GetProperty("Layout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo fileEnableDelete = fileTarget.GetProperty("EnableFileDelete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo fileDeleteOldFile = fileTarget.GetProperty("DeleteOldFileOnStartup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var logfile = Activator.CreateInstance(fileTarget, "logfile-NuterraSteam");
            fileLayout.SetValue(logfile, "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${logger:shortName=true} | ${message}  ${exception}");
            fileName.SetValue(logfile, Path.Combine(LogsDir, $"NuterraSteam.log"));
            fileEnableDelete.SetValue(logfile, true);
            fileDeleteOldFile.SetValue(logfile, true);

            // Rules for mapping loggers to targets
            // Our config:
            // * Log only Info and up
            MethodInfo addConfigRule = nLogConfigType.GetMethod("AddRule", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            addConfigRule.Invoke(NLogConfig, new object[] { infoLevel, fatalLevel, logfile, "NuterraSteam" });

            Type logManager = nlog.GetType("NLog.LogManager", true);

            // Apply config
            PropertyInfo config = logManager.GetProperty("Configuration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            config.SetValue(null, NLogConfig);

            // Setup
            MethodInfo setup = logManager.GetMethod("Setup", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var setupBuilder = setup.Invoke(null, null);
            Type setupBuilderType = setupBuilder.GetType();

            MethodInfo setupExtensions = setupBuilderType.GetMethod("SetupExtensions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo setupInternalLogger = setupBuilderType.GetMethod("SetupInternalLogger", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Action<object> setupExtensionsDelegate = s => {
                s.GetType().GetMethod("AutoLoadAssemblies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(s, new object[] { false });
            };
            setupExtensions.Invoke(setupBuilder, new object[] { });

            Action<object> internalLoggerDelegate = s =>
            {
                var logLevelSet = s.GetType().GetMethod("SetMinimumLogLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(s, new object[] { warnLevel });
                logLevelSet.GetType().GetMethod("NLogInternal.txt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(s, null);
            };
            setupInternalLogger.Invoke(setupBuilder, new object[] { internalLoggerDelegate });
        }

        private static void InitLogManagerIntegration(Assembly logManagerAssembly)
        {
            Type logManager = logManagerAssembly.GetType("LogManager.TTLogManager", true);
            Console.WriteLine("[NuterraSteam] Found LogManager.TTLogManager");
            LogManagerAvailable = true;

            Type logConfigType = logManagerAssembly.GetType("LogManager.LogConfig", true);
            object logManagerConfig = Activator.CreateInstance(logConfigType);
            Console.WriteLine("Created config");

            FieldInfo layout = logConfigType.GetField("layout");
            layout.SetValue(logManagerConfig, "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${logger:shortName=true} | ${message}  ${exception}");
            Console.WriteLine("Set layout");

            FieldInfo keepOldfiles = logConfigType.GetField("keepOldFiles");
            keepOldfiles.SetValue(logManagerConfig, false);
            Console.WriteLine("Set file retention");

            FieldInfo minLevel = logConfigType.GetField("defaultMinLevel");
            var logLevel = infoLevel;
            switch((LogLevel) loggingLevel)
            {
                case LogLevel.Trace:
                    logLevel = traceLevel;
                    break;
                case LogLevel.Info:
                    logLevel = infoLevel;
                    break;
                case LogLevel.Debug:
                    logLevel = debugLevel;
                    break;
                case LogLevel.Warn:
                    logLevel = warnLevel;
                    break;
                case LogLevel.Error:
                    logLevel = errorLevel;
                    break;
                case LogLevel.Fatal:
                    logLevel = fatalLevel;
                    break;
                case LogLevel.Off:
                    logLevel = offLevel;
                    break;
                default:
                    break;
            }
            minLevel.SetValue(logManagerConfig, logLevel);
            Console.WriteLine("Set logging level");

            MethodInfo registerLogger = logManager.GetMethod(
                "RegisterLogger",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { logger.GetType(), logConfigType },
                null
            );
            registerLogger.Invoke(null, new object[] { logger, logManagerConfig });
            Console.WriteLine("Registered logger");
        }

        private static void InitLoggers(Assembly nlog)
        {
            Type loggerType = nlog.GetType("NLog.Logger", true);
            Type logLevel = nlog.GetType("NLog.LogLevel", true);
            traceField = logLevel.GetField("Trace", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            traceLevel = traceField.GetValue(null);
            trace = loggerType.GetMethod("Trace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);

            warnField = logLevel.GetField("Warn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            warnLevel = warnField.GetValue(null);
            warn = loggerType.GetMethod("Warn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);

            fatalField = logLevel.GetField("Fatal", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            fatalLevel = fatalField.GetValue(null);
            fatal = loggerType.GetMethod("Fatal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);
            fatalException = loggerType.GetMethod("Fatal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Exception) }, null);
            fatalParams = loggerType.GetMethod("Fatal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Exception), typeof(string), typeof(object[]) }, null);

            errorField = logLevel.GetField("Error", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            errorLevel = errorField.GetValue(null);
            error = loggerType.GetMethod("Error", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);
            errorException = loggerType.GetMethod("Error", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Exception) }, null);
            errorParams = loggerType.GetMethod("Error", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Exception), typeof(string), typeof(object[]) }, null);

            infoField = logLevel.GetField("Info", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            infoLevel = infoField.GetValue(null);
            info = loggerType.GetMethod("Info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);

            debugField = logLevel.GetField("Debug", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            debugLevel = debugField.GetValue(null);
            debug = loggerType.GetMethod("Debug", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);

            offField = logLevel.GetField("Off", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            offLevel = offField.GetValue(null);
        }

        private static void ReadLoggingLevel()
        {
            string loggingLevelStr = null;
            string generalLevel = CommandLineReader.GetArgument("+log_level");
            if (generalLevel != null)
            {
                loggingLevelStr = generalLevel;
            }
            string modLevel = CommandLineReader.GetArgument("+log_level_NuterraSteam");
            if (modLevel != null)
            {
                loggingLevelStr = modLevel;
            }

            switch (loggingLevelStr)
            {
                case "info":
                    Console.WriteLine($"[NuterraSteam] Logging info and up");
                    loggingLevel = (byte)LogLevel.Info;
                    break;
                case "trace":
                    Console.WriteLine($"[NuterraSteam] Logging trace and up");
                    loggingLevel = (byte)LogLevel.Trace;
                    break;
                case "debug":
                    Console.WriteLine($"[NuterraSteam] Logging debug and up");
                    loggingLevel = (byte)LogLevel.Debug;
                    break;
                case "warn":
                    Console.WriteLine($"[NuterraSteam] Logging warnings and up");
                    loggingLevel = (byte)LogLevel.Warn;
                    break;
                case "error":
                    Console.WriteLine($"[NuterraSteam] Logging errors only");
                    loggingLevel = (byte)LogLevel.Error;
                    break;
                case "fatal":
                    Console.WriteLine($"[NuterraSteam] Logging fatals only");
                    loggingLevel = (byte)LogLevel.Fatal;
                    break;
                case "off":
                    Console.WriteLine($"[NuterraSteam] Logging is disabled");
                    loggingLevel = (byte)LogLevel.Off;
                    break;
                default:
                    Console.WriteLine($"[NuterraSteam] {loggingLevelStr} is unrecognized logging level. Defaulting to {loggingLevel}");
                    break;
            }
        }

        public static void Init()
        {
            if (!inited)
            {
                ReadLoggingLevel();
                inited = true;
                IEnumerable<Assembly> nlogSearch = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith("NLog,"));
                if (nlogSearch.Count() > 0)
                {
                    Assembly nlog = nlogSearch.FirstOrDefault();
                    Type NLogManager = nlog.GetType("NLog.LogManager", true);
                    MethodInfo getLogger = NLogManager.GetMethod("GetLogger", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                    logger = (object)getLogger.Invoke(null, new object[] { "NuterraSteam" });

                    InitLoggers(nlog);

                    IEnumerable<Assembly> logManagerSearch = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith("NLogManager,"));
                    if (logManagerSearch.Count() > 0)
                    {
                        Assembly logManagerAssembly = logManagerSearch.FirstOrDefault();
                        InitLogManagerIntegration(logManagerAssembly);
                    }
                    else
                    {
                        // disable bare NLog integration
                        // InitNLogIntegration(nlog);
                    }
                }
                else
                {
                    Console.WriteLine("[NuterraSteam] NLog not found - resorting to default logging");
                }
            }
        }

        public static void Trace(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Trace)
            {
                if (LogManagerAvailable)
                {
                    trace.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.Log(message);
                }
            }
        }

        public static void Debug(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Debug)
            {
                if (LogManagerAvailable)
                {
                    debug.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.Log(message);
                }
            }
        }

        public static void Info(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Info)
            {
                if (LogManagerAvailable)
                {
                    info.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.Log(message);
                }
            }
        }

        public static void Fatal(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Fatal)
            {
                if (LogManagerAvailable)
                {
                    fatal.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.Log(message);
                }
            }
        }

        public static void Fatal(Exception exception, string message = null, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Fatal) {
                if (LogManagerAvailable)
                {
                    if (message == null)
                    {
                        fatalException.Invoke(logger, new object[] { exception });
                    }
                    else
                    {
                        fatalParams.Invoke(logger, new object[] { exception, message, args });
                    }
                }
                else
                {
                    if (message != null)
                    {
                        if (args?.Length > 0)
                        {
                            UnityEngine.Debug.LogError(String.Format(message, args));
                        }
                        else
                        {
                            UnityEngine.Debug.LogError(message);
                        }
                    }
                    UnityEngine.Debug.LogError(exception);
                }
            }
        }

        public static void Error(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Error)
            {
                if (LogManagerAvailable)
                {
                    error.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogErrorFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.LogError(message);
                }
            }
        }

        public static void Error(Exception exception, string message = null, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Error) {
                if (LogManagerAvailable)
                {
                    if (message == null)
                    {
                        errorException.Invoke(logger, new object[] { exception });
                    }
                    else
                    {
                        errorParams.Invoke(logger, new object[] { exception, message, args });
                    }
                }
                else
                {
                    if (message != null)
                    {
                        if (args?.Length > 0)
                        {
                            UnityEngine.Debug.LogError(String.Format(message, args));
                        }
                        else
                        {
                            UnityEngine.Debug.LogError(message);
                        }
                    }
                    UnityEngine.Debug.LogError(exception);
                }
            }
        }

        public static void Warn(string message, params object[] args)
        {
            if (loggingLevel <= (byte) LogLevel.Warn)
            {
                if (LogManagerAvailable)
                {
                    warn.Invoke(logger, new object[] { message, args });
                }
                else if (args?.Length > 0)
                {
                    UnityEngine.Debug.LogWarningFormat(message, args);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(message);
                }
            }
        }
    }
}
