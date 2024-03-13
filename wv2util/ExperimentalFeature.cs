using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace wv2util
{
    public class ExperimentalFeature : IEquatable<ExperimentalFeature>, IComparable<ExperimentalFeature>
    {
        public string Name { get; set; }
        
        public bool isEnabled;

        public bool IsEnabled
        {
            get { return isEnabled; }
            set
            {
                if (value)
                {
                    turnOn();
                }
                else
                {
                    turnOff();
                }
                isEnabled = value;
            }
        }

        public string Description { get; set; }

        private Action turnOn, turnOff;

        public ExperimentalFeature(Action turnOn, Action turnOff, Func<bool> isOn)
        {
            this.turnOn = turnOn;
            this.turnOff = turnOff;
            isEnabled = isOn();
        }

        static public bool IsEnabledForApp(string commandLine, string envVars)
        {
            // TODO implement
            return false;
        }

        public int CompareTo(ExperimentalFeature other)
        {
            return Name.CompareTo(other.Name);
        }

        public bool Equals(ExperimentalFeature other)
        {
            return Name.Equals(other.Name);
        }
    }

    public class EnvVarExperimentalFeature : ExperimentalFeature
    {
        public EnvVarExperimentalFeature(string envVar, string onVal, string offVal) : base(
            () =>
            {
                // Turn on:
                Environment.SetEnvironmentVariable(envVar, onVal, EnvironmentVariableTarget.User);
            },
            () =>
            {
                // Turn off:
                Environment.SetEnvironmentVariable(envVar, offVal, EnvironmentVariableTarget.User);
            },
            () =>
            {
                string val = Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.User);
                return val != null && val == onVal;
            })
        {
        }
    }

    public class ExperimentalFeatureList : ObservableCollection<ExperimentalFeature>
    {
        private List<ExperimentalFeature> m_List = new List<ExperimentalFeature>();

        public ExperimentalFeatureList()
        {
            // Visual Hosting
            Items.Add(new EnvVarExperimentalFeature(
                "COREWEBVIEW2_FORCED_HOSTING_MODE",
                "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL",
                "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_WINDOW")
            {
                Name = "Visual hosting",
                Description = "When on apps will be running in Visual Hosting instead of regular Window hosting"
            });

            // Canary self-hosting
            Items.Add(new EnvVarExperimentalFeature(
                "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
                "1",
                null)
            {
                Name = "Canary self-hosting",
                Description = "When on apps will use canary WebView2 runtime if installed instead of stable"
            });

            // To add more experimental features to the list either:
            // add EnvVarExperimentalFeature if the feature is controlled by an enviroment variable
            // OR
            // add ExperimentalFeature with on, off and check delegates if the feature requires more specific operations
        }
    }
}