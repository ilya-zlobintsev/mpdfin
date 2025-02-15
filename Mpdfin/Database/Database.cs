using DistIL.Attributes;
using Jellyfin.Sdk.Generated.Models;
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

        var views = await Client.ApiClient.UserViews.GetAsync();

        var musicCollection = views?.Items?.Single(item => item.CollectionType == BaseItemDto_CollectionType.Music);

        if (musicCollection is not null)
        {
            Log.Debug($"Using music collection with id {musicCollection.Id}");

            var itemsResponse = await Client.ApiClient.Items.GetAsync(request =>
            {
                request.QueryParameters.UserId = Client.CurrentUser.Id!;
                request.QueryParameters.Recursive = true;
                request.QueryParameters.ParentId = musicCollection.Id!;
                request.QueryParameters.IncludeItemTypes = [BaseItemKind.Audio];

            });

            Items = itemsResponse?.Items ?? throw new ArgumentNullException(nameof(itemsResponse));
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
