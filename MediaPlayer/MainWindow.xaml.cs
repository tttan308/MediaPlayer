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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace MediaPlayer;

public partial class MainWindow : RibbonWindow
{
    private ObservableCollection<MediaFile> _playlist = new ObservableCollection<MediaFile>();
    private int _currentIndex = 0;
    private MediaFile _currentFile = null;
    private DispatcherTimer _timer;
    private bool _isDragging = false;
    private bool _isPlaying = false;
    private List<string> _currentPlaylist = new List<string>();
    private Dictionary<string, List<string>> _playlists = new Dictionary<string, List<string>>();
    private Random _random = new Random();
    private bool _isShuffle = false;
    private bool _isPlayVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        PlaylistBox.ItemsSource = _playlist;
        InitializeTimer();
        mediaPlayer.ScrubbingEnabled = true;
        this.Closing += MainWindow_Closing;
        LoadRecentlyPlayedFiles();
        mediaPlayer.MediaOpened += mediaPlayer_MediaOpened;
        PlayButton.Visibility = Visibility.Collapsed;

        // RegisterHotKeys();
    }

    // private void RegisterHotKeys()
    // {
    //     _pauseOrPlay = new HotKey(Key.Space, KeyModifier.None, PlayPauseMedia);
    //     _skip = new HotKey(Key.Right, KeyModifier.None, NextMediaFile);
    // }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var jsonString = System.Text.Json.JsonSerializer.Serialize(_playlist);
        System.IO.File.WriteAllText("playlist.json", jsonString);
        jsonString = System.Text.Json.JsonSerializer.Serialize(_currentFile);
        System.IO.File.WriteAllText("currentFile.json", jsonString);
    }

    private void LoadRecentlyPlayedFiles()
    {
        if (System.IO.File.Exists("playlist.json"))
        {
            var jsonString = System.IO.File.ReadAllText("playlist.json");
            _playlist = System.Text.Json.JsonSerializer.Deserialize<
                ObservableCollection<MediaFile>
            >(jsonString);
            PlaylistBox.ItemsSource = _playlist;
        }

        /*// duyệt hết _playlist, Play tất cả các file trong _playlist, bằng một lý do nào đó nó phải load 2 lần mới chạy ???
        */
        /*for (int i = 0; i < _playlist.Count; i++)
                {
                    // play Những file mp3
                    if (_playlist[i].FileName.EndsWith(".mp3"))
                    {
                        mediaPlayer.Source = new Uri(_playlist[i].FileName);
                        mediaPlayer.LoadedBehavior = MediaState.Manual;
                        mediaPlayer.Play();
                        mediaPlayer.Stop();
                        mediaPlayer.Play();
                    }
                }*/

        if (System.IO.File.Exists("currentFile.json"))
        {
            var jsonString = System.IO.File.ReadAllText("currentFile.json");
            _currentFile = System.Text.Json.JsonSerializer.Deserialize<MediaFile>(jsonString);
            _currentIndex = _playlist.IndexOf(_currentFile);
            PlaylistBox.SelectedIndex = _currentIndex;
        }

        /*if (_currentFile != null)
        {
            SetMediaPlayerSource(_currentFile.FileName);
            mediaPlayer.Position = _currentFile.Position;
            mediaPlayer.Play();
        }*/

        if (_currentFile != null)
        {
            mediaPlayer.MediaOpened -= mediaPlayer_MediaOpened; // Loại bỏ lắng nghe sự kiện cũ
            mediaPlayer.MediaOpened += mediaPlayer_MediaOpened; // Đăng ký sự kiện mới
            mediaPlayer.MediaFailed += mediaPlayer_MediaFailed; // Đăng ký sự kiện lỗi

            mediaPlayer.Position = _currentFile.Position;
            if (!_isPlayVisible)
                mediaPlayer.Play();
        }
    }

    private void mediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show($"Error playing media: {e.ErrorException.Message}");
    }

    private void InitializeTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(.1) };
        _timer.Tick += Timer_Tick;
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

        _playlist.Add(new MediaFile { FileName = fileName });

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
        if (_playlist.Count == 0)
        {
            UpdateTimeLabel(mediaProgressMaxLabel, 0);
            UpdateTimeLabel(mediaProgressMinLabel, 0);
        }

        if (file == _currentFile)
            ResetMediaPlayer();
    }

    private void ResetMediaPlayer()
    {
        mediaPlayer.Stop();
        _currentFile = _playlist.FirstOrDefault();
        if (_currentFile != null)
        {
            SetMediaPlayerSource(_currentFile.FileName);
            mediaPlayer.Position = _currentFile.Position;
            if (!_isPlayVisible)
                mediaPlayer.Play();
        }
        else
            mediaPlayer.Source = null;
    }

    private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

        if (!(PlaylistBox.SelectedItem is MediaFile selectedFile))
            return;

        _currentFile = null;
        _currentFile = selectedFile;
        _currentIndex = _playlist.IndexOf(selectedFile);
        SetMediaPlayerSource(selectedFile.FileName);
        mediaPlayer.Position = selectedFile.Position;
        mediaProgress.Value = selectedFile.Position.TotalSeconds;
        if (!_isPlayVisible)
        {
            mediaPlayer.Play();
        }
    }

    private void SetMediaPlayerSource(string fileName)
    {
        try
        {
            mediaPlayer.Source = new Uri(fileName);
            mediaPlayer.LoadedBehavior = MediaState.Manual;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error setting media source: {ex.Message}");
        }
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

        mediaProgress.Value = 0;
    }

    private void UpdateProgressLabels()
    {
        UpdateTimeLabel(mediaProgressMaxLabel, mediaProgress.Maximum);
        UpdateTimeLabel(mediaProgressMinLabel, mediaProgress.Minimum);
    }

    private void mediaProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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
        // if (mediaProgress.Value == mediaProgress.Maximum)
        // {
        //     PlayNextMediaFile();
        // }
    }

    private void PlayNextMediaFile()
    {
        MoveToNextMediaFile();
        if (_currentFile != null)
        {
            SetMediaPlayerSource(_currentFile.FileName);
            mediaPlayer.Position = _currentFile.Position;
            if (_isPlayVisible)
                mediaPlayer.Play();
        }
    }

    private void MoveToNextMediaFile()
    {
        _currentIndex++;
        if (_currentIndex >= _playlist.Count)
            _currentIndex = 0;

        _currentFile = _playlist.ElementAtOrDefault(_currentIndex);
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

        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

        mediaPlayer.Position = _currentFile.Position;

        mediaPlayer.Play();
        _isPlaying = true;
        _timer.Start();

        _isPlayVisible = false;
        UpdateButtonVisibility();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

        mediaPlayer.Position = _currentFile.Position;
        mediaPlayer.Pause();
        _isPlaying = false;
        _timer.Stop();

        _isPlayVisible = true;
        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
        PlayButton.Visibility = _isPlayVisible ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Visibility = _isPlayVisible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

        mediaPlayer.Position = _currentFile.Position;
        mediaPlayer.Stop();
        _isPlaying = false;
        _timer.Stop();
        ResetMediaProgress();
    }

    private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        mediaPlayer.Volume = volumeSlider.Value / 10;
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
            _playlist = new ObservableCollection<MediaFile>(_playlist.OrderBy(x => _random.Next()));
            PlaylistBox.ItemsSource = _playlist;
            MoveToNextMediaFile();
            if (_currentFile != null)
            {
                SetMediaPlayerSource(_currentFile.FileName);
                mediaPlayer.Position = _currentFile.Position;
                if (_isPlayVisible)
                    mediaPlayer.Play();
            }
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

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
            SetMediaPlayerSourceAndPlay(_currentFile.FileName);
        }

        mediaPlayer.Position = _currentFile.Position;
        if (_isPlayVisible)
            mediaPlayer.Play();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
        {
            _currentFile.Position = mediaPlayer.Position;
            int currentIndex = _playlist.IndexOf(_currentFile);
            if (currentIndex != -1)
            {
                _playlist[currentIndex] = _currentFile;
            }
        }

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
            SetMediaPlayerSourceAndPlay(_currentFile.FileName);
        }
        mediaPlayer.Position = _currentFile.Position;
        if (_isPlayVisible)
            mediaPlayer.Play();
    }

    private void SetMediaPlayerSourceAndPlay(string fileName)
    {
        SetMediaPlayerSource(fileName);
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
            var newPosition = (mousePosition.X / mediaProgress.ActualWidth) * mediaProgress.Maximum;
            mediaPlayer.Position = TimeSpan.FromSeconds(newPosition);
            _isDragging = false;
            mediaPlayer.Play();
        }
    }

    private HotKey _pauseOrPlay;
    private HotKey _skip;

    private void PlayPauseMedia(HotKey key)
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

    private void NextMediaFile(HotKey key)
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
            SetMediaPlayerSourceAndPlay(_currentFile.FileName);
        }
        PlaylistBox.SelectedIndex = _currentIndex;
    }
}
