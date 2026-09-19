using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace BusTicketSaleSystem.Services;

public sealed class LocalizationService
{
    private static readonly Lazy<LocalizationService> LazyInstance = new(() => new LocalizationService());
    public static LocalizationService Instance => LazyInstance.Value;

    public const string Vietnamese = "vi-VN";
    public const string English = "en-US";

    private string _currentLanguage = Vietnamese;
    private readonly string _settingsFilePath;

    public event Action<string>? LanguageChanged;

    public string CurrentLanguage => _currentLanguage;
    public bool IsVietnamese => _currentLanguage == Vietnamese;
    public bool IsEnglish => _currentLanguage == English;

    private LocalizationService()
    {
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "language.json");
        LoadSavedLanguage();
    }

    public void Initialize()
    {
        ApplyLanguage(_currentLanguage, save: false);
    }

    public void SetLanguage(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return;
        cultureCode = cultureCode.Trim();

        // Normalize
        if (cultureCode.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
            cultureCode = Vietnamese;
        else
            cultureCode = English;

        if (_currentLanguage == cultureCode) return;

        _currentLanguage = cultureCode;
        ApplyLanguage(_currentLanguage, save: true);
        LanguageChanged?.Invoke(_currentLanguage);
    }

    public void ToggleLanguage()
    {
        SetLanguage(IsVietnamese ? English : Vietnamese);
    }

    public string GetString(string key, string fallback = "")
    {
        if (Application.Current != null && Application.Current.TryFindResource(key) is string val)
        {
            return val;
        }
        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }

    /// <summary>
    /// Looks the key up and substitutes <paramref name="args"/> into its <c>{0}</c> placeholders.
    /// Lets ViewModels build localized sentences without holding a resource reference.
    /// </summary>
    public string Format(string key, params object?[] args)
    {
        var template = GetString(key);
        if (args.Length == 0) return template;
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            // A malformed template must not crash a user-facing message.
            return template;
        }
    }

    private void ApplyLanguage(string cultureCode, bool save)
    {
        try
        {
            var culture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            if (Application.Current != null)
            {
                var resourcePath = cultureCode == English
                    ? "Resources/Strings.en-US.xaml"
                    : "Resources/Strings.vi-VN.xaml";

                var newDict = new ResourceDictionary
                {
                    Source = new Uri(resourcePath, UriKind.Relative)
                };

                // Find and replace existing strings dictionary
                var merged = Application.Current.Resources.MergedDictionaries;
                var existing = merged.FirstOrDefault(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("Strings.vi-VN.xaml") ||
                     d.Source.OriginalString.Contains("Strings.en-US.xaml")));

                if (existing != null)
                {
                    var index = merged.IndexOf(existing);
                    merged[index] = newDict;
                }
                else
                {
                    merged.Add(newDict);
                }
            }

            if (save)
            {
                SaveLanguageSetting(cultureCode);
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Không thể áp dụng ngôn ngữ '{cultureCode}'.", ex);
        }
    }

    private void LoadSavedLanguage()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var prop))
                {
                    var lang = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(lang))
                    {
                        _currentLanguage = lang;
                    }
                }
            }
        }
        catch
        {
            _currentLanguage = Vietnamese;
        }
    }

    private void SaveLanguageSetting(string cultureCode)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { Language = cultureCode });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Không thể lưu cài đặt ngôn ngữ.", ex);
        }
    }
}
