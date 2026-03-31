using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace GitLFSAutoTracker {

    public partial class MainWindow : Window {

        public const float DECLARED_LFS_FILE_SIZE = 99f;

        public event Action<int>? OnReceivedFilesCount = null;
        public event Action<string, long>? OnReadedFileSize = null;
        public event Action<float>? OnTrackEnded = null;

        public readonly Lazy<Stack<FileInfo>> bigFiles = new Lazy<Stack<FileInfo>>();

        public readonly SolidColorBrush defaultColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
        public readonly SolidColorBrush trackedColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 35, 13));


        public MainWindow() {
            InitializeComponent();

            Button_CreateAttributes.Visibility = Visibility.Collapsed;

            Task.Run(SearchLocalFiles).ConfigureAwait(false);

            Label_FilesCounter.Content = $"Started search files with [{DECLARED_LFS_FILE_SIZE} MB] size";

            OnReadedFileSize += (name, size) => {
                Dispatcher.Invoke(() => {

                    if ((size / 1024f / 1024f) > DECLARED_LFS_FILE_SIZE) {
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
                });
            };

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

                if (fileInfo.Length / 1024f / 1024f >= DECLARED_LFS_FILE_SIZE) {
                    sumSize += fileInfo.Length / 1024f / 1024f;
                    bigFiles.Value.Push(fileInfo);
                }
            }

            // on ended Event
            OnTrackEnded?.Invoke(sumSize);
        }
    }
}