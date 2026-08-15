using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols;

/// <summary>
/// ABI of functions defined by popular EVM standards (ERC-20, ERC-721, ERC-1155, etc.),
/// used to guess entrypoint and parameters of calls to contracts with unknown ABI.
/// </summary>
public static class KnownSelectorsAbi
{
    /// <summary>
    /// Looks up a known function by the 4-byte selector prefix of the given calldata.
    /// </summary>
    public static bool TryGetFunction(ReadOnlySpan<byte> input, [NotNullWhen(true)] out FunctionAbi? function)
    {
        if (input.Length < 4)
        {
            function = null;
            return false;
        }

        return Functions.TryGetValue(BinaryPrimitives.ReadUInt32BigEndian(input), out function);
    }

    static readonly AbiParameter[] Call =
    [
        new() { Type = "address", Name = "target" },
        new() { Type = "bytes", Name = "callData" },
    ];

    static readonly AbiParameter[] Call3 =
    [
        new() { Type = "address", Name = "target" },
        new() { Type = "bool", Name = "allowFailure" },
        new() { Type = "bytes", Name = "callData" },
    ];

    static readonly AbiParameter[] Call3Value =
    [
        new() { Type = "address", Name = "target" },
        new() { Type = "bool", Name = "allowFailure" },
        new() { Type = "uint256", Name = "value" },
        new() { Type = "bytes", Name = "callData" },
    ];

    static readonly AbiParameter[] Result =
    [
        new() { Type = "bool", Name = "success" },
        new() { Type = "bytes", Name = "returnData" },
    ];

    static readonly FrozenDictionary<uint, FunctionAbi> Functions = BuildFunctions();

