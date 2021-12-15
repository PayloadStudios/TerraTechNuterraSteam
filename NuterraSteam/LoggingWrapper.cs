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
            Type logManager = logManagerAssembly.GetType("LogManager.Manager", true);
            Console.WriteLine("[NuterraSteam] Found LogManager");
            LogManagerAvailable = true;

            Type logConfigType = logManager.GetNestedTypes().FirstOrDefault(t => t.Name.Contains("LogConfig"));
            object logManagerConfig = Activator.CreateInstance(logConfigType);
            Console.WriteLine("Created config");

            FieldInfo layout = logConfigType.GetField("layout");
            layout.SetValue(logManagerConfig, "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${logger:shortName=true} | ${message}  ${exception}");
            Console.WriteLine("Set layout");

            FieldInfo keepOldfiles = logConfigType.GetField("keepOldFiles");
            keepOldfiles.SetValue(logManagerConfig, false);
            Console.WriteLine("Set file retention");

            FieldInfo minLevel = logConfigType.GetField("minLevel");
            minLevel.SetValue(logManagerConfig, traceLevel);
            Console.WriteLine("Set logging level");

            MethodInfo registerLogger = logManager.GetMethod("RegisterLogger", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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

        public static void Init()
        {
            IEnumerable<Assembly> nlogSearch = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith("NLog,"));
            if (nlogSearch.Count() > 0)
            {
                Assembly nlog = nlogSearch.FirstOrDefault();
                Type NLogManager = nlog.GetType("NLog.LogManager", true);
                MethodInfo getLogger = NLogManager.GetMethod("GetLogger", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                logger = (object) getLogger.Invoke(null, new object[] { "NuterraSteam" });

                InitLoggers(nlog);

                IEnumerable<Assembly> logManagerSearch = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith("LogManager,"));
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

        public static void Trace(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                trace.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogFormat(message, args);
            }
        }

        public static void Debug(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                debug.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogFormat(message, args);
            }
        }

        public static void Info(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                info.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogFormat(message, args);
            }
        }

        public static void Fatal(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                fatal.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogErrorFormat(message, args);
            }
        }

        public static void Fatal(Exception exception, string message = null, params object[] args)
        {
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
                if (message == null)
                {
                    UnityEngine.Debug.LogError(exception);
                }
                else
                {
                    UnityEngine.Debug.LogError(String.Format(message, args));
                    UnityEngine.Debug.LogError(exception);
                }
            }
        }

        public static void Error(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                error.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogErrorFormat(message, args);
            }
        }

        public static void Error(Exception exception, string message = null, params object[] args)
        {
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
                if (message == null)
                {
                    UnityEngine.Debug.LogError(exception);
                }
                else
                {
                    UnityEngine.Debug.LogError(String.Format(message, args));
                    UnityEngine.Debug.LogError(exception);
                }
            }
        }

        public static void Warn(string message, params object[] args)
        {
            if (LogManagerAvailable)
            {
                warn.Invoke(logger, new object[] { message, args });
            }
            else
            {
                UnityEngine.Debug.LogWarningFormat(message, args);
            }
        }
    }
}
