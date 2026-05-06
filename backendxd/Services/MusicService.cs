//using YoutubeExplode;
//using YoutubeExplode.Common;
//using YoutubeExplode.Search;
//using YoutubeExplode.Videos.Streams;
using backendxd.DTOS;
using SoundCloudExplode;
using SoundCloudExplode.Common;

namespace backendxd.Services
{
    public class MusicService
    {

        private readonly SoundCloudClient _soundcloud;

        public MusicService(IConfiguration config)
        {
            //  ID из секции SoundCloud
            var clientId = config["SoundCloud:ClientId"] ?? throw new Exception("SoundCloud ClientId not found!");

          
            _soundcloud = new SoundCloudClient(clientId);
        }

        public async Task<List<TrackDto>> SearchAsync(string query)
        {
            try
            {
                // Поиск треков
                var searchResults = await _soundcloud.Search.GetTracksAsync(query);

                return searchResults.Take(10).Select(t => new TrackDto(
                    t.Id.ToString(),
                    t.Title,
                    t.User?.Username ?? "Unknown Artist",
                    t.ArtworkUrl?.ToString() ?? "",
                    t.Url 
                )).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"SoundCloud search failed: {ex.Message}");
            }
        }

        public async Task<string> GetAudioStreamUrl(string trackUrl)
        {
            try
            {
                
                var streamUrl = await _soundcloud.Tracks.GetDownloadUrlAsync(trackUrl);
                return streamUrl;
            }
            catch (Exception ex)
            {
                throw new Exception($"SoundCloud streaming failed: {ex.Message}");
            }
        }
    }



}

