using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace wv2util
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        // The HWND of the target window over which we will render our overlay
        private IntPtr m_targetHwnd;
        // The timer that tracks when to check for target HWND updates
        private Timer m_checkForUpdatesTimer;
        private Rect m_previousRect;
        
        public static OverlayWindow OpenOverlayForHwnd(IntPtr targetHwnd)
        {
            var overlayWindow = new OverlayWindow(targetHwnd);
            overlayWindow.Show();
            return overlayWindow;
        }
        
        protected OverlayWindow(IntPtr targetHwnd)
        {
            m_targetHwnd = targetHwnd;

            InitializeComponent();
            UpdateOverlayToMatchTarget();

            m_checkForUpdatesTimer = new Timer(1000);
            m_checkForUpdatesTimer.Elapsed += (object o, ElapsedEventArgs e) => 
                UpdateOverlayToMatchTarget();
            m_checkForUpdatesTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Change the extended window style to include WS_EX_TRANSPARENT
            int extendedStyle = PInvoke.User32.GetWindowLong(
                hwnd,
                PInvoke.User32.WindowLongIndexFlags.GWL_EXSTYLE);
            PInvoke.User32.SetWindowLong(
                hwnd,
                PInvoke.User32.WindowLongIndexFlags.GWL_EXSTYLE,
                (PInvoke.User32.SetWindowLongFlags)extendedStyle | PInvoke.User32.SetWindowLongFlags.WS_EX_TRANSPARENT);
        }

        private void UpdateOverlayToMatchTarget()
        {
            var targetHwndRect = HwndUtil.GetWindowRect(m_targetHwnd);
            if (targetHwndRect != m_previousRect)
            {
                m_previousRect = targetHwndRect;

                this.Left = targetHwndRect.Left;
                this.Top = targetHwndRect.Top;
                this.Width = targetHwndRect.Width;
                this.Height = targetHwndRect.Height;

                Trace.WriteLine(" overlay target moved " + m_previousRect + " -> " + targetHwndRect + " (" + this.Left + " " + this.Top  + " " + this.Width + " " + this.Height);
            }

            bool isTargetVisible = PInvoke.User32.IsWindowVisible(m_targetHwnd);
            var targetWindowStyle = PInvoke.User32.GetWindowLong(m_targetHwnd, PInvoke.User32.WindowLongIndexFlags.GWL_STYLE);
            bool isTargetMinized = (targetWindowStyle & (int)PInvoke.User32.WindowStyles.WS_MINIMIZE) != 0;
            bool showWindow = isTargetVisible && !isTargetMinized;

            if (this.IsVisible != showWindow)
            {
                if (showWindow)
                {
                    this.Show();
                }
                else
                {
                    this.Hide();
                }
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Hide and shutdown the Window
            CloseOverlay();            
        }

        public void CloseOverlay()
        {
            // Hide and shutdown this window.
            this.Close();
        }
    }
}
