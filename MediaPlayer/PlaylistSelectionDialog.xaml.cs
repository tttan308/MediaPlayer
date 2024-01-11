using System;
using System.Collections.Generic;
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

namespace MediaPlayer
{
    /// <summary>
    /// Interaction logic for PlaylistSelectionDialog.xaml
    /// </summary>
    public partial class PlaylistSelectionDialog : Window
    {
        public PlaylistSelectionDialog(List<string> playlists)
        {
            InitializeComponent();
            lstPlaylists.ItemsSource = playlists;
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string SelectedPlaylist
        {
            get { return lstPlaylists.SelectedItem as string; }
        }
    }
}
