using System.Text.Json;

namespace TruckTrackerAnalyst.Infrastructure;

/// <summary>Supabase PostgREST の読み取り専用クライアント。GET しか持たない（書込メソッドを作らない）。</summary>
public sealed class SupabaseReadOnlyClient
{
    private const int PageSize = 1000;   // PostgREST の既定上限
    private readonly HttpClient _http;

    public SupabaseReadOnlyClient(string url, string anonKey, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/rest/v1/");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("apikey", anonKey);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + anonKey);
    }

    /// <summary>query 例: "reports?select=id&amp;date=gte.2026-06-01"。全ページ取得して連結。</summary>
    public async Task<List<JsonElement>> GetAllAsync(string query, CancellationToken ct = default)
    {
        var all = new List<JsonElement>();
        for (var offset = 0; ; offset += PageSize)
        {
            using var res = await _http.GetAsync($"{query}&limit={PageSize}&offset={offset}", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new HttpRequestException($"Supabase GET 失敗 {(int)res.StatusCode}: {body}");
            var page = JsonDocument.Parse(body).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
            all.AddRange(page);
            if (page.Count < PageSize) return all;
        }
    }
}
