using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicAppFront.Models
{
    public class SearchResultDto
    {
        public record TrackDto2(string Title, string Author, string Url, string CleanArtist, string CleanTitle, string ImageUrl);
        public record ArtistDto(string Name, string Url, string ImageUrl, string Bio, string Id);
        public record AlbumDto(
            string Name,
            string ImageUrl,
            string Id,
            string Url,
            int? Playcount
            );

        public List<ArtistDto> Artists { get; set; } = new();
        public List<TrackDto2> Tracks { get; set; } = new();


        [JsonPropertyName("topAlbums")]
        public List<AlbumDto> Albums { get; set; } = new();
    }
}
