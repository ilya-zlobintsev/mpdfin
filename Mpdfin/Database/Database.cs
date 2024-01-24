using DistIL.Attributes;

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
    public Node FilesystemRoot;

    [Optimize]
    public BaseItemDto? GetItem(Guid id) => Items.FirstOrDefault(item => item.Id == id);

    public Database(JellyfinClient client, DatabaseStorage storage)
    {
        Storage = storage;
        Client = client;
        FilesystemRoot = Node.BuildTree(this);
    }

    [Optimize]
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
                includeItemTypes: [BaseItemKind.Audio]);

            Items = itemsResponse.Items as List<BaseItemDto>
                ?? itemsResponse.Items.ToList();
            FilesystemRoot = Node.BuildTree(this);

            Log.Debug($"Loaded {Items.Count} items");

            await Storage.Save();

            OnUpdate?.Invoke(this, EventArgs.Empty);
            OnDatabaseUpdated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            OnDatabaseUpdated?.Invoke(this, EventArgs.Empty);
            throw new Exception("Server has no music library configured");
        }
    }
}
