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
        );
        CREATE TABLE IF NOT EXISTS favorite_tracks (
        id SERIAL PRIMARY KEY,
        username TEXT,
        title TEXT,
        author TEXT,
        image_url TEXT
        );
        
        CREATE TABLE IF NOT EXISTS playlists_tracks (
        id SERIAL PRIMARY KEY,
        playlist_name TEXT NOT NULL,      -- Название самого плейлиста
        username TEXT NOT NULL,           -- Чей плейлист (владелец)
        track_title TEXT,                 -- Название трека (может быть NULL для пустого плейлиста)
        track_artist TEXT,                -- Исполнитель (может быть NULL)
        image_url TEXT                    -- Ссылка на обложку трека (может быть NULL)
        );
        CREATE INDEX IF NOT EXISTS idx_playlists_user_name ON playlists_tracks (username, playlist_name);
";

            await context.Database.ExecuteSqlRawAsync(sql);
        }


    }

}
