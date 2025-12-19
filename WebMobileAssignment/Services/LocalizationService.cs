using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebMobileAssignment.Services
{
    public class LocalizationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _resourcePath;
        private Dictionary<string, Dictionary<string, string>> _translations;

        public LocalizationService(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            _resourcePath = Path.Combine(env.ContentRootPath, "Resources");
            _translations = new Dictionary<string, Dictionary<string, string>>();
            LoadTranslations();
        }

        public string CurrentLanguage
        {
            get
            {
                var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["Language"];
                return cookie ?? "en";
            }
        }

        private void LoadTranslations()
        {
            if (!Directory.Exists(_resourcePath))
            {
                Directory.CreateDirectory(_resourcePath);
            }

            var resourceFiles = Directory.GetFiles(_resourcePath, "*.json");
            foreach (var file in resourceFiles)
            {
                var lang = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                _translations[lang] = translations ?? new Dictionary<string, string>();
            }
        }

        public string Get(string key)
        {
            var lang = CurrentLanguage;
            if (_translations.ContainsKey(lang) && _translations[lang].ContainsKey(key))
            {
                return _translations[lang][key];
            }
            // Fallback to English
            if (lang != "en" && _translations.ContainsKey("en") && _translations["en"].ContainsKey(key))
            {
                return _translations["en"][key];
            }
            // Return key if not found
            return key;
        }

        public string this[string key] => Get(key);

        public void SetLanguage(string language)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                IsEssential = true
            };
            _httpContextAccessor.HttpContext?.Response.Cookies.Append("Language", language, cookieOptions);
        }

        public async Task<bool> AutoTranslate(string targetLang)
        {
            try
            {
                if (!_translations.ContainsKey("en"))
                {
                    return false;
                }

                var sourceTranslations = _translations["en"];
                var targetTranslations = new Dictionary<string, string>();

                foreach (var kvp in sourceTranslations)
                {
                    var translatedText = await TranslateText(kvp.Value, "en", targetLang);
                    targetTranslations[kvp.Key] = translatedText ?? kvp.Value;
                    
                    // Add delay to avoid rate limiting
                    await Task.Delay(500);
                }

                // Save to file
                var filePath = Path.Combine(_resourcePath, $"{targetLang}.json");
                var json = JsonSerializer.Serialize(targetTranslations, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);

                // Reload translations
                _translations[targetLang] = targetTranslations;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoTranslate error: {ex.Message}");
                return false;
            }
        }

        private async Task<string?> TranslateText(string text, string sourceLang, string targetLang)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // Try MyMemory Translation API (more reliable, no rate limiting)
                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={sourceLang}|{targetLang}";
                
                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseContent);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("responseStatus", out var status) && status.GetInt32() == 200)
                        {
                            if (root.TryGetProperty("responseData", out var data) && 
                                data.TryGetProperty("translatedText", out var translated))
                            {
                                var result = translated.GetString();
                                if (!string.IsNullOrEmpty(result) && result != text)
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MyMemory translation error: {ex.Message}");
                }

                // Fallback: Return original text if translation fails
                return text;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation API error: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation API timeout: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
                return null;
            }
        }

        public List<LanguageOption> GetAvailableLanguages()
        {
            return new List<LanguageOption>
            {
                new LanguageOption { Code = "en", Name = "English", Flag = "🇬🇧" },
                new LanguageOption { Code = "ms", Name = "Bahasa Melayu", Flag = "🇲🇾" }
            };
        }
    }

    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
    }
}
