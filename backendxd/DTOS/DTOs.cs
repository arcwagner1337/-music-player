namespace backendxd.DTOS
{
    public record LoginRequest(string Username, string Password);
    public record RegisterRequest(string Username, string Email, string Password);
    public record VerifyRequest(string email, string code);
    public record AuthResponse(string Status, string Username);


    public record TrackDto(string Id, string Title, string Author, string Genre, string ThumbUrl, string Url);//убрать потом

    public record TrackDto2(string Title, string Author, string Url, string CleanArtist, string CleanTitle, string ImageUrl);

    public record FavoriteTrackDto(string UserName, string Title, string Author, string ImageUrl);

    public record ArtistDto(string Name, string Url, string ImageUrl, string Bio, string Id);

    public interface ISearchRes { }
    public record SearchResultDtoPreferArtists(
    List<ArtistDto> Artists,
    List<TrackDto2> Tracks,
    List<AlbumDto> TopAlbums
    ) : ISearchRes;

    public record SearchResultDtoPreferTracks(
    List<TrackDto2> Tracks,
    List<AlbumDto> TopAlbums,
    List<ArtistDto> Artists
    ) : ISearchRes;

    public record AlbumDto(
    string Name,
    string ImageUrl,
    string Id,
    string Url,
    int? Playcount
);



}
