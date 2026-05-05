namespace backendxd.DTOS
{
    public record LoginRequest(string Username, string Password);
    public record RegisterRequest(string Username, string Email, string Password);
    public record VerifyRequest(string email, string code);
    public record AuthResponse(string Status, string Username);


}
