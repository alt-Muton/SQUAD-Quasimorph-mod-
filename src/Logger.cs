using System;
using System.Reflection;
using UnityEngine;

namespace Squad_Leader
{
    public class Logger
    {
        public string LogPrefix { get; set; }

        public Logger(string logPrefix = "")
        {
            if (string.IsNullOrEmpty(logPrefix))
                logPrefix = Assembly.GetExecutingAssembly().GetName().Name;

            LogPrefix = logPrefix;
        }

        public void Log(string message)
        {
            Debug.Log("[" + LogPrefix + "] " + message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning("[" + LogPrefix + "] " + message);
        }

        public void LogError(string message)
        {
            Debug.LogError("[" + LogPrefix + "] " + message);
        }

        public void LogException(Exception ex)
        {
            Debug.LogError("[" + LogPrefix + "] Exception Logged:");
            Debug.LogException(ex);
        }
    }
}
