using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols;

/// <summary>
/// ABI of events defined by popular EVM standards (ERC-20, ERC-721, ERC-1155, etc.),
/// used to guess name and payload of logs emitted by contracts with unknown ABI.
/// </summary>
public static class KnownTopicsAbi
{
    /// <summary>
    /// Looks up a known event by the first topic + the number of topics of the given log.
    /// </summary>
    public static bool TryGetEvent(byte[][] topics, [NotNullWhen(true)] out EventAbi? @event)
    {
        if (topics.Length != 0 && topics[0].Length == 32 &&
            Events.TryGetValue(BinaryPrimitives.ReadUInt64BigEndian(topics[0]), out var candidates))
        {
            foreach (var (indexed, abi) in candidates)
            {
                if (indexed == topics.Length - 1 && abi.TopicBytes.IsEqual(topics[0]))
                {
                    @event = abi;
                    return true;
                }
            }
        }

        @event = null;
        return false;
    }

    static readonly FrozenDictionary<ulong, (int Indexed, EventAbi Abi)[]> Events = BuildEvents();

    static FrozenDictionary<ulong, (int Indexed, EventAbi Abi)[]> BuildEvents()
    {
        // Events shared between standards (`ApprovalForAll(address,address,bool)`, etc.)
        // are declared once, under the standard that introduced them.
        // Parameter names follow the reference implementation (OpenZeppelin), not the underscored
        // names from the EIP texts, so that a guessed payload has the same keys as the payload
        // decoded with the actual ABI of a contract implementing the same standard.
        EventAbi[] items =
        [
            #region ERC-20
            new EventAbi
            {
                Name = "Transfer",
                Inputs = [
                    new() { Type = "address", Name = "from", Indexed = true },
                    new() { Type = "address", Name = "to", Indexed = true },
                    new() { Type = "uint256", Name = "value" }],
            },
            new EventAbi
            {
                Name = "Approval",
                Inputs = [
                    new() { Type = "address", Name = "owner", Indexed = true },
                    new() { Type = "address", Name = "spender", Indexed = true },
                    new() { Type = "uint256", Name = "value" }],
            },
            #endregion

            #region ERC-721
            new EventAbi
            {
                // same signature as ERC-20 `Transfer`, but with the third parameter indexed
                Name = "Transfer",
                Inputs = [
                    new() { Type = "address", Name = "from", Indexed = true },
                    new() { Type = "address", Name = "to", Indexed = true },
                    new() { Type = "uint256", Name = "tokenId", Indexed = true }],
            },
            new EventAbi
            {
                // same signature as ERC-20 `Approval`, but with the third parameter indexed
                Name = "Approval",
                Inputs = [
                    new() { Type = "address", Name = "owner", Indexed = true },
                    new() { Type = "address", Name = "approved", Indexed = true },
                    new() { Type = "uint256", Name = "tokenId", Indexed = true }],
            },
            new EventAbi
            {
                // also ERC-1155
                Name = "ApprovalForAll",
                Inputs = [
                    new() { Type = "address", Name = "owner", Indexed = true },
                    new() { Type = "address", Name = "operator", Indexed = true },
                    new() { Type = "bool", Name = "approved" }],
            },
            #endregion

            #region ERC-1155
            new EventAbi
            {
                Name = "TransferSingle",
                Inputs = [
                    new() { Type = "address", Name = "operator", Indexed = true },
                    new() { Type = "address", Name = "from", Indexed = true },
                    new() { Type = "address", Name = "to", Indexed = true },
                    new() { Type = "uint256", Name = "id" },
                    new() { Type = "uint256", Name = "value" }],
            },
            new EventAbi
            {
                Name = "TransferBatch",
                Inputs = [
                    new() { Type = "address", Name = "operator", Indexed = true },
                    new() { Type = "address", Name = "from", Indexed = true },
                    new() { Type = "address", Name = "to", Indexed = true },
                    new() { Type = "uint256[]", Name = "ids" },
                    new() { Type = "uint256[]", Name = "values" }],
            },
            new EventAbi
            {
                Name = "URI",
                Inputs = [
                    new() { Type = "string", Name = "value" },
                    new() { Type = "uint256", Name = "id", Indexed = true }],
            },
            #endregion

            #region ERC-4626
            new EventAbi
            {
                Name = "Deposit",
                Inputs = [
                    new() { Type = "address", Name = "sender", Indexed = true },
                    new() { Type = "address", Name = "owner", Indexed = true },
                    new() { Type = "uint256", Name = "assets" },
                    new() { Type = "uint256", Name = "shares" }],
            },
            new EventAbi
            {
                Name = "Withdraw",
                Inputs = [
                    new() { Type = "address", Name = "sender", Indexed = true },
                    new() { Type = "address", Name = "receiver", Indexed = true },
                    new() { Type = "address", Name = "owner", Indexed = true },
                    new() { Type = "uint256", Name = "assets" },
                    new() { Type = "uint256", Name = "shares" }],
            },
            #endregion

            #region ERC-5267
            new EventAbi
            {
                Name = "EIP712DomainChanged",
            },
            #endregion

            #region ownership and access control
            new EventAbi
            {
                Name = "OwnershipTransferred",
                Inputs = [
                    new() { Type = "address", Name = "previousOwner", Indexed = true },
                    new() { Type = "address", Name = "newOwner", Indexed = true }],
            },
            new EventAbi
            {
                Name = "OwnershipTransferStarted",
                Inputs = [
                    new() { Type = "address", Name = "previousOwner", Indexed = true },
                    new() { Type = "address", Name = "newOwner", Indexed = true }],
            },
            new EventAbi
            {
                Name = "RoleAdminChanged",
                Inputs = [
                    new() { Type = "bytes32", Name = "role", Indexed = true },
                    new() { Type = "bytes32", Name = "previousAdminRole", Indexed = true },
                    new() { Type = "bytes32", Name = "newAdminRole", Indexed = true }],
            },
            new EventAbi
            {
                Name = "RoleGranted",
                Inputs = [
                    new() { Type = "bytes32", Name = "role", Indexed = true },
                    new() { Type = "address", Name = "account", Indexed = true },
                    new() { Type = "address", Name = "sender", Indexed = true }],
            },
            new EventAbi
            {
                Name = "RoleRevoked",
                Inputs = [
                    new() { Type = "bytes32", Name = "role", Indexed = true },
                    new() { Type = "address", Name = "account", Indexed = true },
                    new() { Type = "address", Name = "sender", Indexed = true }],
            },
            new EventAbi
            {
                Name = "Paused",
                Inputs = [new() { Type = "address", Name = "account" }],
            },
            new EventAbi
            {
                Name = "Unpaused",
                Inputs = [new() { Type = "address", Name = "account" }],
            },
            #endregion

            #region proxies (ERC-1967)
            new EventAbi
            {
                Name = "Upgraded",
                Inputs = [new() { Type = "address", Name = "implementation", Indexed = true }],
            },
            new EventAbi
            {
                Name = "BeaconUpgraded",
                Inputs = [new() { Type = "address", Name = "beacon", Indexed = true }],
            },
            new EventAbi
            {
                Name = "AdminChanged",
                Inputs = [
                    new() { Type = "address", Name = "previousAdmin" },
                    new() { Type = "address", Name = "newAdmin" }],
            },
            new EventAbi
            {
                // OpenZeppelin Initializable v5
                Name = "Initialized",
                Inputs = [new() { Type = "uint64", Name = "version" }],
            },
            new EventAbi
            {
                // OpenZeppelin Initializable v4
                Name = "Initialized",
                Inputs = [new() { Type = "uint8", Name = "version" }],
            },
            #endregion
        ];

        static int IndexedCount(EventAbi e) => e.Inputs.Count(x => x.Indexed == true);
        static ulong Prefix(EventAbi e) => BinaryPrimitives.ReadUInt64BigEndian(e.TopicBytes);

        return items
            .DistinctBy(x => (x.Signature, IndexedCount(x)))
            .GroupBy(Prefix)
            .ToFrozenDictionary(g => g.Key, g => g.Select(x => (IndexedCount(x), x)).ToArray());
    }
}
