package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"regexp"
	"time"
)

func main() {
	// ================= ВХОДНЫЕ ДАННЫЕ (ЗАХАРДКОЖЕНЫ) =================
	// Можете менять этот ID на любой другой для тестов
	targetVideoID := "ylqaEGjtg8Q" 
	// =================================================================

	fmt.Println("[Go Standalone] Скрипт запущен в вакууме...")
	fmt.Printf("[Go Standalone] Целевой Video ID: %s\n", targetVideoID)

	// Замеряем время выполнения чистого запроса в Go
	startTime := time.Now()

	// Собираем точный JSON-тело (Payload), который работал в C#
	requestBody := map[string]interface{}{
		"videoId": targetVideoID,
		"context": map[string]interface{}{
			"client": map[string]string{
				"clientName":    "TVHTML5_SIMPLY_EMBEDDED",
				"clientVersion": "2.0",
				"hl":            "ru",
				"gl":            "RU",
			},
		},
	}

	jsonPayload, err := json.Marshal(requestBody)
	if err != nil {
		fmt.Printf("Ошибка сериализации JSON: %v\n", err)
		return
	}

	// Эндпоинт Innertube API
	apiURI := "https://youtubei.googleapis.com/v1/player?key=AIzaSyAO_v9wREp6-wW6v5J9P0A0k8O_W8M3O_Q"

	// Создаем HTTP POST-запрос
	req, err := http.NewRequest("POST", apiURI, bytes.NewBuffer(jsonPayload))
	if err != nil {
		fmt.Printf("Ошибка создания запроса: %v\n", err)
		return
	}

	// Выставляем правильные заголовки
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
	req.Header.Set("Origin", "https://youtube.com")
	req.Header.Set("Referer", "https://youtube.com/")

	// Отправляем запрос через стандартный быстрый HTTP-клиент Go
	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		fmt.Printf("Ошибка сети при отправке запроса: %v\n", err)
		return
	}
	defer resp.Body.Close()

	fmt.Printf("[Go Standalone] Ответ от YouTube получен. Статус-код: %s (%d)\n", resp.Status, resp.StatusCode)

	if resp.StatusCode != 200 {
		// Если Google вернул ошибку, выводим что он прислал (например, HTML-заглушку бана)
		bodyBytes, _ := io.ReadAll(resp.Body)
		fmt.Println("\n--- СЫРОЙ ОТВЕТ СЕРВЕРА (ОШИБКА) ---")
		fmt.Println(string(bodyBytes))
		return
	}

	// Декодируем входящий поток JSON без выделения лишней памяти под строки
	var result map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		fmt.Printf("Ошибка декодирования JSON-ответа: %v\n", err)
		return
	}

	// Идем по цепочке: streamingData -> adaptiveFormats
	streamingData, hasStreaming := result["streamingData"].(map[string]interface{})
	if !hasStreaming {
		fmt.Println("[Ошибка] В ответе YouTube отсутствует объект streamingData. Возможно, трек заблокирован.")
		return
	}

	formats, hasFormats := streamingData["adaptiveFormats"].([]interface{})
	if !hasFormats {
		fmt.Println("[Ошибка] Не найден массив adaptiveFormats.")
		return
	}

	var bestURL string
	var maxBitrate float64

	// Перебираем потоки и ищем лучший звук
	for _, f := range formats {
		fmtObj, ok := f.(map[string]interface{})
		if !ok {
			continue
		}

		mime, _ := fmtObj["mimeType"].(string)
		// Проверяем регуляркой, что это аудио (mimeType содержит audio/)
		if regexp.MustCompile(`^audio/`).MatchString(mime) {
			bitrate, _ := fmtObj["bitrate"].(float64)
			urlProp, _ := fmtObj["url"].(string)

			if bitrate > maxBitrate && urlProp != "" {
				maxBitrate = bitrate
				bestURL = urlProp
			}
		}
	}

	duration := time.Since(startTime)

	// Выводим результаты на экран
	fmt.Println("\n==========================================================================================")
	fmt.Println("РЕЗУЛЬТАТ ВЫПОЛНЕНИЯ В GO:")
	if bestURL != "" {
		fmt.Printf("audurl: %s\n", bestURL)
		fmt.Printf("Максимальный найденный битрейт: %.0f bps\n", maxBitrate)
	} else {
		fmt.Println("audurl: NULL (Прямая ссылка не найдена. Возможно, поток зашифрован в signatureCipher)")
	}
	fmt.Println("==========================================================================================")
	fmt.Printf("Чистое время выполнения скрипта в Go: %v\n", duration)
}
