using backendxd.DTOS;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;



namespace backendxd.Services
{


    public class MusicService2
    {
        private readonly YoutubeClient _yt = new YoutubeClient();


        public async Task<SearchResultDto> SmartSearchAsync2(string query)
        {
            using var client = new HttpClient();
            
            var dzResponse = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit=20");

            var artists = new List<ArtistDto>();
            var tracks = new List<TrackDto2>();
            var topAlbums = new List<AlbumDto>();

            if (dzResponse.TryGetProperty("data", out var data))
            {

                var items = data.EnumerateArray().ToList();

                
                var uniqueArtists = items
                    .Select(item => item.GetProperty("artist"))
                    .GroupBy(a => a.GetProperty("id").GetInt64()) 
                    .Select(g => g.First())
                    .Take(5); 

                var sortedArtists = uniqueArtists.OrderByDescending(a =>
                a.GetProperty("name").GetString().Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0);

                foreach (var dzArtist in sortedArtists)
                {
                    string artistName = dzArtist.GetProperty("name").GetString();

                    artists.Add(new ArtistDto(
                        artistName,
                        "",
                        dzArtist.GetProperty("picture_xl").GetString(),
                        "Биография загружается...",
                        dzArtist.GetProperty("id").GetInt64().ToString()
                    ));
                }

                var sortedItems = items.OrderByDescending(item =>
                    item.GetProperty("artist").GetProperty("name").GetString()
                    .Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0
                    ).Take(10);

                
                foreach (var item in sortedItems.Take(10))
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString(),
                        item.GetProperty("artist").GetProperty("name").GetString(),
                        "", 
                        item.GetProperty("artist").GetProperty("name").GetString(),
                        item.GetProperty("title").GetString(),
                        item.GetProperty("album").GetProperty("cover_big").GetString()
                    ));
                }

               
                var uniqueAlbums = items
                    .GroupBy(x => x.GetProperty("album").GetProperty("id").GetInt64())
                    .Take(6);

                foreach (var albGroup in uniqueAlbums)
                {
                    var alb = albGroup.First().GetProperty("album");
                    topAlbums.Add(new AlbumDto(
                        alb.GetProperty("title").GetString(),
                        alb.GetProperty("cover_xl").GetString(),
                        alb.GetProperty("id").GetInt64().ToString(),
                        "",
                        0
                    ));
                }
            }

            return new SearchResultDto(artists, tracks, topAlbums);
        }

        public async Task<List<TrackDto2>> GetAlbumTracksAsync(long albumId)
        {
            using var client = new HttpClient();
            
            var response = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/album/{albumId}");

            var tracks = new List<TrackDto2>();

            if (response.TryGetProperty("tracks", out var tracksProp))
            {
                var data = tracksProp.GetProperty("data").EnumerateArray();
                string artistName = response.GetProperty("artist").GetProperty("name").GetString();
                string albumCover = response.GetProperty("cover_big").GetString();

                foreach (var item in data)
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString(),
                        artistName,
                        "", 
                        artistName,
                        item.GetProperty("title").GetString(),
                        albumCover 
                    ));
                }
            }

            return tracks;
        }


        public async Task<List<AlbumDto>> GetArtistAlbumsAsync(long artistId)
        {
            using var client = new HttpClient();
            
            var response = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/artist/{artistId}/albums");

            var albums = new List<AlbumDto>();

            if (response.TryGetProperty("data", out var data))
            {
                foreach (var alb in data.EnumerateArray())
                {
                    albums.Add(new AlbumDto(
                        alb.GetProperty("title").GetString(),
                        alb.GetProperty("cover_xl").GetString(),
                        alb.GetProperty("id").GetInt64().ToString(),
                        "", 
                        0   
                    ));
                }
            }

            return albums;
        }





        private async Task<TrackDto2?> SearchOnYouTubeAsync(string artist, string track)
        {
            
            var searchQuery = $"\"{artist}\" {track} official audio";
            var searchResult = await _yt.Search.GetVideosAsync(searchQuery).CollectAsync(10);

            if (searchResult.Count > 0)
            {
                
                string[] stopWords = { "live", "concert", "remix", "cover", "festival", "tour" };

                var video = searchResult.OrderByDescending(v =>
                {
                    
                    int score = 0;
                    
                    if (v.Author.ChannelTitle.Equals($"{artist} - Topic", StringComparison.OrdinalIgnoreCase)) score += 10;
                    
                    if (v.Title.Contains("Official Audio", StringComparison.OrdinalIgnoreCase)) score += 5;
                    return score;
                }).FirstOrDefault(v =>
                    v.Duration > TimeSpan.FromMinutes(1) &&
                    v.Duration < TimeSpan.FromMinutes(15) &&
                    // Проверка на стоп-слова: если в названии есть "live", скипаем
                    !stopWords.Any(word => v.Title.Contains(word, StringComparison.OrdinalIgnoreCase)) &&
                    // Убеждаемся, что это всё еще нужный нам артист
                    (v.Author.ChannelTitle.Contains(artist, StringComparison.OrdinalIgnoreCase) ||
                     v.Title.Contains(artist, StringComparison.OrdinalIgnoreCase))
                ) ?? searchResult.FirstOrDefault();

                if (video != null)
                {
                    string imageUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Width).First().Url;

                    return new TrackDto2(
                        video.Title,
                        video.Author.ChannelTitle,
                        video.Url,
                        artist,
                        track,
                        imageUrl
                    );
                }
            }
            return null;
        }


        public async Task<string> GetFullStreamByTrackInfoAsync(string artist, string track)
        {
            try
            {
                
                var ytTrack = await SearchOnYouTubeAsync(artist, track);

                if (ytTrack == null || string.IsNullOrEmpty(ytTrack.Url))
                    return string.Empty;

                
                return await GetAudioStreamUrl(ytTrack.Url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Ошибка при получении потока: {ex.Message}");
                return string.Empty;
            }
        }



        public async Task<string> GetAudioStreamUrl(string videoId)
        {
            var manifest = await _yt.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            return streamInfo.Url;
        }



        public async Task<TrackDto2?> GetSimilarTrackAsync(string artist, string track)
        {
            string apiKey = "4d8d972f782abe5adfe7a8917e3c6e3d";
            using var client = new HttpClient();

            try
            {
                
                string url = $"http://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&api_key={apiKey}&format=json&limit=10";

                var response = await client.GetFromJsonAsync<JsonElement>(url);

                if (response.TryGetProperty("similartracks", out var similarTracks))
                {
                    var trackList = similarTracks.GetProperty("track").EnumerateArray().ToList();

                    if (trackList.Count > 0)
                    {
                        
                        var random = new Random();
                        var selected = trackList[random.Next(0, Math.Min(5, trackList.Count))];

                        string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString();
                        string nextTrack = selected.GetProperty("name").GetString();

                        
                        string dzSearchUrl = $"https://api.deezer.com/search?q=artist:\"{Uri.EscapeDataString(nextArtist)}\" track:\"{Uri.EscapeDataString(nextTrack)}\"&limit=1";
                        var dzResponse = await client.GetFromJsonAsync<JsonElement>(dzSearchUrl);

                        string imageUrl = ""; 

                        if (dzResponse.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                        {
                            var firstMatch = data.EnumerateArray().First();
                            imageUrl = firstMatch.GetProperty("album").GetProperty("cover_big").GetString();
                        }

                   
                        return new TrackDto2(
                            nextTrack,
                            nextArtist,
                            "", 
                            nextArtist,
                            nextTrack,
                            imageUrl
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка рекомендаций: {ex.Message}");
            }

            return null; 
        }

    }


}
