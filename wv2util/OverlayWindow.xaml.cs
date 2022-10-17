using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
using Timer = System.Timers.Timer;


namespace wv2util
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        // The HWND of the target window over which we will render our overlay
        private IntPtr m_targetHwnd;
        private IntPtr m_thisHwnd;
        // The timer that tracks when to check for target HWND updates
        private Timer m_checkForUpdatesTimer;
        private Rect m_previousRect;
        private bool m_closed = false;
        
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
            var synchronizationContext = SynchronizationContext.Current;
            m_checkForUpdatesTimer.Elapsed += (object o, ElapsedEventArgs e) =>
            { 
                synchronizationContext.Post((objectContext) => UpdateOverlayToMatchTarget(), null);
            };
            m_checkForUpdatesTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            m_thisHwnd = new WindowInteropHelper(this).Handle;

            // Change the extended window style to include WS_EX_TRANSPARENT
            int extendedStyle = PInvoke.User32.GetWindowLong(
                m_thisHwnd,
                PInvoke.User32.WindowLongIndexFlags.GWL_EXSTYLE);
            PInvoke.User32.SetWindowLong(
                m_thisHwnd,
                PInvoke.User32.WindowLongIndexFlags.GWL_EXSTYLE,
                (PInvoke.User32.SetWindowLongFlags)extendedStyle | PInvoke.User32.SetWindowLongFlags.WS_EX_TRANSPARENT);
        }

        private void UpdateOverlayToMatchTarget()
        {
            if (!m_closed)
            {
                var targetHwndRect = HwndUtil.GetWindowRect(m_targetHwnd);
                if (targetHwndRect != m_previousRect && !m_previousRect.IsEmpty)
                {
                    PInvoke.User32.MoveWindow(
                        m_thisHwnd,
                        (int)targetHwndRect.Left,
                        (int)targetHwndRect.Top,
                        (int)targetHwndRect.Width,
                        (int)targetHwndRect.Height,
                        true);
                }
                m_previousRect = targetHwndRect;

                bool isTargetVisible = PInvoke.User32.IsWindowVisible(m_targetHwnd);
                var targetWindowStyle = PInvoke.User32.GetWindowLong(m_targetHwnd, PInvoke.User32.WindowLongIndexFlags.GWL_STYLE);
                bool isTargetMinized = (targetWindowStyle & (int)PInvoke.User32.WindowStyles.WS_MINIMIZE) != 0;
                bool isTargetIconic = PInvoke.User32.IsIconic(m_targetHwnd);
                bool showWindow = isTargetVisible && !isTargetMinized && !isTargetIconic;

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

                IntPtr targetOwner = PInvoke.User32.GetAncestor(
                    m_targetHwnd, PInvoke.User32.GetAncestorFlags.GA_ROOTOWNER);
                IntPtr prevTargetHwnd = PInvoke.User32.GetNextWindow(
                    targetOwner, PInvoke.User32.GetNextWindowCommands.GW_HWNDPREV);
                if (prevTargetHwnd != null && prevTargetHwnd != m_thisHwnd)
                {
                    PInvoke.User32.SetWindowPos(m_thisHwnd, prevTargetHwnd, 0, 0, 0, 0,
                        PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE |
                        PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE);
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
            m_closed = true;
            this.Close();
        }
    }
}
