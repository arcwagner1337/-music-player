using backendxd.DTOS;
using System.Text.Json;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;



namespace backendxd.Services
{


    public class MusicService2
    {
        private readonly YoutubeClient _yt = new YoutubeClient();


        public async Task<TrackDto2?> GetSimilarTrackAsync(string artist, string track)
        {
            string apiKey = "4d8d972f782abe5adfe7a8917e3c6e3d";
            using var client = new HttpClient();

            try
            {
                // 1. Запрашиваем похожие треки
                string url = $"http://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&api_key={apiKey}&format=json&limit=10";

                var response = await client.GetFromJsonAsync<JsonElement>(url);

                if (response.TryGetProperty("similartracks", out var similarTracks))
                {
                    var trackList = similarTracks.GetProperty("track").EnumerateArray().ToList();

                    if (trackList.Count > 0)
                    {
                        // Берем случайный трек из первой пятерки (чтобы не слушать одно и то же)
                        var random = new Random();
                        var selected = trackList[random.Next(0, Math.Min(5, trackList.Count))];

                        string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString();
                        string nextTrack = selected.GetProperty("name").GetString();

                        // 2. Теперь нам нужно найти этот трек на YouTube, чтобы получить URL
                        return await SearchOnYouTubeAsync(nextArtist, nextTrack);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка рекомендаций: {ex.Message}");
            }

            return null; // Если ничего не нашли
        }

        private async Task<TrackDto2?> SearchOnYouTubeAsync(string artist, string track)
        {
            var _yt = new YoutubeClient();
            // Ищем "Artist - Track", ограничиваем поиск 1 результатом
            var searchResult = await _yt.Search.GetVideosAsync($"{artist} {track}").CollectAsync(1);

            if (searchResult.Count > 0)
            {
                var video = searchResult[0];
                return new TrackDto2(video.Title, video.Author.ChannelTitle, video.Url, artist, track);
            }
            return null;
        }




    }


}