    static FrozenDictionary<uint, FunctionAbi> BuildFunctions()
    {
        // Functions shared between standards (`name()`, `transferFrom(address,address,uint256)`, etc.)
        // are declared once, under the standard that introduced them.
        // Input names follow the reference implementation (OpenZeppelin), not the underscored
        // names from the EIP texts, so that guessed parameters have the same keys as the parameters
        // decoded with the actual ABI of a contract implementing the same standard.
        // Output names are taken from the EIP texts, as the reference implementation leaves
        // return values unnamed.
        FunctionAbi[] items =
        [
            #region ERC-165
            new FunctionAbi
            {
                Name = "supportsInterface",
                Inputs = [new() { Type = "bytes4", Name = "interfaceId" }],
                Outputs = [new() { Type = "bool", Name = "supported" }],
                StateMutability = StateMutability.View,
            },
            #endregion

            #region ERC-20
            new FunctionAbi
            {
                Name = "name",
                Outputs = [new() { Type = "string", Name = "name" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "symbol",
                Outputs = [new() { Type = "string", Name = "symbol" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "decimals",
                Outputs = [new() { Type = "uint8", Name = "decimals" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "totalSupply",
                Outputs = [new() { Type = "uint256", Name = "totalSupply" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "balanceOf",
                Inputs = [new() { Type = "address", Name = "account" }],
                Outputs = [new() { Type = "uint256", Name = "balance" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "transfer",
                Inputs = [new() { Type = "address", Name = "to" }, new() { Type = "uint256", Name = "value" }],
                Outputs = [new() { Type = "bool", Name = "success" }],
            },
            new FunctionAbi
            {
                // also ERC-721 `transferFrom(address _from, address _to, uint256 _tokenId)`
                Name = "transferFrom",
                Inputs = [new() { Type = "address", Name = "from" }, new() { Type = "address", Name = "to" }, new() { Type = "uint256", Name = "value" }],
                Outputs = [new() { Type = "bool", Name = "success" }],
            },
            new FunctionAbi
            {
                // also ERC-721 `approve(address _approved, uint256 _tokenId)`
                Name = "approve",
                Inputs = [new() { Type = "address", Name = "spender" }, new() { Type = "uint256", Name = "value" }],
                Outputs = [new() { Type = "bool", Name = "success" }],
            },
            new FunctionAbi
            {
                Name = "allowance",
                Inputs = [new() { Type = "address", Name = "owner" }, new() { Type = "address", Name = "spender" }],
                Outputs = [new() { Type = "uint256", Name = "remaining" }],
                StateMutability = StateMutability.View,
            },
            #endregion

            #region ERC-20 extensions
            new FunctionAbi
            {
                Name = "mint",
                Inputs = [new() { Type = "address", Name = "to" }, new() { Type = "uint256", Name = "amount" }],
            },
            new FunctionAbi
            {
                Name = "burn",
                Inputs = [new() { Type = "uint256", Name = "amount" }],
            },
            new FunctionAbi
            {
                Name = "burnFrom",
                Inputs = [new() { Type = "address", Name = "account" }, new() { Type = "uint256", Name = "amount" }],
            },
            new FunctionAbi
            {
                Name = "increaseAllowance",
                Inputs = [new() { Type = "address", Name = "spender" }, new() { Type = "uint256", Name = "addedValue" }],
                Outputs = [new() { Type = "bool", Name = "success" }],
            },
            new FunctionAbi
            {
                Name = "decreaseAllowance",
                Inputs = [new() { Type = "address", Name = "spender" }, new() { Type = "uint256", Name = "subtractedValue" }],
                Outputs = [new() { Type = "bool", Name = "success" }],
            },
            #endregion

            #region ERC-2612
            new FunctionAbi
            {
                Name = "permit",
                Inputs = [
                    new() { Type = "address", Name = "owner" },
                    new() { Type = "address", Name = "spender" },
                    new() { Type = "uint256", Name = "value" },
                    new() { Type = "uint256", Name = "deadline" },
                    new() { Type = "uint8", Name = "v" },
                    new() { Type = "bytes32", Name = "r" },
                    new() { Type = "bytes32", Name = "s" }],
            },
            new FunctionAbi
            {
                Name = "nonces",
                Inputs = [new() { Type = "address", Name = "owner" }],
                Outputs = [new() { Type = "uint256", Name = "nonce" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "DOMAIN_SEPARATOR",
                Outputs = [new() { Type = "bytes32", Name = "domainSeparator" }],
                StateMutability = StateMutability.View,
            },
            #endregion

            #region ERC-721
            new FunctionAbi
            {
                Name = "ownerOf",
                Inputs = [new() { Type = "uint256", Name = "tokenId" }],
                Outputs = [new() { Type = "address", Name = "owner" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "safeTransferFrom",
                Inputs = [
                    new() { Type = "address", Name = "from" },
                    new() { Type = "address", Name = "to" },
                    new() { Type = "uint256", Name = "tokenId" },
                    new() { Type = "bytes", Name = "data" }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                Name = "safeTransferFrom",
                Inputs = [
                    new() { Type = "address", Name = "from" },
                    new() { Type = "address", Name = "to" },
                    new() { Type = "uint256", Name = "tokenId" }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                // also ERC-1155
                Name = "setApprovalForAll",
                Inputs = [new() { Type = "address", Name = "operator" }, new() { Type = "bool", Name = "approved" }],
            },
            new FunctionAbi
            {
                Name = "getApproved",
                Inputs = [new() { Type = "uint256", Name = "tokenId" }],
                Outputs = [new() { Type = "address", Name = "approved" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                // also ERC-1155
                Name = "isApprovedForAll",
                Inputs = [new() { Type = "address", Name = "owner" }, new() { Type = "address", Name = "operator" }],
                Outputs = [new() { Type = "bool", Name = "approved" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "tokenURI",
                Inputs = [new() { Type = "uint256", Name = "tokenId" }],
                Outputs = [new() { Type = "string", Name = "uri" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "tokenByIndex",
                Inputs = [new() { Type = "uint256", Name = "index" }],
                Outputs = [new() { Type = "uint256", Name = "tokenId" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "tokenOfOwnerByIndex",
                Inputs = [new() { Type = "address", Name = "owner" }, new() { Type = "uint256", Name = "index" }],
                Outputs = [new() { Type = "uint256", Name = "tokenId" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "onERC721Received",
                Inputs = [
                    new() { Type = "address", Name = "operator" },
                    new() { Type = "address", Name = "from" },
                    new() { Type = "uint256", Name = "tokenId" },
                    new() { Type = "bytes", Name = "data" }],
                Outputs = [new() { Type = "bytes4", Name = "magicValue" }],
            },
            #endregion

            #region ERC-1155
            new FunctionAbi
            {
                Name = "safeTransferFrom",
                Inputs = [
                    new() { Type = "address", Name = "from" },
                    new() { Type = "address", Name = "to" },
                    new() { Type = "uint256", Name = "id" },
                    new() { Type = "uint256", Name = "value" },
                    new() { Type = "bytes", Name = "data" }],
            },
            new FunctionAbi
            {
                Name = "safeBatchTransferFrom",
                Inputs = [
                    new() { Type = "address", Name = "from" },
                    new() { Type = "address", Name = "to" },
                    new() { Type = "uint256[]", Name = "ids" },
                    new() { Type = "uint256[]", Name = "values" },
                    new() { Type = "bytes", Name = "data" }],
            },
            new FunctionAbi
            {
                Name = "balanceOf",
                Inputs = [new() { Type = "address", Name = "account" }, new() { Type = "uint256", Name = "id" }],
                Outputs = [new() { Type = "uint256", Name = "balance" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "balanceOfBatch",
                Inputs = [new() { Type = "address[]", Name = "accounts" }, new() { Type = "uint256[]", Name = "ids" }],
                Outputs = [new() { Type = "uint256[]", Name = "balances" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "uri",
                Inputs = [new() { Type = "uint256", Name = "id" }],
                Outputs = [new() { Type = "string", Name = "uri" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "onERC1155Received",
                Inputs = [
                    new() { Type = "address", Name = "operator" },
                    new() { Type = "address", Name = "from" },
                    new() { Type = "uint256", Name = "id" },
                    new() { Type = "uint256", Name = "value" },
                    new() { Type = "bytes", Name = "data" }],
                Outputs = [new() { Type = "bytes4", Name = "magicValue" }],
            },
            new FunctionAbi
            {
                Name = "onERC1155BatchReceived",
                Inputs = [
                    new() { Type = "address", Name = "operator" },
                    new() { Type = "address", Name = "from" },
                    new() { Type = "uint256[]", Name = "ids" },
                    new() { Type = "uint256[]", Name = "values" },
                    new() { Type = "bytes", Name = "data" }],
                Outputs = [new() { Type = "bytes4", Name = "magicValue" }],
            },
            #endregion

            #region ERC-1271
            new FunctionAbi
            {
                Name = "isValidSignature",
                Inputs = [new() { Type = "bytes32", Name = "hash" }, new() { Type = "bytes", Name = "signature" }],
                Outputs = [new() { Type = "bytes4", Name = "magicValue" }],
                StateMutability = StateMutability.View,
            },
            #endregion

            #region ERC-2981
            new FunctionAbi
            {
                Name = "royaltyInfo",
                Inputs = [new() { Type = "uint256", Name = "tokenId" }, new() { Type = "uint256", Name = "salePrice" }],
                Outputs = [new() { Type = "address", Name = "receiver" }, new() { Type = "uint256", Name = "royaltyAmount" }],
                StateMutability = StateMutability.View,
            },
            #endregion

            #region ERC-4626
            new FunctionAbi
            {
                Name = "asset",
                Outputs = [new() { Type = "address", Name = "assetTokenAddress" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "totalAssets",
                Outputs = [new() { Type = "uint256", Name = "totalManagedAssets" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "convertToShares",
                Inputs = [new() { Type = "uint256", Name = "assets" }],
                Outputs = [new() { Type = "uint256", Name = "shares" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "convertToAssets",
                Inputs = [new() { Type = "uint256", Name = "shares" }],
                Outputs = [new() { Type = "uint256", Name = "assets" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "previewDeposit",
                Inputs = [new() { Type = "uint256", Name = "assets" }],
                Outputs = [new() { Type = "uint256", Name = "shares" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "deposit",
                Inputs = [new() { Type = "uint256", Name = "assets" }, new() { Type = "address", Name = "receiver" }],
                Outputs = [new() { Type = "uint256", Name = "shares" }],
            },
            new FunctionAbi
            {
                Name = "previewMint",
                Inputs = [new() { Type = "uint256", Name = "shares" }],
                Outputs = [new() { Type = "uint256", Name = "assets" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "mint",
                Inputs = [new() { Type = "uint256", Name = "shares" }, new() { Type = "address", Name = "receiver" }],
                Outputs = [new() { Type = "uint256", Name = "assets" }],
            },
            new FunctionAbi
            {
                Name = "previewWithdraw",
                Inputs = [new() { Type = "uint256", Name = "assets" }],
                Outputs = [new() { Type = "uint256", Name = "shares" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "withdraw",
                Inputs = [
                    new() { Type = "uint256", Name = "assets" },
                    new() { Type = "address", Name = "receiver" },
                    new() { Type = "address", Name = "owner" }],
                Outputs = [new() { Type = "uint256", Name = "shares" }],
            },
            new FunctionAbi
            {
                Name = "previewRedeem",
                Inputs = [new() { Type = "uint256", Name = "shares" }],
                Outputs = [new() { Type = "uint256", Name = "assets" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "redeem",
                Inputs = [
                    new() { Type = "uint256", Name = "shares" },
                    new() { Type = "address", Name = "receiver" },
                    new() { Type = "address", Name = "owner" }],
                Outputs = [new() { Type = "uint256", Name = "assets" }],
            },
            #endregion

            #region ownership and access control
            new FunctionAbi
            {
                Name = "owner",
                Outputs = [new() { Type = "address", Name = "owner" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "transferOwnership",
                Inputs = [new() { Type = "address", Name = "newOwner" }],
            },
            new FunctionAbi
            {
                Name = "renounceOwnership",
            },
            new FunctionAbi
            {
                Name = "hasRole",
                Inputs = [new() { Type = "bytes32", Name = "role" }, new() { Type = "address", Name = "account" }],
                Outputs = [new() { Type = "bool", Name = "granted" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "getRoleAdmin",
                Inputs = [new() { Type = "bytes32", Name = "role" }],
                Outputs = [new() { Type = "bytes32", Name = "adminRole" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "grantRole",
                Inputs = [new() { Type = "bytes32", Name = "role" }, new() { Type = "address", Name = "account" }],
            },
            new FunctionAbi
            {
                Name = "revokeRole",
                Inputs = [new() { Type = "bytes32", Name = "role" }, new() { Type = "address", Name = "account" }],
            },
            new FunctionAbi
            {
                Name = "renounceRole",
                Inputs = [new() { Type = "bytes32", Name = "role" }, new() { Type = "address", Name = "callerConfirmation" }],
            },
            new FunctionAbi
            {
                Name = "paused",
                Outputs = [new() { Type = "bool", Name = "paused" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "pause",
            },
            new FunctionAbi
            {
                Name = "unpause",
            },
            #endregion

            #region proxies (ERC-1822, ERC-1967)
            new FunctionAbi
            {
                Name = "implementation",
                Outputs = [new() { Type = "address", Name = "implementation" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "proxiableUUID",
                Outputs = [new() { Type = "bytes32", Name = "uuid" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "admin",
                Outputs = [new() { Type = "address", Name = "admin" }],
                StateMutability = StateMutability.View,
            },
            new FunctionAbi
            {
                Name = "changeAdmin",
                Inputs = [new() { Type = "address", Name = "newAdmin" }],
            },
            new FunctionAbi
            {
                Name = "upgradeTo",
                Inputs = [new() { Type = "address", Name = "newImplementation" }],
            },
            new FunctionAbi
            {
                Name = "upgradeToAndCall",
                Inputs = [new() { Type = "address", Name = "newImplementation" }, new() { Type = "bytes", Name = "data" }],
                StateMutability = StateMutability.Payable,
            },
            #endregion

            #region multicall
            new FunctionAbi
            {
                Name = "multicall",
                Inputs = [new() { Type = "bytes[]", Name = "data" }],
                Outputs = [new() { Type = "bytes[]", Name = "results" }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                Name = "aggregate",
                Inputs = [new() { Type = "tuple[]", Name = "calls", Components = Call }],
                Outputs = [new() { Type = "uint256", Name = "blockNumber" }, new() { Type = "bytes[]", Name = "returnData" }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                Name = "tryAggregate",
                Inputs = [
                    new() { Type = "bool", Name = "requireSuccess" },
                    new() { Type = "tuple[]", Name = "calls", Components = Call }],
                Outputs = [new() { Type = "tuple[]", Name = "returnData", Components = Result }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                Name = "aggregate3",
                Inputs = [new() { Type = "tuple[]", Name = "calls", Components = Call3 }],
                Outputs = [new() { Type = "tuple[]", Name = "returnData", Components = Result }],
                StateMutability = StateMutability.Payable,
            },
            new FunctionAbi
            {
                Name = "aggregate3Value",
                Inputs = [new() { Type = "tuple[]", Name = "calls", Components = Call3Value }],
                Outputs = [new() { Type = "tuple[]", Name = "returnData", Components = Result }],
                StateMutability = StateMutability.Payable,
            },
            #endregion
        ];

        static uint Selector(FunctionAbi fn) => BinaryPrimitives.ReadUInt32BigEndian(fn.SelectorBytes);

        return items
            .DistinctBy(Selector)
            .ToFrozenDictionary(Selector);
    }
}
