import requests
import time

def get_audio_url(video_id):
    url = "https://www.youtube.com/youtubei/v1/player"
    
    payload = {
        "videoId": video_id,
        "context": {
            "client": {
                "clientName": "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
                "clientVersion": "2.0",
                "hl": "en",
                "gl": "US"
            }
        }
    }
    
    headers = {
        "User-Agent": "Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0) AppleWebKit/538.1",
        "X-YouTube-Client-Name": "85",
        "X-YouTube-Client-Version": "2.0",
        "Content-Type": "application/json",
        "Origin": "https://www.youtube.com"
    }
    
    url = "https://www.youtube.com/youtubei/v1/player?key=AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8"

    start = time.time()
    response = requests.post(url, json=payload, headers=headers)
    elapsed = time.time() - start
    print(f"request took: {elapsed:.2f}s")
    
    data = response.json()
    formats = data.get("streamingData", {}).get("adaptiveFormats", [])
    audio = [f for f in formats if f.get("mimeType", "").startswith("audio/")]
    
    if not audio:
        print("no audio formats, status:", response.status_code)
        print(data.keys())
        return None
        
    best = max(audio, key=lambda x: x.get("bitrate", 0))
    return best["url"]

url = get_audio_url("5jZ9IoMqHcY")
print("got url:", bool(url))