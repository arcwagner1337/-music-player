using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace backendxd.Services
{
    public class GenerateJWT
    {
        private readonly IConfiguration _config;

        public GenerateJWT(IConfiguration config)
        {
            _config = config;
        }


        public string GenerateJwtToken(string username)
        {
            var claims = new[] { new Claim(ClaimTypes.Name, username) };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JWT_SECRET"] ?? "default_secret_key_32_chars_long!!"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "void_core",
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
