using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace YaDIS3.Core;

public sealed partial class TvdbClient
{
    private async Task<string> TokenAsync(
        CancellationToken cancellationToken
    )
    {
        await _login.WaitAsync(cancellationToken);
        try
        {
            if (
                _token is not null
                && _expires > DateTimeOffset.UtcNow
            )
            {
                return _token;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Clé API TVDB v4 absente."
                );
            }

            var body = new Dictionary<string, string>
            {
                ["apikey"] = settings.ApiKey,
            };
            if (!string.IsNullOrWhiteSpace(settings.Pin))
            {
                body["pin"] = settings.Pin;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(s_api, "login")
            )
            {
                Content = JsonContent.Create(body),
            };
            using HttpResponseMessage response =
                await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Connexion TVDB refusée (HTTP {(int)response.StatusCode}).",
                    null,
                    response.StatusCode
                );
            }

            using JsonDocument json =
                await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken
                    ),
                    cancellationToken: cancellationToken
                );
            string next = ReadText(
                ReadChild(json.RootElement, "data"),
                "token"
            );
            if (
                ReadText(json.RootElement, "status") != "success"
                || next.Length == 0
            )
            {
                throw new InvalidDataException(
                    "TVDB n'a pas renvoyé de jeton."
                );
            }

            _token = next;
            _expires = DateTimeOffset.UtcNow.AddHours(23);
            return next;
        }
        finally
        {
            _login.Release();
        }
    }

    private async Task<JsonDocument> GetAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var refreshed = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            string bearer = await TokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(s_api, path)
            );
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.UserAgent.ParseAdd(
                "yaDIS3/2.5.0-alpha.4"
            );
            using HttpResponseMessage response =
                await http.SendAsync(request, cancellationToken);
            if (
                response.StatusCode
                    == HttpStatusCode.Unauthorized
                && !refreshed
            )
            {
                refreshed = true;
                await _login.WaitAsync(cancellationToken);
                try
                {
                    if (_token == bearer)
                    {
                        _token = null;
                    }
                }
                finally
                {
                    _login.Release();
                }

                continue;
            }

            if (
                response.StatusCode
                    == HttpStatusCode.TooManyRequests
                && attempt < 3
            )
            {
                TimeSpan delay =
                    response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        Math.Clamp(delay.TotalSeconds, 1, 30)
                    ),
                    cancellationToken
                );
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"TVDB : HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode
                );
            }

            JsonDocument json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(
                    cancellationToken
                ),
                cancellationToken: cancellationToken
            );
            if (
                ReadText(json.RootElement, "status") == "success"
            )
            {
                return json;
            }

            json.Dispose();
            throw new InvalidDataException(
                "Réponse TVDB sans statut success."
            );
        }

        throw new HttpRequestException(
            "TVDB reste temporairement indisponible."
        );
    }
}
