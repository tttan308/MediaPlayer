using System;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using Fluent;
using Microsoft.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace MediaPlayer
{
    public partial class MainWindow : RibbonWindow
    {
        public ObservableCollection<MediaFile> playlist { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            playlist = new ObservableCollection<MediaFile>();
            PlaylistBox.ItemsSource = playlist;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) { }

        private void AddMediaFile(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media files (*.mp3;*.mp4;*.wav)|*.mp3;*.mp4;*.wav|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    playlist.Add(new MediaFile { FileName = fileName });
                }
            }
        }

        private void RemoveMediaFile(object sender, RoutedEventArgs e) 
        {
            try 
            {
                var selectedFile = PlaylistBox.SelectedItem as MediaFile;
                if (selectedFile != null)
                {
                    // Check if the selected file is the one currently playing
                    if (selectedFile == currentFile)
                    {
                        // Stop the current file
                        mediaPlayer.Stop();

                        // Remove the file from the playlist
                        playlist.Remove(selectedFile);

                        // Check if there are any files left in the playlist
                        if (playlist.Count > 0)
                        {
                            // Get the next file in the playlist
                            var nextFile = playlist[0];

                            // Set the next file as the current file
                            currentFile = nextFile;

                            // Play the next file
                            mediaPlayer.Source = new Uri(nextFile.FilePath);
                            mediaPlayer.Play();
                        }
                    }
                    else
                    {
                        // If the selected file is not the one currently playing, just remove it from the playlist
                        playlist.Remove(selectedFile);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception or show a message to the user
                Console.WriteLine(ex.Message);
            }
        }

        private void ShowRecentFiles(object sender, RoutedEventArgs e) { }

        private void SearchMediaFiles(object sender, RoutedEventArgs e) { }

        var currentFile = playlist[0];
        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var selectedFile = e.AddedItems[0] as MediaFile; 
                mediaPlayer.Source = new Uri(selectedFile.FileName);
                mediaPlayer.LoadedBehavior = MediaState.Manual;
                currentFile = selectedFile; 
                mediaPlayer.Play();
            }
        }

        public class MediaFile
        {
            public string FileName { get; set; }
        }

        private void Previous_Click(object sender, RoutedEventArgs e) {
            // Previous song

         }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Play();
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Pause();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e) { }

        private void Next_Click(object sender, RoutedEventArgs e) { }
    }
}
