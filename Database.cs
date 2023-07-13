using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin;

class Database
{
    readonly ItemsClient itemsClient;
    readonly UserViewsClient userViewsClient;

    public List<BaseItemDto> Items = new();
    readonly UserDto CurrentUser;
    readonly SdkClientSettings Settings;

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
        settings.InitializeClientSettings("dotnet test", "0.0.1", "desktop", "1");
        return settings;
    }

    public Database(string serverUrl, AuthenticationResult authenticationResult)
    {
        HttpClient httpClient = new();

        var settings = ClientSettings();
        settings.BaseUrl = serverUrl;
        settings.AccessToken = authenticationResult.AccessToken;
        Settings = settings;

        CurrentUser = authenticationResult.User;

        itemsClient = new(settings, httpClient);
        userViewsClient = new(settings, httpClient);
    }

    public async Task Update()
    {
        Log.Debug("Updating database");

        var views = await userViewsClient.GetUserViewsAsync(CurrentUser.Id);

        var musicCollection = views.Items.Single(item => item.CollectionType == "music");

        if (musicCollection is not null)
        {
            Log.Debug($"Using music collection with id {musicCollection.Id}");

            var itemsResponse = await itemsClient.GetItemsByUserIdAsync(
                CurrentUser.Id,
                recursive: true,
                parentId: musicCollection.Id,
                includeItemTypes: new[] { BaseItemKind.Audio });

            Items = itemsResponse.Items.ToList();

            Log.Debug($"Loaded {Items.Count} items");
        }
        else
        {
            throw new Exception("Server has no music library configured");
        }

        // var userResponse = await artistsClient.GetArtistsAsync();
        // Artists = (List<BaseItemDto>)userResponse.Items;
        // Log.Debug($"Loaded {Artists.Count} artists");
        // Artists = (List<BaseItemDto>)userResponse.Items;
        // Log.Debug($"Loaded {Artists.Count} artists");
    }

    public Uri GetAudioStreamUri(Guid itemId)
    {
        return new Uri($"{Settings.BaseUrl}/Audio/{itemId}/universal?api_key={Settings.AccessToken}&UserId={CurrentUser?.Id}&Container=opus,webm|opus,mp3,aac,m4a|aac,m4b|aac,flac,webma,webm|webma,wav,ogg");
    }
}
