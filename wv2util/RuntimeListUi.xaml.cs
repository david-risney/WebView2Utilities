using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for RuntimeList.xaml
    /// </summary>
    public partial class RuntimeListUi : Window
    {
        public RuntimeListUi()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            GetRuntimeInfosFromPath(Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Microsoft");
        }

        struct RuntimeInfo
        {
            public string Path;
            public string Version;
            public string Channel;
        }

        void PopulateRuntimeListViewWithRuntimeInfos(List<RuntimeInfo> runtimeInfos)
        {
            runtimeInfos.ForEach(runtimeInfo =>
            {
            });
        }

        List<RuntimeInfo> GetRuntimeInfosFromPath(string rootPath)
        {
            string[] files = Directory.GetFiles(rootPath, "msedgewebview2.exe", SearchOption.AllDirectories);
            return files.Select(filePath => GetRuntimeInfoFromPath(filePath)).ToList();
        }

        RuntimeInfo GetRuntimeInfoFromPath(string runtimePath)
        {
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(runtimePath);
            RuntimeInfo info = new RuntimeInfo();
            info.Version = fileVersionInfo.FileVersion;
            info.Path = Directory.GetParent(runtimePath).FullName;
            info.Channel = PathToChannel(runtimePath);

            return info;
        }

        string PathToChannel(string path)
        {
            if (path.Contains("SxS"))
            {
                return "Canary";
            }
            else if (path.Contains("Beta"))
            {
                return "Beta";
            }
            else if (path.Contains("Dev"))
            {
                return "Dev";
            }
            return "";
        }
    }
}
