using System.Diagnostics.CodeAnalysis;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.DB;

public class Database
{
    public readonly JellyfinClient Client;
    readonly DatabaseStorage Storage;

    public event EventHandler? OnDatabaseUpdated;
    public event EventHandler? OnUpdate;

    public List<BaseItemDto> Items
    {
        get => Storage.Items;
        private set => Storage.Items = value;
    }

    public BaseItemDto? GetItem(Guid id)
    {
        return Items.Find(item => item.Id == id);
    }

    public Database(JellyfinClient client, DatabaseStorage storage)
    {
        Storage = storage;
        Client = client;
    }

    [RequiresUnreferencedCode("Serialization")]
    public async Task Update()
    {
        Log.Information("Updating database");
        if (OnUpdate is not null)
        {
            OnUpdate(this, new());
        }

        var views = await Client.UserViewsClient.GetUserViewsAsync(Client.CurrentUser.Id);

        var musicCollection = views.Items.Single(item => item.CollectionType == "music");

        if (musicCollection is not null)
        {
            Log.Debug($"Using music collection with id {musicCollection.Id}");

            var itemsResponse = await Client.ItemsClient.GetItemsByUserIdAsync(
                Client.CurrentUser.Id,
                recursive: true,
                parentId: musicCollection.Id,
                includeItemTypes: new[] { BaseItemKind.Audio });

            Items = itemsResponse.Items.ToList();

            Log.Debug($"Loaded {Items.Count} items");

            await Storage.Save();

            if (OnUpdate is not null)
            {
                OnUpdate(this, new());
            }
            if (OnDatabaseUpdated is not null)
            {
                OnDatabaseUpdated(this, new());
            }
        }
        else
        {
            if (OnDatabaseUpdated is not null)
            {
                OnDatabaseUpdated(this, new());
            }
            throw new Exception("Server has no music library configured");
        }
    }
}
