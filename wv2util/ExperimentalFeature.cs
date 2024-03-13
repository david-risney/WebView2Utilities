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

        public ExperimentalFeature(Action turnOn, Action turnOff)
        {
            this.turnOn = turnOn;
            this.turnOff = turnOff;
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

    public class ExperimentalFeatureList : ObservableCollection<ExperimentalFeature>
    {
        private List<ExperimentalFeature> m_List = new List<ExperimentalFeature>();

        public ExperimentalFeatureList()
        {
            Items.Add(new ExperimentalFeature(
                () => { },
                () => { }) { Name = "Hosting", IsEnabled = true });
            Items.Add(new ExperimentalFeature(() => { }, () => { }) { Name = "Canary", IsEnabled = false });
        }

        public List<ExperimentalFeature> GetList()
        {
            return m_List;
        }

        public IDisposable Subscribe(IObserver<ExperimentalFeature> observer)
        {
            throw new NotImplementedException();
        }
    }
}