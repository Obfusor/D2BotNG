using D2BotNG.Core.Protos;
using D2BotNG.Data;
using D2BotNG.Utilities;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace D2BotNG.Services;

public class ItemServiceImpl : ItemService.ItemServiceBase
{
    private readonly ItemRepository _itemRepository;

    public ItemServiceImpl(ItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public override Task<ListEntitiesResponse> ListEntities(ListEntitiesRequest request, ServerCallContext context)
    {
        var pathPrefix = string.IsNullOrEmpty(request.PathPrefix) ? null : request.PathPrefix;
        var entities = _itemRepository.GetEntities(pathPrefix);

        var response = new ListEntitiesResponse();
        response.Entities.AddRange(entities);

        return Task.FromResult(response);
    }

    public override Task<SearchItemsResponse> Search(SearchItemsRequest request, ServerCallContext context)
    {
        var entityPath = string.IsNullOrEmpty(request.EntityPath) ? null : request.EntityPath;
        var query = string.IsNullOrEmpty(request.Query) ? null : request.Query;

        // Only pass mode filter if any field is set
        ModeFilter? modeFilter = null;
        if (request.ModeFilter != null &&
            (request.ModeFilter.HasHardcore || request.ModeFilter.HasExpansion || request.ModeFilter.HasLadder))
        {
            modeFilter = request.ModeFilter;
        }

        var result = _itemRepository.SearchPaged(entityPath, query, modeFilter, request.Offset, request.Limit);

        var response = new SearchItemsResponse { Total = result.Total };
        response.Results.AddRange(result.Results.Select(r => new SearchResultItem
        {
            Item = r.Item,
            EntityPath = r.EntityPath,
        }));

        return Task.FromResult(response);
    }

    public override async Task<Empty> RemoveItem(RemoveItemRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.EntityPath))
        {
            throw RpcExceptions.InvalidArgument("EntityPath is required");
        }

        if (string.IsNullOrEmpty(request.DescriptionId))
        {
            throw RpcExceptions.InvalidArgument("DescriptionId is required");
        }

        // Reject obvious path-traversal segments and Windows alternate-stream
        // syntax. The repository performs a full-path containment check too as
        // defense in depth, but doing it here gives the caller a proper
        // InvalidArgument response instead of a generic Internal error.
        var segments = request.EntityPath.Split('/', '\\');
        if (segments.Any(s => s == ".." || s.Contains(':')))
        {
            throw RpcExceptions.InvalidArgument("EntityPath must be a relative path under the mules directory");
        }

        try
        {
            var removed = await _itemRepository.RemoveItemAsync(request.EntityPath, request.DescriptionId);
            if (!removed)
            {
                throw RpcExceptions.NotFound("Item", request.DescriptionId);
            }
        }
        catch (ArgumentException ex)
        {
            // Repository's defense-in-depth full-path check failed.
            throw RpcExceptions.InvalidArgument(ex.Message);
        }

        return new Empty();
    }
}
