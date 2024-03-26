using Markdig.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
            VersionInfo.Text = "v" + VersionUtil.GetWebView2UtilitiesVersion();
            GenerateNewsBlocksAsync();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri.IsFile)
            {
                ProcessUtil.OpenExplorerToFile(e.Uri.LocalPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            }
            e.Handled = true;
        }

        private void Hyperlink_ExecutedRouted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start(e.Parameter.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static string s_newsLink = "https://api.github.com/repos/MicrosoftEdge/WebView2Announcements/issues";
        private async Task<string> GetNewsDataAsync()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var response = await client.GetAsync(s_newsLink);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
            else
                return await Task.FromResult($"{{ \"error\": \"{response.StatusCode}\" }}");
        }

        private Dictionary<TextBlock, UIElement> m_newsBodyCollapsedControllerMap = new Dictionary<TextBlock, UIElement>();
        private async void GenerateNewsBlocksAsync()
        {
            string newsData = await GetNewsDataAsync();
            var newsJson = JsonSerializer.Deserialize<JsonElement>(newsData);
            int newsDisplayed = 3;
            int bodyFontSize = 12;

            if (newsJson.ValueKind != JsonValueKind.Null &&
                newsJson.ValueKind == JsonValueKind.Array &&
                newsJson.EnumerateArray().Any())
            {
                for (int i = 0; i < newsDisplayed && i < newsJson.GetArrayLength(); i++)
                {
                    // Skip a newsBlock when the data is invalid.
                    if (newsJson[i].ValueKind != JsonValueKind.Object ||
                        !newsJson[i].TryGetProperty("title", out var title) ||
                        !newsJson[i].TryGetProperty("body", out var body))
                    {
                        continue;
                    }

                    // Add the title to the block.
                    TextBlock newsBlock = new TextBlock
                    {
                        Text = title.GetString(),
                        FontSize = bodyFontSize + 2,
                        Margin = new Thickness(10, 10, 0, 0),
                        Cursor = Cursors.Hand
                    };

                    // Add body content to the block.
                    MarkdownViewer bodyBlock = new MarkdownViewer
                    {
                        Markdown = body.GetString(),
                        FontSize = bodyFontSize,
                    };
                    foreach (Block block in bodyBlock.Document.Blocks)
                        if (block.FontSize > bodyFontSize)
                            block.FontSize = bodyFontSize;
                    bodyBlock.CommandBindings.Add(new CommandBinding(Commands.Hyperlink, Hyperlink_ExecutedRouted));
                    Grid bodyGrid = new Grid
                    {
                        Children = { bodyBlock },
                        Width = 500,
                    };

                    // Enable click to collapse function.
                    newsBlock.MouseDown += NewsBlock_Click;
                    m_newsBodyCollapsedControllerMap[newsBlock] = bodyGrid;
                    bodyGrid.Visibility = Visibility.Collapsed;

                    newsBlock.Inlines.Add(new LineBreak());
                    newsBlock.Inlines.Add(bodyGrid);
                    NewsPanel.Children.Add(newsBlock);
                }
            }
        }

        private void NewsBlock_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock clickedTextBlock && m_newsBodyCollapsedControllerMap.ContainsKey(clickedTextBlock))
            {
                UIElement targetContainer = m_newsBodyCollapsedControllerMap[clickedTextBlock];
                targetContainer.Visibility = targetContainer.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

    }
}
