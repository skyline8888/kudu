﻿using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Settings
{
    public static class ScmHostingConfigurations
    {
        public readonly static string ConfigsFile = 
            System.Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\SiteExtensions\kudu\ScmHostingConfigurations.txt");

        private static Dictionary<string, string> _configs;
        private static DateTime _configsTTL;

        // for mocking purpose
        public static Dictionary<string, string> Config
        {
            get { return _configs; }
            set
            {
                _configs = value;
                _configsTTL = value != null ? DateTime.UtcNow.AddMinutes(10) : DateTime.MinValue;
            }
        }

        public static int ArmRetryAfterSeconds
        {
            get
            {
                const int DefaultArmRetryAfterSeconds = 30;
                if (!int.TryParse(GetValue("ArmRetryAfterSeconds"), out int secs))
                {
                    return DefaultArmRetryAfterSeconds;
                }

                return Math.Max(10, secs);
            }
        }

        public static bool GetLatestDeploymentOptimized
        {
            get { return GetValue("GetLatestDeploymentOptimized", "1") != "0"; }
        }

        public static bool DeploymentStatusCompleteFileEnabled
        {
            get { return GetValue("DeploymentStatusCompleteFileEnabled", "1") != "0"; }
        }

        public static int TelemetryIntervalMinutes
        {
            get { return int.TryParse(GetValue("TelemetryIntervalMinutes"), out int value) ? value : 30; }
        }

        public static string GetValue(string key, string defaultValue = null)
        {
            var env = System.Environment.GetEnvironmentVariable($"SCM_{key}");
            if (!string.IsNullOrEmpty(env))
            {
                return env;
            }

            var configs = _configs;
            if (configs == null || DateTime.UtcNow > _configsTTL)
            {
                _configsTTL = DateTime.UtcNow.AddMinutes(10);

                try
                {
                    var settings = FileSystemHelpers.FileExists(ConfigsFile) 
                        ? FileSystemHelpers.ReadAllText(ConfigsFile) 
                        : null;

                    KuduEventSource.Log.GenericEvent(
                        ServerConfiguration.GetRuntimeSiteName(),
                        $"ScmHostingConfigurations: Update value '{settings}'",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty);

                    configs = Parse(settings);
                    _configs = configs;
                }
                catch (Exception ex)
                {
                    KuduEventSource.Log.KuduException(
                        ServerConfiguration.GetRuntimeSiteName(),
                        "ScmHostingConfigurations.GetValue",
                        string.Empty,
                        string.Empty,
                        $"ScmHostingConfigurations: Fail to GetValue('{key}')",
                        ex.ToString());
                }
            }

            return (configs == null || !configs.TryGetValue(key, out string value)) ? defaultValue : value;
        }

        public static Dictionary<string, string> Parse(string settings)
        {
            return string.IsNullOrEmpty(settings)
                ? new Dictionary<string, string>()
                : settings
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
                    .Where(a => a.Length == 2)
                    .ToDictionary(a => a[0], a => a[1], StringComparer.OrdinalIgnoreCase);
        }
    }
}
