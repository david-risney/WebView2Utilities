using PInvoke;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Interop;
using System.Diagnostics;

namespace wv2util
{
    public interface ITreeItem : INotifyPropertyChanged
    {
        string Name { get; }

        BitmapSource IconAsBitmapSource { get; }

        ObservableCollection<ITreeItem> Children { get; }

        Object Model { get; }

        bool IsSelected { get; set; }
        bool IsExpanded { get; set; }
    }

    public class TreeItemBase : ITreeItem
    {
        public virtual string Name { get { throw new NotImplementedException(); } }

        public virtual BitmapSource IconAsBitmapSource => null;

        public virtual ObservableCollection<ITreeItem> Children { get; } = new ObservableCollection<ITreeItem>();

        public virtual object Model { get; } = null;

        public virtual event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool m_IsSelected = false;
        public virtual bool IsSelected
        {
            get => m_IsSelected;
            set
            {

                if (m_IsSelected != value)
                {
                    m_IsSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private bool m_IsExpanded = false;
        public virtual bool IsExpanded
        {
            get => m_IsExpanded;
            set
            {
                if (m_IsExpanded != value)
                {
                    m_IsExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }
    }

    public class HostAppEntryTreeItem : TreeItemBase
    {
        private HostAppList m_hostAppList;
        private HostAppEntry m_hostAppEntry;
        public HostAppEntryTreeItem(HostAppList hostAppList, HostAppEntry hostAppEntry)
        {
            m_hostAppList = hostAppList;
            m_hostAppEntry = hostAppEntry;

            m_hostAppList.CollectionChanged += HostAppListCollectionChanged;
        }

        private void HostAppListCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("Name");
            OnPropertyChanged("IconAsBitmapSource");
            OnPropertyChanged("Children");
        }

        public override string Name => m_hostAppEntry.ExecutableName + " " + m_hostAppEntry.PIDAndStatus;

        public override BitmapSource IconAsBitmapSource => GetExecutableIcon(m_hostAppEntry);

        private static BitmapSource GetExecutableIcon(HostAppEntry hostAppEntry)
        {
            BitmapSource result = null;

            try
            {
                result = GetExecutableIcon(hostAppEntry.ExecutablePath);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            if (result == null)
            {
                try
                {
                    // Load a generic system icon (e.g., application icon)
                    Icon systemIcon = SystemIcons.Application;
                    Bitmap systemBitmap = systemIcon.ToBitmap();
                    MemoryStream systemStream = new MemoryStream();
                    systemBitmap.Save(systemStream, ImageFormat.Png);
                    systemStream.Seek(0, SeekOrigin.Begin);

                    BitmapImage systemBitmapImage = new BitmapImage();
                    systemBitmapImage.BeginInit();
                    systemBitmapImage.StreamSource = systemStream;
                    systemBitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    systemBitmapImage.EndInit();

                    result = systemBitmapImage;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            
            return result;
        }

        private static BitmapSource GetExecutableIcon(string executablePath)
        {
            // Get the icon associated with the specified executable
            Icon appIcon = Icon.ExtractAssociatedIcon(executablePath);

            // Convert the Icon to a BitmapSource
            Bitmap bitmap = appIcon.ToBitmap();
            MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);

            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            return bitmapImage;
        }

        public override ObservableCollection<ITreeItem> Children
        {
            get
            {
                return new ObservableCollection<ITreeItem>(
                    m_hostAppEntry.Children.Select(processEntry => new ProcessEntryTreeItem(m_hostAppList, processEntry)));
            }
        }

        public override Object Model => m_hostAppEntry;

    }

    public class ProcessEntryTreeItem : TreeItemBase
    {
        private HostAppList m_hostAppList;
        private ProcessEntry m_processEntry;
        public ProcessEntryTreeItem(HostAppList hostAppList, ProcessEntry processEntry)
        {
            m_hostAppList = hostAppList;
            m_processEntry = processEntry;

            m_hostAppList.CollectionChanged += HostAppListCollectionChanged;
        }

        private void HostAppListCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("Name");
            OnPropertyChanged("IconAsBitmapSource");
            OnPropertyChanged("Children");
        }

        public override string Name => m_processEntry.ExecutableName + " " + m_processEntry.EdgeProcessKind + " " + m_processEntry.PID;

        public override BitmapSource IconAsBitmapSource => GetExecutableIcon(m_processEntry.ExecutablePath);

        private static BitmapSource GetExecutableIcon(HostAppEntry hostAppEntry)
        {
            BitmapSource result = null;

            try
            {
                result = GetExecutableIcon(hostAppEntry.ExecutablePath);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            if (result == null)
            {
                try
                {
                    // Load a generic system icon (e.g., application icon)
                    Icon systemIcon = SystemIcons.Application;
                    Bitmap systemBitmap = systemIcon.ToBitmap();
                    MemoryStream systemStream = new MemoryStream();
                    systemBitmap.Save(systemStream, ImageFormat.Png);
                    systemStream.Seek(0, SeekOrigin.Begin);

                    BitmapImage systemBitmapImage = new BitmapImage();
                    systemBitmapImage.BeginInit();
                    systemBitmapImage.StreamSource = systemStream;
                    systemBitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    systemBitmapImage.EndInit();

                    result = systemBitmapImage;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }

            return result;
        }

        private static BitmapSource GetExecutableIcon(string executablePath)
        {
            // Get the icon associated with the specified executable
            Icon appIcon = Icon.ExtractAssociatedIcon(executablePath);

            // Convert the Icon to a BitmapSource
            Bitmap bitmap = appIcon.ToBitmap();
            MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);

            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            return bitmapImage;
        }

        public override ObservableCollection<ITreeItem> Children
        {
            get
            {
                return new ObservableCollection<ITreeItem>(
                    m_processEntry.Children.Select(processEntry => new ProcessEntryTreeItem(m_hostAppList, processEntry)));
            }
        }

        public override Object Model => m_processEntry;
    }
}