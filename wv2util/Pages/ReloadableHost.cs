using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace wv2util.Pages
{
    public class ReloadableHost
    {
        private Button m_button;
        private IReloadable m_reloadable;

        public ReloadableHost(Button button, IReloadable reloadable)
        {
            m_button = button;
            m_reloadable = reloadable;

            m_reloadable.ReloadingChanged += OnReloadingChanged;
            m_button.Click += OnButtonClick;
        }

        private void OnButtonClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!m_reloadable.Reloading)
            {
                m_reloadable.Reload();
            }
        }

        private void OnReloadingChanged(object sender, EventArgs e)
        {
            // Get back to UI thread
            m_button.Dispatcher.Invoke(() =>
            {
                if (m_reloadable.Reloading)
                {
                    m_button.Content = "⌚";
                    m_button.IsEnabled = false;
                }
                else
                {
                    m_button.Content = "🔃";
                    m_button.IsEnabled = true;
                }
            });
        }
    }
}
