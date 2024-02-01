using Jellyfin.Sdk;

namespace Mpdfin;

public class JellyfinClient
{
    public readonly ItemsClient ItemsClient;
    public readonly UserViewsClient UserViewsClient;
    public readonly PlaystateClient PlaystateClient;

    public readonly UserDto CurrentUser;
    readonly SdkClientSettings Settings;

    public JellyfinClient(string serverUrl, AuthenticationResult authenticationResult)
    {
        HttpClient httpClient = new();

        Settings = ClientSettings();
        Settings.BaseUrl = serverUrl;
        Settings.AccessToken = authenticationResult.AccessToken;

        CurrentUser = authenticationResult.User;

        ItemsClient = new(Settings, httpClient);
        UserViewsClient = new(Settings, httpClient);
        PlaystateClient = new(Settings, httpClient);
    }

    public static Task<AuthenticationResult> Authenticate(string serverUrl, string username, string password)
    {
        var settings = ClientSettings();
        settings.BaseUrl = serverUrl;

        HttpClient httpClient = new();
        UserClient userClient = new(settings, httpClient);

        AuthenticateUserByName request = new()
        {
            Username = username,
            Pw = password
        };

        return userClient.AuthenticateUserByNameAsync(request);
    }

    static SdkClientSettings ClientSettings()
    {
        SdkClientSettings settings = new();
        settings.InitializeClientSettings("Mpdfin", "0.1.0", Environment.MachineName, "1");
        return settings;
    }

    public Uri GetAudioStreamUri(Guid itemId)
    {
        return new Uri($"{Settings.BaseUrl}/Audio/{itemId}/universal?api_key={Settings.AccessToken}&UserId={CurrentUser?.Id}&Container=opus,webm|opus,mp3,aac,m4a|aac,m4b|aac,flac,webma,webm|webma,wav,ogg");
    }
}
