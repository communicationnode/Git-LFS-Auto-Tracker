using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace GitLFSAutoTracker {

    public partial class MainWindow : Window {

        public float lfs_file_size = 25f;

        public event Action<int>? OnReceivedFilesCount = null;
        public event Action<string, long>? OnReadedFileSize = null;
        public event Action<float>? OnTrackEnded = null;

        public readonly Lazy<Stack<FileInfo>> bigFiles = new Lazy<Stack<FileInfo>>();

        public readonly SolidColorBrush defaultColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
        public readonly SolidColorBrush trackedColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 35, 13));


        public MainWindow() {
            InitializeComponent();
            ExecuteSearch();
            InitMainEvents();
            InitButtonEvents();
        }

        public void ExecuteSearch() {
            bigFiles.Value.Clear();
            Button_CreateAttributes.Visibility = Visibility.Collapsed;
            StackPanel_SizeButtons.Visibility = Visibility.Collapsed;
            ListBox_Files.Items.Clear();
            Label_FilesCounter.Content = $"Started search files with [{lfs_file_size} MB] size";
            Task.Run(SearchLocalFiles).ConfigureAwait(false);
        }
        public async Task SearchLocalFiles() {

            // search all files
            string workingDirectory = Environment.CurrentDirectory;
            ReadOnlyMemory<string> findedPaths = Directory.GetFiles(workingDirectory, "*", searchOption: SearchOption.AllDirectories).AsMemory();

            // tracked all files Event
            OnReceivedFilesCount?.Invoke(findedPaths.Length);

            // detect big files Event
            float sumSize = 0f;
            for (int i = 0; i < findedPaths.Length; i++) {
                FileInfo fileInfo = new FileInfo(findedPaths.Span[i]);
                OnReadedFileSize?.Invoke(fileInfo.Name, fileInfo.Length);

                string firstFolder = fileInfo.FullName.Replace(Environment.CurrentDirectory, "").Remove(0, 1).Split('\\')[0];
                if (firstFolder == ".git") continue;

                if (fileInfo.Length / 1024f / 1024f >= lfs_file_size) {
                    sumSize += fileInfo.Length / 1024f / 1024f;
                    bigFiles.Value.Push(fileInfo);
                }
            }

            // on ended Event
            OnTrackEnded?.Invoke(sumSize);

            await Task.Yield();
        }
        public int InitMainEvents() {
            OnReadedFileSize += (name, size) => {
                Dispatcher.Invoke(() => {

                    if ((size / 1024f / 1024f) > lfs_file_size) {
                        ListBox_Files.Items.Insert(0, new Label() {
                            Content = $"[{(Math.Round(size / 1024f / 1024f, 5))} MB]: {name}",
                            Foreground = trackedColor,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    else {
                        ListBox_Files.Items.Add(new Label() {
                            Content = $"[{(Math.Round(size / 1024f / 1024f, 5))} MB]: {name}",
                            Foreground = defaultColor,
                            FontWeight = FontWeights.Bold
                        });
                    }
                });
            };

            OnTrackEnded += (fullSize) => {
                Dispatcher.Invoke(() => {
                    Button_CreateAttributes.Visibility = Visibility.Visible;
                    Label_FilesCounter.Content = $"Finded [{bigFiles.Value.Count}]\nfiles [{fullSize} MB] at all";
                    StackPanel_SizeButtons.Visibility = Visibility.Visible;
                });
            };

            return 0;
        }
        public int InitButtonEvents() {
            Button_CreateAttributes.Click += (sender, e) => {
                StringBuilder sBuilder = new StringBuilder();
                // filter=lfs diff=lfs merge=lfs -text
                while (bigFiles.Value.Count > 0) {
                    sBuilder.AppendLine($"{bigFiles.Value.Pop().FullName.Replace(Environment.CurrentDirectory, "").Remove(0, 1)} filter=lfs diff=lfs merge=lfs -text");
                }

                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, ".gitattributes"), sBuilder.ToString());

                Button_CreateAttributes.Visibility = Visibility.Collapsed;
                ListBox_Files.Items.Clear();
                Label_FilesCounter.Content = "Created .gitattributes";

                Thread.Sleep(1000);
                Process.GetCurrentProcess().Kill();
            };

            Button_Search25mb.Click += (sender, e) => { lfs_file_size = 25f; ExecuteSearch(); };
            Button_Search49mb.Click += (sender, e) => { lfs_file_size = 49f; ExecuteSearch(); };
            Button_Search99mb.Click += (sender, e) => { lfs_file_size = 99f; ExecuteSearch(); };

            return 0;
        }
    }
}