using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MusicAppFront.Resources
{
    public static class PlaylistCommands
    {
        public static readonly RoutedUICommand AddTrackToPlaylist = new RoutedUICommand(
            "Add Track To Playlist",
            "AddTrackToPlaylist",
            typeof(PlaylistCommands)
        );

        // Команда для редиректа на создание плейлиста
        public static readonly RoutedUICommand RedirectToCreatePlaylist = new RoutedUICommand(
            "Redirect To Create Playlist",
            "RedirectToCreatePlaylist",
            typeof(PlaylistCommands)
        );

        public static readonly RoutedUICommand OpenPlaylist = new RoutedUICommand();


    }
}
