from fastapi import FastAPI
import uvicorn
import time
import yt_dlp
from ytmusicapi import YTMusic

app = FastAPI()
COOKIES = "_gcl_au=1.1.459640435.1778471632; PREF=tz=Asia.Novosibirsk&f6=40000000&f7=100&repeat=NONE&autoplay=true; SID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4o-fxuZSOFMU9VIT96g16OxQACgYKAXASARUSFQHGX2MiAM5oG4EKrKl6N1U546eO-xoVAUF8yKpA_VGesvns_6XOHQK70r4H0076; __Secure-1PSID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4oQJPVfazF0lserj9RS1PDzgACgYKAeISARUSFQHGX2MiWYrc-A1A4h6WcJggZzKtlRoVAUF8yKoAdz2-jp6dVt1ppQQwxCfw0076; __Secure-3PSID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4oQlUj9t9ne7SZN5POk5kC-QACgYKAZ0SARUSFQHGX2Mi34xxDUCNNoMMGO-eUY1_6xoVAUF8yKo0aanlC4ExMs_wp1lcn8Go0076; HSID=AHEFHjRtbFKTqYHJ-; SSID=AWpHC0Efl4vO-WHc3; APISID=H-82AgxWGfW_s_xl/Agbm8vJ1EjBh4W3M1; SAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; __Secure-1PAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; __Secure-3PAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; YSC=UEzMIeKtmPo; __Secure-ROLLOUT_TOKEN=CNeky5Kzj9uYfxCS_OKvgaSUAxitvYu9pdGUAw%3D%3D; LOGIN_INFO=AFmmF2swRAIgM-_jKnIxwRQt9KxZlA7HCqGf0-VN1jlfxEOJBhWpn1wCICRQDBLjffuB8LRuekjkADaBl7YcegrdG00OZUS2p_mX:QUQ3MjNmeFN1YmEtS3h4T0lCTm9tdDE2QnV1cDJ2bnBkTXNhdDFtOUV5MUQ5YXFrTVp6U3hVSTFXQkszSEJXakRuSUdxOUk1R0hNYjhuc0JyYUl4TWk0Um5uQS1ZMDNKU2tNUWtVQUR4STEyVG0wNWJFTGhVQWw0RnFJT2xlNkR5X2ctQzZFVnhkdVYxTThHMXhIN1Uxc21OM191N1RvakJR; __Secure-1PSIDTS=sidts-CjQBhkeRdy5L2jDZN94GgE7hy7sKK0WEmAtOC6tgw1J0fJxLBEOS5o0AynSjI-jd8oPGcVfiEAA; __Secure-3PSIDTS=sidts-CjQBhkeRdy5L2jDZN94GgE7hy7sKK0WEmAtOC6tgw1J0fJxLBEOS5o0AynSjI-jd8oPGcVfiEAA; VISITOR_PRIVACY_METADATA=CgJTRRIiEh4SHAsMDg8QERITFBUWFxgZGhscHR4fICEiIyQlJicgbQ%3D%3D; __Secure-YENID=15.YTE=TCRiv0J2eqWdq1SCwxLsX1k2fjKZmJ-4qUZL-E0d_ndX5NoChgD57I1Me6wnHOFf6mM0c4N_mLuGysvaNtOxGRM2jSh-EXDfsa3oNl84QF1kuuxvngixVZQhk08i2T05Tt_QF8KdPP0bbKI6J1dDf_4cwAQdLUiEb0m7DOgyxXR8FiR0RHoRmZjopXfEIddVrmhE1mqcR7pApDGwCdvm48YEjNgA_ntKHeMnyT8ULfjNSb8YTLrvYgkR_jNp5Z6asZwhuHwMowsLKibXCBRq3J3IBgo2YyLzsnihGuuSmLOshoQ3CT3KdpSXgkf4eWwHkXteEeUuLjfAmZL7YBPGpQ; CONSISTENCY=AHzIXrxvKv_l2VSjdSXzRT1AIm7UQh_ANDCvxnnf2-mNM0qKOOcEkKFGM26wYPhfQ1cZ0fExvqHuWCCWO9Qhl_CmPulG8fRVU_MQ7EexcNq9WMMRES6WVbbTFHtx5bUDwaEInrJvIczOzGTp9cLmf2od; __Secure-YEC=CgtGQTdfV0kzdUstNCiR4crQBjIoCgJTRRIiEh4SHAsMDg8QERITFBUWFxgZGhscHR4fICEiIyQlJicgbWLgAgrdAjE1LllURT1UQ1JpdjBKMmVxV2RxMVNDd3hMc1gxazJmaktabUotNHFVWkwtRTBkX25kWDVOb0NoZ0Q1N0kxTWU2d25IT0ZmNm1NMGM0Tl9tTHVHeXN2YU50T3hHUk0yalNoLUVYRGZzYTNvTmw4NFFGMWt1dXh2bmdpeFZaUWhrMDhpMlQwNVR0X1FGOEtkUFAwYmJLSTZKMWREZl80Y3dBUWRMVWlFYjBtN0RPZ3l4WFI4RmlSMFJIb1JtWmpvcFhmRUlkZFZybWhFMW1xY1I3cEFwREd3Q2R2bTQ4WUVqTmdBX250S0hlTW55VDhVTGZqTlNiOFlUTHJ2WWdrUl9qTnA1WjZhc1p3aHVId01vd3NMS2liWENCUnEzSjNJQmdvMll5THpzbmloR3V1U21MT3Nob1EzQ1QzS2RwU1hna2Y0ZVd3SGtYdGVFZVV1TGpmQW1aTDdZQlBHcFE%3D; SIDCC=AKEyXzWclFJ8B_yDb2dUYlMuN1XyE0pb5xAWEkBXlouCN597ERaGMdCem1_b-PXH4YNmnYymFQ; __Secure-1PSIDCC=AKEyXzX8AZ600BCYNp1lSsH4PAmrq7jhqJnKDfZWIL9AzH0WoRVaeRKllKmfXX_uy-NIZPsCfgs; __Secure-3PSIDCC=AKEyXzUwoRK3hsUVXtmJYaki6pUK6AJx6v23FL1-W8au4B9WIK0GLy0nVoKLPWKU0Zx40--IlA"
yt = YTMusic()
yt.auth = {"cookie": "_gcl_au=1.1.459640435.1778471632; PREF=tz=Asia.Novosibirsk&f6=40000000&f7=100&repeat=NONE&autoplay=true; SID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4o-fxuZSOFMU9VIT96g16OxQACgYKAXASARUSFQHGX2MiAM5oG4EKrKl6N1U546eO-xoVAUF8yKpA_VGesvns_6XOHQK70r4H0076; __Secure-1PSID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4oQJPVfazF0lserj9RS1PDzgACgYKAeISARUSFQHGX2MiWYrc-A1A4h6WcJggZzKtlRoVAUF8yKoAdz2-jp6dVt1ppQQwxCfw0076; __Secure-3PSID=g.a000-QiD9mrS9zMNgFWmT-hptfqU47Ne7D7t1dQUADl6077nuk4oQlUj9t9ne7SZN5POk5kC-QACgYKAZ0SARUSFQHGX2Mi34xxDUCNNoMMGO-eUY1_6xoVAUF8yKo0aanlC4ExMs_wp1lcn8Go0076; HSID=AHEFHjRtbFKTqYHJ-; SSID=AWpHC0Efl4vO-WHc3; APISID=H-82AgxWGfW_s_xl/Agbm8vJ1EjBh4W3M1; SAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; __Secure-1PAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; __Secure-3PAPISID=rhU8q5pUy4MmpBOT/Agb-LEth9K7ti6glv; YSC=UEzMIeKtmPo; __Secure-ROLLOUT_TOKEN=CNeky5Kzj9uYfxCS_OKvgaSUAxitvYu9pdGUAw%3D%3D; LOGIN_INFO=AFmmF2swRAIgM-_jKnIxwRQt9KxZlA7HCqGf0-VN1jlfxEOJBhWpn1wCICRQDBLjffuB8LRuekjkADaBl7YcegrdG00OZUS2p_mX:QUQ3MjNmeFN1YmEtS3h4T0lCTm9tdDE2QnV1cDJ2bnBkTXNhdDFtOUV5MUQ5YXFrTVp6U3hVSTFXQkszSEJXakRuSUdxOUk1R0hNYjhuc0JyYUl4TWk0Um5uQS1ZMDNKU2tNUWtVQUR4STEyVG0wNWJFTGhVQWw0RnFJT2xlNkR5X2ctQzZFVnhkdVYxTThHMXhIN1Uxc21OM191N1RvakJR; __Secure-1PSIDTS=sidts-CjQBhkeRdy5L2jDZN94GgE7hy7sKK0WEmAtOC6tgw1J0fJxLBEOS5o0AynSjI-jd8oPGcVfiEAA; __Secure-3PSIDTS=sidts-CjQBhkeRdy5L2jDZN94GgE7hy7sKK0WEmAtOC6tgw1J0fJxLBEOS5o0AynSjI-jd8oPGcVfiEAA; VISITOR_PRIVACY_METADATA=CgJTRRIiEh4SHAsMDg8QERITFBUWFxgZGhscHR4fICEiIyQlJicgbQ%3D%3D; __Secure-YENID=15.YTE=TCRiv0J2eqWdq1SCwxLsX1k2fjKZmJ-4qUZL-E0d_ndX5NoChgD57I1Me6wnHOFf6mM0c4N_mLuGysvaNtOxGRM2jSh-EXDfsa3oNl84QF1kuuxvngixVZQhk08i2T05Tt_QF8KdPP0bbKI6J1dDf_4cwAQdLUiEb0m7DOgyxXR8FiR0RHoRmZjopXfEIddVrmhE1mqcR7pApDGwCdvm48YEjNgA_ntKHeMnyT8ULfjNSb8YTLrvYgkR_jNp5Z6asZwhuHwMowsLKibXCBRq3J3IBgo2YyLzsnihGuuSmLOshoQ3CT3KdpSXgkf4eWwHkXteEeUuLjfAmZL7YBPGpQ; CONSISTENCY=AHzIXrxvKv_l2VSjdSXzRT1AIm7UQh_ANDCvxnnf2-mNM0qKOOcEkKFGM26wYPhfQ1cZ0fExvqHuWCCWO9Qhl_CmPulG8fRVU_MQ7EexcNq9WMMRES6WVbbTFHtx5bUDwaEInrJvIczOzGTp9cLmf2od; __Secure-YEC=CgtGQTdfV0kzdUstNCiR4crQBjIoCgJTRRIiEh4SHAsMDg8QERITFBUWFxgZGhscHR4fICEiIyQlJicgbWLgAgrdAjE1LllURT1UQ1JpdjBKMmVxV2RxMVNDd3hMc1gxazJmaktabUotNHFVWkwtRTBkX25kWDVOb0NoZ0Q1N0kxTWU2d25IT0ZmNm1NMGM0Tl9tTHVHeXN2YU50T3hHUk0yalNoLUVYRGZzYTNvTmw4NFFGMWt1dXh2bmdpeFZaUWhrMDhpMlQwNVR0X1FGOEtkUFAwYmJLSTZKMWREZl80Y3dBUWRMVWlFYjBtN0RPZ3l4WFI4RmlSMFJIb1JtWmpvcFhmRUlkZFZybWhFMW1xY1I3cEFwREd3Q2R2bTQ4WUVqTmdBX250S0hlTW55VDhVTGZqTlNiOFlUTHJ2WWdrUl9qTnA1WjZhc1p3aHVId01vd3NMS2liWENCUnEzSjNJQmdvMll5THpzbmloR3V1U21MT3Nob1EzQ1QzS2RwU1hna2Y0ZVd3SGtYdGVFZVV1TGpmQW1aTDdZQlBHcFE%3D; SIDCC=AKEyXzWclFJ8B_yDb2dUYlMuN1XyE0pb5xAWEkBXlouCN597ERaGMdCem1_b-PXH4YNmnYymFQ; __Secure-1PSIDCC=AKEyXzX8AZ600BCYNp1lSsH4PAmrq7jhqJnKDfZWIL9AzH0WoRVaeRKllKmfXX_uy-NIZPsCfgs; __Secure-3PSIDCC=AKEyXzUwoRK3hsUVXtmJYaki6pUK6AJx6v23FL1-W8au4B9WIK0GLy0nVoKLPWKU0Zx40--IlA"}

