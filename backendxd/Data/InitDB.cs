using Microsoft.EntityFrameworkCore;

namespace backendxd.Data
{
    public class InitDB
    {
        public static async Task InitDatabase(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string sql = @"
        CREATE TABLE IF NOT EXISTS users (
            id SERIAL PRIMARY KEY, 
            name TEXT UNIQUE, 
            password TEXT, 
            email TEXT, 
            sub_start BIGINT, 
            sub_end BIGINT
        );
        CREATE TABLE IF NOT EXISTS pending_registrations (
            id SERIAL PRIMARY KEY, 
            username TEXT NOT NULL, 
            email TEXT NOT NULL, 
            password TEXT NOT NULL, 
            code VARCHAR(6) NOT NULL, 
            expires_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP + INTERVAL '15 minutes' 
        );";

            await context.Database.ExecuteSqlRawAsync(sql);
        }


    }

}
