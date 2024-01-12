using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Fluent;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace MediaPlayer
{
    public partial class MainWindow : RibbonWindow
    {
        private ObservableCollection<MediaFile> _playlist = new ObservableCollection<MediaFile>();
        private int _currentIndex = 0;
        private MediaFile _currentFile = null;
        private DispatcherTimer _timer;
        private bool _isDragging = false;
        private bool _isPlaying = false;
        private List<string> _currentPlaylist = new List<string>();
        private Dictionary<string, List<string>> _playlists =
            new Dictionary<string, List<string>>();
        private Random _random = new Random();
        private bool _isShuffle = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeComponents();
            LoadRecentFiles();
        }

        private void InitializeComponents()
        {
            PlaylistBox.ItemsSource = _playlist;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.1) };
            _timer.Tick += Timer_Tick;
            mediaPlayer.ScrubbingEnabled = true;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveRecentFiles();
        }

        private void SaveRecentFiles()
        {
            var recentFiles = _playlist.Select(file => new { file.FileName, file.Position }).ToList();
            var json = JsonConvert.SerializeObject(recentFiles);
            File.WriteAllText("recentFiles.json", json);
        }

        private void LoadRecentFiles()
        {
            if (!File.Exists("recentFiles.json")) return;
            var json = File.ReadAllText("recentFiles.json");
            var recentFiles = JsonConvert.DeserializeObject<List<MediaFile>>(json);
            foreach (var file in recentFiles)
            {
                AddFileToPlaylist(file.FileName);
                _playlist.Last().Position = file.Position;
            }
        }

        private void AddMediaFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media files (*.mp3;*.mp4;*.wav)|*.mp3;*.mp4;*.wav|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            foreach (var fileName in openFileDialog.FileNames)
            {
                AddFileToPlaylist(fileName);
            }
        }

        private void AddFileToPlaylist(string fileName)
        {
            if (_playlist.Any(file => file.FileName == fileName))
            {
                MessageBox.Show($"The file {fileName} is already in the playlist.");
                return;
            }

            _playlist.Add(new MediaFile { FileName = fileName, Position = TimeSpan.Zero });

            if (!_currentPlaylist.Contains(fileName))
            {
                _currentPlaylist.Add(fileName);
            }
        }

        private void RemoveMediaFile(object sender, RoutedEventArgs e)
        {
            if (!(PlaylistBox.SelectedItem is MediaFile selectedFile))
                return;

            HandleMediaRemoval(selectedFile);
        }

        private void HandleMediaRemoval(MediaFile file)
        {
            _playlist.Remove(file);
            if (file == _currentFile)
                ResetMediaPlayer();
        }

        private void ResetMediaPlayer()
        {
            mediaPlayer.Stop();
            _currentFile = _playlist.FirstOrDefault();
            if (_currentFile != null)
                SetMediaPlayerSource(_currentFile.FileName);
            else
                mediaPlayer.Source = null;
        }

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || !(e.AddedItems[0] is MediaFile selectedFile))
                return;

            _currentFile = selectedFile;
            _currentIndex = _playlist.IndexOf(selectedFile);
            SetMediaPlayerSource(selectedFile.FileName);
            mediaPlayer.Position = selectedFile.Position;
            UpdateMediaProgress();
            mediaPlayer.Play();
        }

        private void SetMediaPlayerSource(string fileName)
        {
            mediaPlayer.Source = null;
            mediaPlayer.Source = new Uri(fileName);
            mediaPlayer.LoadedBehavior = MediaState.Manual;
        }

        private void mediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                mediaProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }

            UpdateProgressLabels();
            _isPlaying = true;
            _timer.Start();
        }

        private void UpdateProgressLabels()
        {
            UpdateTimeLabel(mediaProgressMaxLabel, mediaProgress.Maximum);
            UpdateTimeLabel(mediaProgressMinLabel, mediaProgress.Minimum);
        }

        private void mediaProgress_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e
        )
        {
            if (_isDragging)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(mediaProgress.Value);
            }

            UpdateTimeLabel(mediaProgressMinLabel, mediaProgress.Value);
            HandleMediaProgressCompletion();
        }

        private void HandleMediaProgressCompletion()
        {
            if (mediaProgress.Value == mediaProgress.Maximum)
            {
                PlayNextMediaFile();
            }
        }

        private void PlayNextMediaFile()
        {
            MoveToNextMediaFile();
            if (_currentFile != null)
            {
                SetMediaPlayerSource(_currentFile.FileName);
                mediaPlayer.Play();
            }
        }

        private void MoveToNextMediaFile()
        {
            _currentIndex++;
            if (_currentIndex >= _playlist.Count)
                _currentIndex = 0;

            _currentFile = _playlist.ElementAtOrDefault(_currentIndex);
            PlaylistBox.SelectedIndex = _currentIndex;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isDragging)
            {
                UpdateMediaProgress();
            }
        }

        private void UpdateMediaProgress()
        {
            mediaProgress.Value = mediaPlayer.Position.TotalSeconds;
            UpdateTimeLabel(mediaProgressMinLabel, mediaProgress.Value);
        }

        private void UpdateTimeLabel(Label label, double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            label.Content = string.Format("{0:D2}:{1:D2}", time.Minutes, time.Seconds);
        }

        private void mediaProgress_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
        }

        private void mediaProgress_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(mediaProgress.Value);
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile != null && !mediaPlayer.HasAudio && !mediaPlayer.HasVideo)
            {
                SetMediaPlayerSource(_currentFile.FileName);
            }

            mediaPlayer.Play();
            _isPlaying = true;
            _timer.Start();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            _isPlaying = false;
            _timer.Stop();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            _isPlaying = false;
            _timer.Stop();
            ResetMediaProgress();
        }

        private void volumeSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e
        )
        {
            mediaPlayer.Volume = volumeSlider.Value / 100;
        }

        private void ResetMediaProgress()
        {
            mediaProgress.Value = 0;
            UpdateTimeLabel(mediaProgressMinLabel, 0);
        }

        private void SavePlaylist(object sender, RoutedEventArgs e)
        {
            var dialog = new InputBox("Enter playlist name");
            if (dialog.ShowDialog() == true)
            {
                var playlistName = dialog.Answer;
                if (string.IsNullOrWhiteSpace(playlistName))
                {
                    MessageBox.Show("Please enter a valid playlist name.");
                    return;
                }

                if (!_playlists.ContainsKey(playlistName))
                {
                    _playlists[playlistName] = _currentPlaylist.ToList();
                    var json = JsonConvert.SerializeObject(_playlists, Formatting.Indented);
                    File.WriteAllText("playlists.json", json);
                    MessageBox.Show("Playlist saved successfully.");
                }
                else
                {
                    MessageBox.Show("A playlist with this name already exists.");
                }
            }
        }

        private void LoadPlaylist(object sender, RoutedEventArgs e)
        {
            if (!File.Exists("playlists.json"))
            {
                MessageBox.Show("No saved playlists found.");
                return;
            }

            var json = File.ReadAllText("playlists.json");
            _playlists = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

            var dialog = new PlaylistSelectionDialog(_playlists.Keys.ToList());
            if (dialog.ShowDialog() == true)
            {
                var playlistName = dialog.SelectedPlaylist;
                if (_playlists.ContainsKey(playlistName))
                {
                    _currentPlaylist.Clear();
                    _currentPlaylist.AddRange(_playlists[playlistName]);
                    _playlist.Clear();
                    foreach (var fileName in _currentPlaylist)
                    {
                        _playlist.Add(new MediaFile { FileName = fileName });
                    }
                    MessageBox.Show($"Playlist '{playlistName}' loaded successfully.");
                }
            }
        }

        private void ClearPlaylist(object sender, RoutedEventArgs e)
        {
            _currentPlaylist.Clear();
            _playlist.Clear();
            mediaPlayer.Source = null;
            ResetMediaProgress();
            MessageBox.Show("Playlist cleared.");
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            _isShuffle = !_isShuffle;
            ShufflePlaylist();
        }

        private void ShufflePlaylist()
        {
            if (_playlist.Count > 0)
            {
                _playlist = new ObservableCollection<MediaFile>(
                    _playlist.OrderBy(x => _random.Next())
                );
                PlaylistBox.ItemsSource = _playlist;
                MoveToNextMediaFile();
                if (_currentFile != null)
                {
                    SetMediaPlayerSource(_currentFile.FileName);
                    mediaPlayer.Play();
                }
            }
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_isShuffle)
            {
                var random = new Random();
                var nextIndex = random.Next(_playlist.Count);
                mediaPlayer.Source = new Uri(_playlist[nextIndex].FileName);
                _currentIndex = nextIndex;
            }
            else
            {
                if (_playlist.Count == 0)
                {
                    MessageBox.Show("The playlist is empty.");
                    return;
                }

                _currentIndex--;
                if (_currentIndex < 0)
                {
                    _currentIndex = _playlist.Count - 1;
                }

                _currentFile = _playlist[_currentIndex];
                SetMediaPlayerSourceAndPlay(_currentFile);
            }
            PlaylistBox.SelectedIndex = _currentIndex;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_isShuffle)
            {
                var random = new Random();
                var nextIndex = random.Next(_playlist.Count);
                mediaPlayer.Source = new Uri(_playlist[nextIndex].FileName);
                _currentIndex = nextIndex;
            }
            else
            {
                if (_playlist.Count == 0)
                {
                    MessageBox.Show("The playlist is empty.");
                    return;
                }

                _currentIndex++;
                if (_currentIndex >= _playlist.Count)
                {
                    _currentIndex = 0;
                }

                _currentFile = _playlist[_currentIndex];
                SetMediaPlayerSourceAndPlay(_currentFile);
            }
            PlaylistBox.SelectedIndex = _currentIndex;
        }

        private void SetMediaPlayerSourceAndPlay(MediaFile file)
        {
            SetMediaPlayerSource(file.FileName);
            mediaPlayer.Position = file.Position;
            mediaPlayer.Play();
        }

        private void mediaProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            var mousePosition = e.GetPosition(mediaProgress);
            var newPosition = (mousePosition.X / mediaProgress.ActualWidth) * mediaProgress.Maximum;
            mediaProgress.Value = newPosition;
            mediaPlayer.Pause();
        }

        private void mediaPlayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying)
            {
                mediaPlayer.Pause();
                _isPlaying = false;
                _timer.Stop();
            }
            else
            {
                mediaPlayer.Play();
                _isPlaying = true;
                _timer.Start();
            }
        }

        private void mediaProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                var mousePosition = e.GetPosition(mediaProgress);
                var newPosition =
                    (mousePosition.X / mediaProgress.ActualWidth) * mediaProgress.Maximum;
                mediaPlayer.Position = TimeSpan.FromSeconds(newPosition);
                _isDragging = false;
                mediaPlayer.Play();
            }
        }
    }
}
