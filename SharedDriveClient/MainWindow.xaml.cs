using Microsoft.Win32;
using SharedDriveLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static SharedDriveLibrary.ValueDefinitions;

namespace SharedDriveClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        // Const values
        private const string ServerIPAddress = ValueDefinitions.ServerIPAddress;
        private const int ServerPortNumber = ValueDefinitions.ServerPortNumber;
        private const int BlockSize = ValueDefinitions.BlockSize;
        private const string DefaultFolder = @"C:\SharedDrive";
        private const string DownloadsDirectoryInfoFile = DefaultFolder + "\\Downloads directory info.txt";
        private const string DefaultDownloadsDirectory = DefaultFolder + "\\Downloads";
        // Private data members
        private static string _uploadFilename = "";
        private static string _shortUploadFilename = "";
        private FileEntry _selected_file;
        private string _downloadDirectory;
        private ObservableCollection<FileEntry> _files_collection;
        private ObservableCollection<ProgressModel> _download_progress_list;
        private ObservableCollection<ProgressModel> _upload_progress_list;
        private bool _openDirectoryAfterDownload;
        private bool _isFileSelected;
        private bool _isFileUploading;
        private bool _isFileDownloading;

        // Properties
        public string UploadFilename
        {
            get
            {
                return _uploadFilename;
            }
            set
            {
                _uploadFilename = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UploadFilename"));
            }
        }

        public FileEntry SelectedFile
        {
            get
            {
                return _selected_file;
            }
            set
            {
                _selected_file = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedFile"));
                IsFileSelected = true;
            }
        }

        public string DownloadDirectory
        {
            get
            {
                return _downloadDirectory;
            }
            set
            {
                _downloadDirectory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DownloadDirectory"));
            }
        }

        public ObservableCollection<FileEntry> FilesCollection
        {
            get
            {
                return _files_collection;
            }
            set
            {
                _files_collection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilesCollection"));
            }
        }

        public ObservableCollection<ProgressModel> DownloadProgressList
        {
            get
            {
                return _download_progress_list;
            }
            set
            {
                _download_progress_list = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DownloadProgressList"));
            }
        }

        public ObservableCollection<ProgressModel> UploadProgressList
        {
            get
            {
                return _upload_progress_list;
            }
            set
            {
                _upload_progress_list = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UploadProgressList"));
            }
        }

        public bool OpenDirectoryAfterDownload
        {
            get
            {
                return _openDirectoryAfterDownload;
            }
            set
            {
                _openDirectoryAfterDownload = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("OpenDirectoryAfterDownload"));
            }
        }

        public bool IsFileSelected
        {
            get
            {
                return _isFileSelected;
            }
            set
            {
                _isFileSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFileSelected"));
            }
        }

        public bool IsFileUploading
        {
            get
            {
                return _isFileUploading;
            }
            set
            {
                _isFileUploading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFileUploading"));
            }
        }

        public bool IsFileDownloading
        {
            get
            {
                return _isFileDownloading;
            }
            set
            {
                _isFileDownloading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFileDownloading"));
            }
        }

        // Events
        public event PropertyChangedEventHandler PropertyChanged;

        // Constructor
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                DownloadDirectory = GetDefaultDownloadDirectory();
                FilesCollection = new ObservableCollection<FileEntry>();
                DownloadProgressList = new ObservableCollection<ProgressModel>();
                UploadProgressList = new ObservableCollection<ProgressModel>();
                OpenDirectoryAfterDownload = true;
                UploadProgressListView.Visibility = Visibility.Hidden;
                DownloadProgressListView.Visibility = Visibility.Hidden;
                Task.Factory.StartNew(() => UpdateFileList());
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }



        // Button event handlers

        // Upload button event handler
        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                if (FilesCollection.Where(f => f.Name == _shortUploadFilename).Count() > 0)
                {
                    var result = MessageBox.Show($"A file with the name '{_shortUploadFilename}' already exists. Would you like to replace it?",
                        "Replace file", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                    if (result == MessageBoxResult.OK)
                    {
                        DeleteFile(_shortUploadFilename);
                        InitProgress(out ProgressModel progModel, out Progress<ProgressModel> progress, _shortUploadFilename,
                            ActionType.Upload);
                        MakeListViewVisibleOnUI(UploadProgressListView);
                        UploadFile(UploadFilename, _shortUploadFilename, progress);
                        lock (this)
                        {
                            RemoveOnUI(UploadProgressList, progModel);
                        }
                        if (UploadProgressList.Count < 1)
                            MakeListViewHiddenOnUI(UploadProgressListView);
                    }
                }
                else
                {
                    InitProgress(out ProgressModel progModel, out Progress<ProgressModel> progress, _shortUploadFilename,
                        ActionType.Upload);
                    UploadFile(UploadFilename, _shortUploadFilename, progress);
                    lock (this)
                    {
                        RemoveOnUI(UploadProgressList, progModel);
                    }
                }
            });
            UploadFileTextBox.Clear();
        }

        // Browse button (Upload Panel) event handler
        private void UploadBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Title = "File Upload"
            };
            dialog.ShowDialog();
            UploadFilename = dialog.FileName;
            _shortUploadFilename = dialog.SafeFileName;
        }

        // Download button event handler
        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                var filename = SelectedFile.Name;
                filename = SelectedFile.Name;
                InitProgress(out ProgressModel progModel, out Progress<ProgressModel> progress,
                    filename, ActionType.Download);
                MakeListViewVisibleOnUI(DownloadProgressListView);
                DownloadFile(filename, progress);
                lock (this)
                {
                    RemoveOnUI(DownloadProgressList, progModel);
                    if (DownloadProgressList.Count < 1)
                        MakeListViewHiddenOnUI(DownloadProgressListView);
                }
                if (OpenDirectoryAfterDownload)
                    System.Diagnostics.Process.Start("explorer", DownloadDirectory);
            });
        }

        // Refresh button event handler
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => RefreshList());
        }

        // Delete button event handler
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFile != null)
                DeleteFile(SelectedFile.Name);
        }

        // Browse button (Download Panel) event handler
        private void BrowseDownloadDirectoriesButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    SetDownloadsDirectory(dialog.SelectedPath);
                    DownloadDirectory = dialog.SelectedPath + '\\';
                }
            }
        }

        // Other methods

        // Initiate progress bar for file upload/download
        private void InitProgress(out ProgressModel progModel, out Progress<ProgressModel> progress, string filename, ActionType actionType)
        {
            lock (this)
            {
                progModel = new ProgressModel() { PctCompleted = 0, Filename = filename };
                progress = new Progress<ProgressModel>();
                if (actionType == ActionType.Upload)
                {
                    AddOnUI(UploadProgressList, progModel);
                    progress.ProgressChanged += ReportUploadProgress;
                }
                else if (actionType == ActionType.Download)
                {
                    AddOnUI(DownloadProgressList, progModel);
                    progress.ProgressChanged += ReportDownloadProgress;
                }
            }
        }

        // Update the list of files displayed on the Download Panel
        private void UpdateFileList()
        {
            try
            {
                var clientSocket = new TcpClient(ServerIPAddress, ServerPortNumber);
                var networkStream = clientSocket.GetStream();
                var data = BitConverter.GetBytes((int)RequestType.GetList);
                networkStream.Write(data, 0, data.Length);
                var response = new byte[sizeof(int)];
                networkStream.Read(response, 0, response.Length);
                var data_length = BitConverter.ToInt32(response, 0);
                data = new byte[data_length];
                networkStream.Read(data, 0, data.Length);
                var entries = (List<FileEntry>)SerializationMethods.Deserialize(data);
                foreach (var entry in entries)
                {
                    AddOnUI(FilesCollection, entry);
                }
            }
            catch (SocketException)
            {
                MessageBox.Show("Could not connect to the server, please try again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Upload a file to the shared drive
        public void UploadFile(string fullPath, string filename, IProgress<ProgressModel> progress)
        {
            try
            {
                var report = new ProgressModel() { PctCompleted = 0, Filename = filename };
                var fileSize = (int)new FileInfo(fullPath).Length;
                int numBlocks = fileSize / BlockSize;
                int lastBlockSize = fileSize % BlockSize;
                // Header to be sent to the server
                var fileHeader = new ClientUploadFileHeader()
                {
                    Filename = filename,
                    FileSize = fileSize,
                    NumBlocks = numBlocks,
                    LastBlockSize = lastBlockSize
                };
                // Connect to server
                var clientSocket = new TcpClient(ServerIPAddress, ServerPortNumber);
                var networkStream = clientSocket.GetStream();
                // Convert data to byte arrays, then write to network stream
                var uploadRequestBytes = BitConverter.GetBytes((int)RequestType.UploadFile);
                var headerBytes = SerializationMethods.Serialize(fileHeader);
                var headerLenBytes = BitConverter.GetBytes(headerBytes.Length);
                networkStream.Write(uploadRequestBytes, 0, uploadRequestBytes.Length);
                networkStream.Write(headerLenBytes, 0, headerLenBytes.Length);
                networkStream.Write(headerBytes, 0, headerBytes.Length);
                var fstream = File.OpenRead(fullPath);
                for (int i = 0; i < numBlocks; i++)
                {
                    var file_data = new byte[BlockSize];
                    fstream.Seek(i * BlockSize, SeekOrigin.Begin);
                    fstream.Read(file_data, 0, file_data.Length);
                    networkStream.Write(file_data, 0, file_data.Length);
                    report.PctCompleted = (i + 1) * 100 / numBlocks;
                    progress.Report(report);
                }
                if (lastBlockSize > 0)
                {
                    var file_data = new byte[lastBlockSize];
                    fstream.Seek(numBlocks * BlockSize, SeekOrigin.Begin);
                    fstream.Read(file_data, 0, file_data.Length);
                    networkStream.Write(file_data, 0, file_data.Length);
                }
                fstream.Dispose();
                // Check if upload was successful
                var responeBuffer = new byte[sizeof(int)];
                var read = networkStream.Read(responeBuffer, 0, responeBuffer.Length);
                var responseType = BitConverter.ToInt32(responeBuffer, 0);
                if (responseType == (int)ResponeType.Failure)
                    throw new Exception("Error uploading file, try again");
                networkStream.Close();
                RefreshList();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Download a file from the shared drive
        private void DownloadFile(string fileToDownload, IProgress<ProgressModel> progress)
        {
            NetworkStream networkStream = null;
            FileStream fileStream = null;
            try
            {
                var report = new ProgressModel() { PctCompleted = 0, Filename = fileToDownload };
                var socket = new TcpClient(ServerIPAddress, ServerPortNumber);
                networkStream = socket.GetStream();
                // Send request
                var request_bytes = BitConverter.GetBytes((int)RequestType.DownloadFile);
                networkStream.Write(request_bytes, 0, request_bytes.Length);
                // Create and send request header
                var clientHeader = new ClientDownloadFileHeader() { Filename = fileToDownload };
                var clientHeaderBytes = SerializationMethods.Serialize(clientHeader);
                var clientHeaderLengthBytes = BitConverter.GetBytes(clientHeaderBytes.Length);
                networkStream.Write(clientHeaderLengthBytes, 0, clientHeaderLengthBytes.Length);
                networkStream.Write(clientHeaderBytes, 0, clientHeaderBytes.Length);
                // Check server response
                var responseBytes = new byte[sizeof(int)];
                networkStream.Read(responseBytes, 0, responseBytes.Length);
                var response = BitConverter.ToInt32(responseBytes, 0);
                if (response == (int)ResponeType.Failure)
                {
                    throw new FileDownloadException();
                }
                // Receive server header
                var serverHeaderSizeBytes = new byte[sizeof(int)];
                networkStream.Read(serverHeaderSizeBytes, 0, serverHeaderSizeBytes.Length);
                var serverHeaderBytes = new byte[BitConverter.ToInt32(serverHeaderSizeBytes, 0)];
                networkStream.Read(serverHeaderBytes, 0, serverHeaderBytes.Length);
                var serverHeader = (ServerDownloadFileHeader)SerializationMethods.Deserialize(serverHeaderBytes);
                var receivedBlockBuffer = new byte[BlockSize];
                fileStream = File.OpenWrite(DownloadDirectory + fileToDownload);
                int totalBytesRead = 0;
                var fileSize = serverHeader.FileSize;
                while (totalBytesRead < fileSize)
                {
                    var buffer = new byte[BlockSize];
                    var bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    totalBytesRead += bytesRead;
                    fileStream.Write(buffer, 0, bytesRead);
                    var bytesReadRatio = (double)totalBytesRead / serverHeader.FileSize;
                    report.PctCompleted = (int)(bytesReadRatio * 100);
                    progress.Report(report);
                }
            }
            catch (FileDownloadException)
            {
                MessageBox.Show("The requested file cannot be downloaded at this time");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                networkStream.Close();
            }
        }

        // Delete a file from the shared drive
        private void DeleteFile(string filename)
        {
            var socket = new TcpClient(ServerIPAddress, ServerPortNumber);
            var networkStream = socket.GetStream();
            var request_bytes = BitConverter.GetBytes((int)RequestType.DeleteFile);
            var filename_length_bytes = BitConverter.GetBytes(filename.Length);
            var filename_bytes = Encoding.ASCII.GetBytes(filename);
            networkStream.Write(request_bytes, 0, request_bytes.Length);
            networkStream.Write(filename_length_bytes, 0, filename_length_bytes.Length);
            networkStream.Write(filename_bytes, 0, filename_bytes.Length);
            var respone_buffer = new byte[sizeof(int)];
            networkStream.Read(respone_buffer, 0, respone_buffer.Length);
            var response = BitConverter.ToInt32(respone_buffer, 0);
            if (response == (int)ResponeType.Success)
            {
                RefreshList();
            }
            else
            {
                MessageBox.Show("Unable to delete file", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            networkStream.Close();
        }

        private void RefreshList()
        {
            ClearOnUI(FilesCollection);
            UpdateFileList();
        }

        private void ReportDownloadProgress(object sender, ProgressModel e)
        {
            if (DownloadProgressList.Count > 0 && (DownloadProgressList.Where(p => p.Filename.Equals(e.Filename)).ToList().Count > 0))
            {
                DownloadProgressList.Where(p => p.Filename.Equals(e.Filename)).First().PctCompleted = e.PctCompleted;
            }
        }

        private void ReportUploadProgress(object sender, ProgressModel e)
        {
            if (UploadProgressList.Count > 0 && (UploadProgressList.Where(p => p.Filename.Equals(e.Filename)).ToList().Count > 0))
            {
                UploadProgressList.Where(p => p.Filename.Equals(e.Filename)).First().PctCompleted = e.PctCompleted;
            }
        }

        // Get the default path for downloads
        private string GetDefaultDownloadDirectory()
        {
            FileStream fStream = null;
            byte[] data = null;
            try
            {
                fStream = File.OpenRead(DownloadsDirectoryInfoFile);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(DefaultFolder);
                SetDownloadsDirectory(DefaultDownloadsDirectory);
                fStream = File.OpenRead(DownloadsDirectoryInfoFile);
            }
            finally
            {
                data = new byte[fStream.Length];
                fStream.Read(data, 0, data.Length);
                fStream.Dispose();
            }
            return Encoding.ASCII.GetString(data) + '\\';
        }

        // Set new path for downloads
        private void SetDownloadsDirectory(string path)
        {
            File.WriteAllText(DownloadsDirectoryInfoFile, string.Empty);
            var fStream = File.OpenWrite(DownloadsDirectoryInfoFile);
            var data = Encoding.ASCII.GetBytes(path);
            fStream.Write(data, 0, data.Length);
            fStream.Dispose();
        }

        // UI update methods
        public static void AddOnUI<T>(ObservableCollection<T> collection, T item)
        {
            Action<T> addMethod = collection.Add;
            Application.Current.Dispatcher.BeginInvoke(addMethod, item);
        }

        public static void ClearOnUI<T>(ObservableCollection<T> collection)
        {
            Action clearMethod = collection.Clear;
            Application.Current.Dispatcher.BeginInvoke(clearMethod);
        }

        public static void RemoveOnUI<T>(ObservableCollection<T> collection, T item)
        {
            Action<ObservableCollection<T>, T> removeMethod = RemoveFromOC;
            Application.Current.Dispatcher.Invoke(removeMethod, collection, item);
        }

        public static void RemoveFromOC<T>(ObservableCollection<T> collection, T item)
        {
            collection.Remove(item);
        }

        public static void MakeListViewVisibleOnUI(System.Windows.Controls.ListView listView)
        {
            Action<System.Windows.Controls.ListView> makeVisibleMethod = (lv) => lv.Visibility = Visibility.Visible;
            Application.Current.Dispatcher.Invoke(makeVisibleMethod, listView);
        }

        public static void MakeListViewHiddenOnUI(System.Windows.Controls.ListView listView)
        {
            Action<System.Windows.Controls.ListView> makeHiddenMethod = (lv) => lv.Visibility = Visibility.Hidden;
            Application.Current.Dispatcher.Invoke(makeHiddenMethod, listView);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SizeToContent = SizeToContent.Manual;
        }

    }
}
