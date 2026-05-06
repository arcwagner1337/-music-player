namespace backendxd.DTOS
{
    public record LoginRequest(string Username, string Password);
    public record RegisterRequest(string Username, string Email, string Password);
    public record VerifyRequest(string email, string code);
    public record AuthResponse(string Status, string Username);
    public record TrackDto(string Id, string Title, string Artist, string CoverUrl, string Url);



}
