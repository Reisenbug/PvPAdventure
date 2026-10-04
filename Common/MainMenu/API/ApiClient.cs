using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PvPAdventure.Common.MainMenu.API;

internal static class ApiClient
{
    public static Task<ApiResult<string>> GetStringAsync(string uri, CancellationToken cancellationToken = default) => Offline<string>();
    public static Task<ApiResult<string>> PostStringAsync(string uri, object body = null, CancellationToken cancellationToken = default) => Offline<string>();
    public static Task<ApiResult<T>> GetJsonAsync<T>(string uri, CancellationToken cancellationToken = default) => Offline<T>();
    public static Task<ApiResult<T>> PostJsonAsync<T>(string uri, object body = null, CancellationToken cancellationToken = default) => Offline<T>();

    private static Task<ApiResult<T>> Offline<T>() => Task.FromResult(
        ApiResult<T>.Error(HttpStatusCode.ServiceUnavailable, "Online services are disabled in the independent edition."));
}