# Настройки, чтобы быть "человеком"
ydl_opts = {
    'format': 'bestaudio',
    'quiet': True,
    'no_warnings': True,
    'cookie': COOKIES,
    'user_agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36',
    'http_headers': {
        'Referer': 'https://music.youtube.com/',
    }
}

ydl = yt_dlp.YoutubeDL(ydl_opts)

@app.get("/get_stream")
def get_stream(video_id: str):
    try:
        # Мы используем yt-dlp, но с настройками "маскировки"
        ydl_opts = {
            'format': 'bestaudio/best',
            'quiet': True,
            'no_warnings': True,
            # Важно: используем заголовки, как будто мы браузер
            'http_headers': {
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36',
            },
            # Передаем куки, которые мы уже проверили
            'cookie': COOKIES 
        }
        
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            # Используем прямую ссылку на видео
            info = ydl.extract_info(f"https://www.youtube.com/watch?v={video_id}", download=False)
            
            # В info['url'] должна лежать прямая ссылка на CDN
            return {"url": info['url']}
            
    except Exception as e:
        return {"error": f"Детальная ошибка: {str(e)}"}

# if __name__ == "__main__":
#     uvicorn.run(app, host="127.0.0.1", port=8895)

if __name__ == "__main__":
    # Тестовый ID (подставь любой рабочий ID)
    test_id = "Ov0ssVwxTsk"
    
    print(f"--- Тестируем извлечение для: {test_id} ---")
    
    try:
        # Прямой вызов функции
        result = get_stream(test_id)
        
        if "url" in result:
            print("\nУСПЕХ!")
            print(f"Ссылка: {result['url']}")
        else:
            print("\nОШИБКА:")
            print(result.get("error"))
            
    except Exception as e:
        print(f"Критическая ошибка: {e}")