from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
import yt_dlp
import json
import time
import sys

port = int(sys.argv[1]) if len(sys.argv) > 1 else 8888
cookies = sys.argv[2] if len(sys.argv) > 2 else "LoginCookies.txt"

class Handler(BaseHTTPRequestHandler):

    

    def warmup():
        print("warming up yt-dlp...")
    try:
        opts = {
            'format': '251',
            'quiet': True,
            'no_warnings': True,
            'cookiefile': cookies,
            'nocheckcertificate': True,
            'socket_timeout': 5,
            'extractor_args': {
                'youtube': {
                    'skip': ['dash', 'hls']
                }
            }
        }
        with yt_dlp.YoutubeDL(opts) as ydl:
            ydl.extract_info("https://youtu.be/dQw4w9WgXcQ", download=False)
        print("yt-dlp warmed up!")
    except Exception as e:
        print(f"warmup error: {e}")


    def do_GET(self):
        parsed = urlparse(self.path)
        params = parse_qs(parsed.query)
        
        if 'url' not in params:
            self.send_response(400)
            self.end_headers()
            return
        
        yt_url = params['url'][0]
        print(f"resolving: {yt_url}, port: {port}")
        try:
            opts = {
                'format': '251',
                'quiet': True,
                'no_warnings': True,
                'cookiefile': cookies,
                'nocheckcertificate': True,
                'socket_timeout': 5,
                'extractor_args': {
                    'youtube': {
                        'skip': ['dash', 'hls']
                        
                    }
                }
            }
            start = time.time()
            with yt_dlp.YoutubeDL(opts) as ydl:
                info = ydl.extract_info(yt_url, download=False)
                audio_url = info['url']
                duration = info.get('duration', 0)
            elapsed = time.time() - start
            print(f"resolved in {elapsed:.2f}s")
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({
                'url': audio_url,
                'duration': duration
            }).encode())
                
        except Exception as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(str(e).encode())
        
    def log_message(self, format, *args):
        pass # тишина в консоли
    warmup()
    
print(f"yt-dlp server on :{port}")
HTTPServer(('localhost', port), Handler).serve_forever()



