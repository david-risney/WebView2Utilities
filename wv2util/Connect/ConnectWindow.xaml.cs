using System;
using System.Collections.Generic;
using System.Linq;
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

namespace wv2util.Connect
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectWindow : Window
    {
        public ConnectWindow(HostAppEntry browserProcess)
        {
            Environment.SetEnvironmentVariable(
                "COREWEBVIEW2_FORCED_HOSTING_MODE",
                "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL");

            InitializeComponent();

            _ = InitializeAsync(browserProcess);
        }

        public void ShowLoading(bool showLoading)
        {
            this.ProgressBar.Visibility = showLoading? Visibility.Visible : Visibility.Hidden;
            this.WebViewGrid.Visibility = showLoading ? Visibility.Hidden : Visibility.Visible;
        }

        public async Task InitializeAsync(HostAppEntry browserProcess)
        {
            try
            {
                ShowLoading(true);

                var env = await ConnectUtil.CreateEnvironmentAsync(browserProcess);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.SourceChanged += WebView_SourceChanged;
                WebView.Source = new Uri(UriTextBox.Text);

                ShowLoading(false);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to connect.\n" + e.Message);
                this.Close();
            }
        }

        private void WebView_SourceChanged(object sender, Microsoft.Web.WebView2.Core.CoreWebView2SourceChangedEventArgs e)
        {
            UriTextBox.Text = WebView.Source.ToString();
        }

        private void UriTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = UriTextBox.Text;
                if (text.Contains(" " ))
                {
                    text = "https://bing.com/?q=" + Uri.EscapeDataString(text);
                }
                else if (!text.Contains(":"))
                {
                    text = "https://" + text;
                }
                this.WebView.Source = new Uri(text);
            }
        }
    }
}
