using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for CreateReportWindow.xaml
    /// </summary>
    public partial class CreateReportWindow : Window
    {
        public CreateReportWindow(Window parent, ReportCreator reportCreator)
        {
            base.Owner = parent;
            m_ReportCreator = reportCreator;
            InitializeComponent();

            InitializeDestinationPathTextBox();
            InitializeFilesListBox();

            this.Closing += CreateReportWindow_Closing;
        }

        private CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        private void CreateReportWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_CancellationTokenSource.Cancel();
            m_ReportCreator.Cleanup();
        }

        private ReportCreator m_ReportCreator;
        public ObservableCollection<ReportCreator.FileEntry> ReportFilesList => m_ReportCreator.ReportFilesList;
        public String DestinationPath => m_ReportCreator.DestinationPath;

        private void DestinationPathChangeButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the filename part of the DestinationPath
            string destinationPathFileName = System.IO.Path.GetFileName(m_ReportCreator.DestinationPath);

            // Prompt the user to pick a path to save the report zip
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
                FileName = destinationPathFileName,
                RestoreDirectory = true
            };
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                m_ReportCreator.DestinationPath = saveFileDialog.FileName;
                InitializeDestinationPathTextBox();
            }
        }
        
        private void InitializeDestinationPathTextBox()
        {
            this.DestinationPathTextBox.Text = m_ReportCreator.DestinationPath;
        }

        private void InitializeFilesListBox()
        {
            this.FilesListBox.ItemsSource = ReportFilesList;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void CreateReportButton_Click(object sender, RoutedEventArgs e)
        {
            this.CreateReportButton.IsEnabled = false;
            this.CreateReportButton.Content = "Creating Report...";

            try
            {
                await m_ReportCreator.CreateReportAsync(m_CancellationTokenSource.Token);
                if ((bool)OpenReportInExplorerCheckBox.IsChecked)
                {
                    ProcessUtil.OpenExplorerToFile(m_ReportCreator.DestinationPath);
                }
                this.Close();
                
                MessageBox.Show("The report was created.", "Report Created", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception error)
            {
                this.CreateReportButton.IsEnabled = true;
                this.CreateReportButton.Content = "Create Report";

                MessageBox.Show(error.ToString(), "Failed to create report", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddDxDiagLogButton_Click(object sender, RoutedEventArgs e)
        {
            AddDxDiagLogButton.IsEnabled = false;
            AddDxDiagLogButton.Content = "Adding DxDiag Log...";

            try
            {
                await m_ReportCreator.AddDxDiagLogAsync(m_CancellationTokenSource.Token);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Failed to add DxDiag log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddDxDiagLogButton.IsEnabled = true;
                AddDxDiagLogButton.Content = "Add DxDiag Log";
            }
        }

        private TaskCompletionSource<bool> m_ProcMonLogScenarioTaskCompletionSource;
        private async void AddProcMonLogButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcMonLogButton.IsEnabled = false;
            AddProcMonLogButton.Content = "Creating ProcMon Log...";
            StopProcMonLogButton.IsEnabled = true;

            try
            {
                m_ProcMonLogScenarioTaskCompletionSource = new TaskCompletionSource<bool>();
                await m_ReportCreator.AddScenarioLogAsync(
                    ReportCreator.LogKind.ProcMon, 
                    m_ProcMonLogScenarioTaskCompletionSource.Task, 
                    m_CancellationTokenSource.Token);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Failed to add ProcMon log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddProcMonLogButton.IsEnabled = true;
                AddProcMonLogButton.Content = "Add ProcMon Log";
                StopProcMonLogButton.IsEnabled = false;
                StopProcMonLogButton.Content = "Complete ProcMon Log";
            }
        }

        private void StopProcMonLogButton_Click(object sender, RoutedEventArgs e)
        {
            StopProcMonLogButton.IsEnabled = false;
            StopProcMonLogButton.Content = "Completing ProcMon Log...";
            m_ProcMonLogScenarioTaskCompletionSource?.SetResult(true);
            m_ProcMonLogScenarioTaskCompletionSource = null;            
        }
    }
}
