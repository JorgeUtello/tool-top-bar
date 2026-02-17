using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ToolTopBar
{
    public class WeatherResult
    {
        public double Temperature { get; set; }
        public int WeatherCode { get; set; }
        public string IconUri { get; set; } = string.Empty;
    }

    public class WeatherService
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string OpenMeteoUrlTemplate = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current_weather=true";
        private const string iconBaseUri = "https://www.gstatic.com/weather/conditions/v1/svg/";

        private static readonly Dictionary<int, string> WeatherCodeToIcon = new Dictionary<int, string>
        {
            [0] = "sunny_light.svg",
            [1] = "partly_cloudy_light.svg",
            [2] = "cloudy_light.svg",
            [3] = "cloudy_light.svg",
            [51] = "rain_light.svg",
            [55] = "rain_light.svg",
            [61] = "rain_light.svg",
            [65] = "rain_light.svg",
            [80] = "showers_light.svg",
            [82] = "showers_light.svg",
            [95] = "thunderstorm_light.svg",
        };

        public async Task<WeatherResult?> GetWeatherAsync(double latitude, double longitude)
        {
            try
            {
                var url = string.Format(OpenMeteoUrlTemplate, latitude, longitude);
                using var res = await _http.GetAsync(url).ConfigureAwait(false);
                res.EnsureSuccessStatusCode();
                var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var doc = await JsonSerializer.DeserializeAsync<OpenMeteoResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);
                if (doc?.current_weather == null)
                    return null;

                var cw = doc.current_weather;
                var icon = MapCodeToIcon(cw.weathercode);
                return new WeatherResult
                {
                    Temperature = cw.temperature,
                    WeatherCode = cw.weathercode,
                    IconUri = icon
                };
            }
            catch
            {
                return null;
            }
        }

        private static string MapCodeToIcon(int code)
        {
            if (WeatherCodeToIcon.TryGetValue(code, out var file))
                return iconBaseUri + file;
            return iconBaseUri + "cloudy_light.svg";
        }

        private class OpenMeteoResponse
        {
            public CurrentWeather? current_weather { get; set; }
        }

        private class CurrentWeather
        {
            public double temperature { get; set; }
            public int weathercode { get; set; }
        }
    }
}
