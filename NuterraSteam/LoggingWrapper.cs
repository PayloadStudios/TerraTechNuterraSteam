using System;
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
        private static object Logger;

        public static void Init()
        {

        }

        public static void Log(string args)
        {
            if (LogManagerAvailable)
            {

            }
            else
            {
                Debug.Log(args);
            }
        }

        public static void LogError(string args)
        {
            if (LogManagerAvailable)
            {

            }
            else
            {
                Debug.LogError(args);
            }
        }

        public static void LogError(Exception args)
        {
            if (LogManagerAvailable)
            {

            }
            else
            {
                Debug.LogError(args);
            }
        }

        public static void LogWarning(params object[] args)
        {
            if (LogManagerAvailable)
            {

            }
            else
            {
                Debug.LogWarning(args);
            }
        }
    }
}
