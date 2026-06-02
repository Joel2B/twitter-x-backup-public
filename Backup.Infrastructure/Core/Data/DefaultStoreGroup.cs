using Backup.Application.Core;
using Backup.Infrastructure.Core.Abstractions.Data;

namespace Backup.Infrastructure.Core.Data;

internal sealed class DefaultStoreGroup<TStore>(
    IEnumerable<TStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService? secondaryStoreSelectionService,
    string missingStoreMessage,
    string multipleDefaultMessage
)
    where TStore : IDefaultStore
{
    private readonly List<TStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;
    private readonly ISecondaryStoreSelectionService? _secondaryStoreSelectionService =
        secondaryStoreSelectionService;
    private readonly string _missingStoreMessage = missingStoreMessage;
    private readonly string _multipleDefaultMessage = multipleDefaultMessage;

    public IReadOnlyList<TStore> Stores => _stores;

    public TStore Primary =>
        _primarySelectionService.ResolvePrimary(
            _stores,
            store => store.IsDefault,
            _missingStoreMessage,
            _multipleDefaultMessage
        );

    public IReadOnlyList<TStore> GetSecondaries(TStore primary)
    {
        if (_secondaryStoreSelectionService is null)
            return [];

        return
        [
            .. _secondaryStoreSelectionService.SelectSecondaries(_stores, primary),
        ];
    }
}
