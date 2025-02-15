using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Audio.Item.Universal;
using Jellyfin.Sdk.Generated.Models;

namespace Mpdfin;

public class JellyfinClient
{
    public readonly JellyfinApiClient ApiClient;

    public readonly UserDto CurrentUser;
    readonly JellyfinSdkSettings _settings;

    public JellyfinClient(string serverUrl, AuthenticationResult authenticationResult)
    {
        HttpClient httpClient = new();

        _settings = ClientSettings();
        _settings.SetServerUrl(serverUrl);
        _settings.SetAccessToken(authenticationResult.AccessToken);

        CurrentUser = authenticationResult.User!;

        var authProvider = new JellyfinAuthenticationProvider(_settings);
        ApiClient = new(new JellyfinRequestAdapter(authProvider, _settings, httpClient));
    }

    public static Task<AuthenticationResult?> Authenticate(string serverUrl, string username, string password)
    {
        var settings = ClientSettings();
        settings.SetServerUrl(serverUrl);


        var authProvider = new JellyfinAuthenticationProvider(settings);
        var client = new JellyfinApiClient(new JellyfinRequestAdapter(authProvider, settings));

        AuthenticateUserByName request = new()
        {
            Username = username,
            Pw = password,
        };

        return client.Users.AuthenticateByName.PostAsync(request);
    }

    static JellyfinSdkSettings ClientSettings()
    {
        JellyfinSdkSettings settings = new();
        settings.Initialize("Mpdfin", "0.1.0", Environment.MachineName, "1");
        return settings;
    }

    public Uri GetAudioStreamUri(Guid itemId)
    {
        var requestInformation = ApiClient.Audio[itemId].Universal.ToGetRequestInformation(request =>
        {
            request.QueryParameters.UserId = CurrentUser.Id!;
            request.QueryParameters.Container =
                ["opus", "webm|opus", "mp3", "aac", "m4a|aac", "m4b|aac", "flac,webma", "webm|webma", "wav", "ogg"];
        });

        return new($"{requestInformation.URI}&api_key={_settings.AccessToken}");
    }
}
