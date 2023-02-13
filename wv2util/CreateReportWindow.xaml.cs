using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                await m_ReportCreator.CreateReportAsync();
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
    }
}
