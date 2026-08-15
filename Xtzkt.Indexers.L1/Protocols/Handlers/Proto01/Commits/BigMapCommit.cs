using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class BigMapCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public List<(BigMap, BigMapKey?, BigMapUpdate, IBigmapOperation)> Updates = [];

        readonly List<(IBigmapOperation op, L1Contract contract, BigMapDiff diff)> Diffs = [];
        readonly Dictionary<int, int> TempPtrs = new(7);
        int TempPtr = 0;

        public virtual void Append(IBigmapOperation op, L1Contract contract, IEnumerable<BigMapDiff> diffs)
        {
            foreach (var diff in diffs)
            {
                #region transform temp ptrs
                if (diff.Ptr < 0)
                {
                    if (diff.Action <= BigMapDiffAction.Copy)
                    {
                        TempPtrs[diff.Ptr] = --TempPtr;
                        diff.Ptr = TempPtr;
                    }
                    else
                    {
                        diff.Ptr = TempPtrs[diff.Ptr];
                    }
                }
                if (diff is CopyDiff copy && copy.SourcePtr < 0)
                {
                    copy.SourcePtr = TempPtrs[copy.SourcePtr];
                }
                #endregion
                Diffs.Add((op, contract, diff));
            }
        }

        public virtual async Task Apply()
        {
            if (Diffs.Count == 0) return;
            Context.Block.Events |= L1BlockEvents.Bigmaps;

            #region prefetch
            var allocated = new HashSet<int>(7);
            var copiedFrom = new HashSet<int>(7);

            foreach (var diff in Diffs.Where(x => x.diff.Ptr >= 0))
            {
                if (diff.diff.Action == BigMapDiffAction.Alloc)
                {
                    allocated.Add(diff.diff.Ptr);
                }
                else if (diff.diff is CopyDiff copy)
                {
                    var origin = GetOrigin(copy);
                    if (origin < 0)
                        allocated.Add(diff.diff.Ptr);
                    else
                        copiedFrom.Add(origin);
                }
            }

            await Cache.BigMaps.Prefetch(Diffs
                .Where(x => x.diff.Ptr >= 0 && !allocated.Contains(x.diff.Ptr))
                .Select(x => x.diff.Ptr));

            await Cache.BigMapKeys.Prefetch(Diffs
                .Where(x => x.diff.Ptr >= 0 && !allocated.Contains(x.diff.Ptr) && x.diff.Action == BigMapDiffAction.Update && Cache.BigMaps.HasCached(x.diff.Ptr))
                .Select(x => (Cache.BigMaps.Get(x.diff.Ptr).Id, (x.diff as UpdateDiff)!.KeyHash)));

            var copiedKeys = new List<BigMapKey>();
            if (copiedFrom.Count != 0)
            {
                await Cache.BigMaps.Prefetch(copiedFrom);

                var copiedFromIds = copiedFrom
                    .Select(ptr => Cache.BigMaps.Get(ptr).Id)
                    .ToHashSet();

                copiedKeys = await Db.BigMapKeys
                    .AsNoTracking()
                    .Where(x => copiedFromIds.Contains(x.BigMapId))
                    .ToListAsync();
            }
            #endregion

            BigMapUpdate bigMapUpdate;
            var bigMapUpdates = new List<BigMapUpdate>(Updates.Count);
            var images = new Dictionary<int, Dictionary<string, (byte[] RawKey, byte[] RawValue)>>();
            foreach (var diff in Diffs)
            {
                switch (diff.diff)
                {
                    case AllocDiff alloc:
                        if (alloc.Ptr >= 0)
                        {
                            #region allocate new
                            var script = await Cache.Schemas.GetAsync(diff.contract);
                            var storage = await Cache.Storages.GetAsync(diff.contract);
                            var storageView = script.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                            var bigMapNode = storageView.Nodes()
                                .FirstOrDefault(x => x.Schema.Prim == PrimType.big_map && x.Value is MichelineInt v && v.Value == alloc.Ptr);

                            if (bigMapNode == null)
                            {
                                storage = (Db.ChangeTracker.Entries()
                                    .First(x => x.Entity is Storage s && (s.OriginationId == diff.op.Id || s.TransactionId == diff.op.Id))
                                    .Entity as Storage)!;
                                storageView = script.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                                bigMapNode = storageView.Nodes()
                                    .FirstOrDefault(x => x.Schema.Prim == PrimType.big_map && x.Value is MichelineInt v && v.Value == alloc.Ptr)
                                        ?? throw new Exception($"Allocated big_map {alloc.Ptr} missed in the storage");
                            }
                            var bigMapSchema = (bigMapNode.Schema as BigMapSchema)!;
                            var allocatedBigMap = new BigMap
                            {
                                Id = Cache.Chain.NextBigMapId(),
                                ChainId = Cache.Chain.Get().Id,
                                Ptr = alloc.Ptr,
                                ContractId = diff.contract.Id,
                                StoragePath = bigMapNode.Path,
                                KeyType = bigMapSchema.Key.ToMicheline().ToBytes(),
                                ValueType = bigMapSchema.Value.ToMicheline().ToBytes(),
                                Active = true,
                                FirstLevel = diff.op.Level,
                                FirstTimestamp = diff.op.Timestamp,
                                LastLevel = diff.op.Level,
                                LastTimestamp = diff.op.Timestamp,
                                ActiveKeys = 0,
                                TotalKeys = 0,
                                Updates = 1,
                                Tags = GetTags(diff.contract, bigMapNode)
                            };
                            Db.BigMaps.Add(allocatedBigMap);
                            Cache.BigMaps.Add(allocatedBigMap);

                            bigMapUpdate = new BigMapUpdate
                            {
                                Id = Cache.Chain.NextBigMapUpdateId(),
                                ChainId = Cache.Chain.Get().Id,
                                Action = BigMapAction.Allocate,
                                BigMapId = allocatedBigMap.Id,
                                Level = diff.op.Level,
                                Timestamp = diff.op.Timestamp,
                                TransactionId = (diff.op as TransactionOperation)?.Id,
                                OriginationId = (diff.op as OriginationOperation)?.Id
                            };
                            //Db.BigMapUpdates.Add(bigMapUpdate);
                            bigMapUpdates.Add(bigMapUpdate);
                            Updates.Add((allocatedBigMap, null, bigMapUpdate, diff.op));
                            diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + 1;

                            images.Add(alloc.Ptr, []);
                            #endregion
                        }
                        else
                        {
                            #region alloc temp
                            images.Add(alloc.Ptr, []);
                            #endregion
                        }
                        break;
                    case CopyDiff copy:
                        if (copy.SourcePtr >= 0 && !copiedFrom.Contains(copy.SourcePtr))
                            break;
                        if (!images.TryGetValue(copy.SourcePtr, out var src))
                        {
                            var sourceId = Cache.BigMaps.Get(copy.SourcePtr).Id;
                            src = copiedKeys
                                .Where(x => x.BigMapId == sourceId)
                                .ToDictionary(x => x.KeyHash, x => (x.RawKey, x.RawValue));
                        }
                        if (copy.Ptr >= 0)
                        {
                            #region copy to new
                            var script = await Cache.Schemas.GetAsync(diff.contract);
                            var storage = await Cache.Storages.GetAsync(diff.contract);
                            var storageView = script.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                            var bigMapNode = storageView.Nodes()
                                .FirstOrDefault(x => x.Schema.Prim == PrimType.big_map && x.Value is MichelineInt v && v.Value == copy.Ptr);

                            if (bigMapNode == null)
                            {
                                storage = (Db.ChangeTracker.Entries()
                                    .First(x => x.Entity is Storage s && (s.OriginationId == diff.op.Id || s.TransactionId == diff.op.Id))
                                    .Entity as Storage)!;
                                storageView = script.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                                bigMapNode = storageView.Nodes()
                                    .FirstOrDefault(x => x.Schema.Prim == PrimType.big_map && x.Value is MichelineInt v && v.Value == copy.Ptr)
                                        ?? throw new Exception($"Copied big_map {copy.Ptr} missed in the storage");
                            }

                            var bigMapSchema = (bigMapNode.Schema as BigMapSchema)!;

                            var copiedBigMap = new BigMap
                            {
                                Id = Cache.Chain.NextBigMapId(),
                                ChainId = Cache.Chain.Get().Id,
                                Ptr = copy.Ptr,
                                ContractId = diff.contract.Id,
                                StoragePath = bigMapNode.Path,
                                KeyType = bigMapSchema.Key.ToMicheline().ToBytes(),
                                ValueType = bigMapSchema.Value.ToMicheline().ToBytes(),
                                Active = true,
                                FirstLevel = diff.op.Level,
                                FirstTimestamp = diff.op.Timestamp,
                                LastLevel = diff.op.Level,
                                LastTimestamp = diff.op.Timestamp,
                                ActiveKeys = 0,
                                TotalKeys = 0,
                                Updates = 1,
                                Tags = GetTags(diff.contract, bigMapNode)
                            };
                            Db.BigMaps.Add(copiedBigMap);
                            Cache.BigMaps.Add(copiedBigMap);

                            var keys = src.Select(kv =>
                            {
                                var rawKey = Micheline.FromBytes(kv.Value.RawKey);
                                var rawValue = Micheline.FromBytes(kv.Value.RawValue);
                                return new BigMapKey
                                {
                                    Id = Cache.Chain.NextBigMapKeyId(),
                                    ChainId = Cache.Chain.Get().Id,
                                    BigMapId = copiedBigMap.Id,
                                    Active = true,
                                    KeyHash = kv.Key,
                                    JsonKey = Regexes.RestrictedUnicode().Replace(bigMapSchema.Key.Humanize(rawKey), Regexes.NullEscapeString),
                                    JsonValue = Regexes.RestrictedUnicode().Replace(bigMapSchema.Value.Humanize(rawValue), Regexes.NullEscapeString),
                                    RawKey = bigMapSchema.Key.Optimize(rawKey).ToBytes(),
                                    RawValue = bigMapSchema.Value.Optimize(rawValue).ToBytes(),
                                    FirstLevel = diff.op.Level,
                                    FirstTimestamp = diff.op.Timestamp,
                                    LastLevel = diff.op.Level,
                                    LastTimestamp = diff.op.Timestamp,
                                    Updates = 1
                                };
                            }).ToList();

                            copiedBigMap.ActiveKeys += keys.Count;
                            copiedBigMap.TotalKeys += keys.Count;
                            copiedBigMap.Updates += keys.Count;

                            Db.BigMapKeys.AddRange(keys);
                            Cache.BigMapKeys.Add(keys);

                            bigMapUpdate = new BigMapUpdate
                            {
                                Id = Cache.Chain.NextBigMapUpdateId(),
                                ChainId = Cache.Chain.Get().Id,
                                Action = BigMapAction.Allocate,
                                BigMapId = copiedBigMap.Id,
                                Level = diff.op.Level,
                                Timestamp = diff.op.Timestamp,
                                TransactionId = (diff.op as TransactionOperation)?.Id,
                                OriginationId = (diff.op as OriginationOperation)?.Id
                            };
                            //Db.BigMapUpdates.Add(bigMapUpdate);
                            bigMapUpdates.Add(bigMapUpdate);
                            Updates.Add((copiedBigMap, null, bigMapUpdate, diff.op));

                            foreach (var key in keys)
                            {
                                bigMapUpdate = new BigMapUpdate
                                {
                                    Id = Cache.Chain.NextBigMapUpdateId(),
                                    ChainId = Cache.Chain.Get().Id,
                                    Action = BigMapAction.AddKey,
                                    BigMapKeyId = key.Id,
                                    BigMapId = key.BigMapId,
                                    JsonValue = key.JsonValue,
                                    RawValue = key.RawValue,
                                    Level = diff.op.Level,
                                    Timestamp = diff.op.Timestamp,
                                    TransactionId = (diff.op as TransactionOperation)?.Id,
                                    OriginationId = (diff.op as OriginationOperation)?.Id
                                };
                                //Db.BigMapUpdates.Add(bigMapUpdate);
                                bigMapUpdates.Add(bigMapUpdate);
                                Updates.Add((copiedBigMap, key, bigMapUpdate, diff.op));
                            }
                            diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + keys.Count + 1;

                            images.Add(copy.Ptr, keys.ToDictionary(x => x.KeyHash, x => (x.RawKey, x.RawValue)));
                            #endregion
                        }
                        else
                        {
                            #region copy to temp
                            images.Add(copy.Ptr, src.ToDictionary(x => x.Key, x => x.Value));
                            #endregion
                        }
                        break;
                    case UpdateDiff update:
                        if (update.Ptr >= 0)
                        {
                            var bigMap = Cache.BigMaps.Get(update.Ptr);

                            if (Cache.BigMapKeys.TryGet(bigMap.Id, update.KeyHash, out var key))
                            {
                                if (update.Value != null)
                                {
                                    #region update key
                                    Db.TryAttach(bigMap);
                                    bigMap.LastLevel = diff.op.Level;
                                    bigMap.LastTimestamp = diff.op.Timestamp;
                                    if (!key.Active) bigMap.ActiveKeys++;
                                    bigMap.Updates++;

                                    Db.TryAttach(key);
                                    key.Active = true;
                                    key.JsonValue = Regexes.RestrictedUnicode().Replace(bigMap.Schema.Value.Humanize(update.Value), Regexes.NullEscapeString);
                                    key.RawValue = bigMap.Schema.Value.Optimize(update.Value).ToBytes();
                                    key.LastLevel = diff.op.Level;
                                    key.LastTimestamp = diff.op.Timestamp;
                                    key.Updates++;

                                    bigMapUpdate = new BigMapUpdate
                                    {
                                        Id = Cache.Chain.NextBigMapUpdateId(),
                                        ChainId = Cache.Chain.Get().Id,
                                        Action = BigMapAction.UpdateKey,
                                        BigMapKeyId = key.Id,
                                        BigMapId = key.BigMapId,
                                        JsonValue = key.JsonValue,
                                        RawValue = key.RawValue,
                                        Level = diff.op.Level,
                                        Timestamp = diff.op.Timestamp,
                                        TransactionId = (diff.op as TransactionOperation)?.Id,
                                        OriginationId = (diff.op as OriginationOperation)?.Id
                                    };
                                    //Db.BigMapUpdates.Add(bigMapUpdate);
                                    bigMapUpdates.Add(bigMapUpdate);
                                    Updates.Add((bigMap, key, bigMapUpdate, diff.op));
                                    diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + 1;
                                    #endregion
                                }
                                else if (key.Active) // WTF: edo2net:76611 - key was removed twice
                                {
                                    #region remove key
                                    Db.TryAttach(bigMap);
                                    bigMap.LastLevel = diff.op.Level;
                                    bigMap.LastTimestamp = diff.op.Timestamp;
                                    bigMap.ActiveKeys--;
                                    bigMap.Updates++;

                                    Db.TryAttach(key);
                                    key.Active = false;
                                    key.LastLevel = diff.op.Level;
                                    key.LastTimestamp = diff.op.Timestamp;
                                    key.Updates++;

                                    bigMapUpdate = new BigMapUpdate
                                    {
                                        Id = Cache.Chain.NextBigMapUpdateId(),
                                        ChainId = Cache.Chain.Get().Id,
                                        Action = BigMapAction.RemoveKey,
                                        BigMapKeyId = key.Id,
                                        BigMapId = key.BigMapId,
                                        JsonValue = key.JsonValue,
                                        RawValue = key.RawValue,
                                        Level = diff.op.Level,
                                        Timestamp = diff.op.Timestamp,
                                        TransactionId = (diff.op as TransactionOperation)?.Id,
                                        OriginationId = (diff.op as OriginationOperation)?.Id
                                    };
                                    //Db.BigMapUpdates.Add(bigMapUpdate);
                                    bigMapUpdates.Add(bigMapUpdate);
                                    Updates.Add((bigMap, key, bigMapUpdate, diff.op));
                                    diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + 1;
                                    #endregion
                                }
                            }
                            else if (update.Value != null) // WTF: edo2net:34839 - non-existent key was removed
                            {
                                #region add key
                                Db.TryAttach(bigMap);
                                bigMap.LastLevel = diff.op.Level;
                                bigMap.LastTimestamp = diff.op.Timestamp;
                                bigMap.ActiveKeys++;
                                bigMap.TotalKeys++;
                                bigMap.Updates++;

                                key = new BigMapKey
                                {
                                    Id = Cache.Chain.NextBigMapKeyId(),
                                    ChainId = Cache.Chain.Get().Id,
                                    Active = true,
                                    BigMapId = bigMap.Id,
                                    FirstLevel = diff.op.Level,
                                    FirstTimestamp = diff.op.Timestamp,
                                    LastLevel = diff.op.Level,
                                    LastTimestamp = diff.op.Timestamp,
                                    JsonKey = Regexes.RestrictedUnicode().Replace(bigMap.Schema.Key.Humanize(update.Key), Regexes.NullEscapeString),
                                    JsonValue = Regexes.RestrictedUnicode().Replace(bigMap.Schema.Value.Humanize(update.Value), Regexes.NullEscapeString),
                                    RawKey = bigMap.Schema.Key.Optimize(update.Key).ToBytes(),
                                    RawValue = bigMap.Schema.Value.Optimize(update.Value).ToBytes(),
                                    KeyHash = update.KeyHash,
                                    Updates = 1
                                };

                                Db.BigMapKeys.Add(key);
                                Cache.BigMapKeys.Add(key);

                                bigMapUpdate = new BigMapUpdate
                                {
                                    Id = Cache.Chain.NextBigMapUpdateId(),
                                    ChainId = Cache.Chain.Get().Id,
                                    Action = BigMapAction.AddKey,
                                    BigMapKeyId = key.Id,
                                    BigMapId = key.BigMapId,
                                    JsonValue = key.JsonValue,
                                    RawValue = key.RawValue,
                                    Level = diff.op.Level,
                                    Timestamp = diff.op.Timestamp,
                                    TransactionId = (diff.op as TransactionOperation)?.Id,
                                    OriginationId = (diff.op as OriginationOperation)?.Id
                                };
                                //Db.BigMapUpdates.Add(bigMapUpdate);
                                bigMapUpdates.Add(bigMapUpdate);
                                Updates.Add((bigMap, key, bigMapUpdate, diff.op));
                                diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + 1;
                                #endregion
                            }
                        }
                        else
                        {
                            #region update temp
                            if (!images.TryGetValue(update.Ptr, out var image))
                                throw new Exception("Can't update non-existent temporary big_map");

                            if (image.TryGetValue(update.KeyHash, out var key))
                            {
                                if (update.Value != null)
                                {
                                    image[update.KeyHash] = (key.RawKey, update.Value.ToBytes());
                                }
                                else
                                {
                                    image.Remove(update.KeyHash);
                                }
                            }
                            else if (update.Value != null) // WTF: edo2net:34839 - non-existent key was removed
                            {
                                image.Add(update.KeyHash, (update.Key.ToBytes(), update.Value.ToBytes()));
                            }
                            #endregion
                        }
                        break;
                    case RemoveDiff remove:
                        if (remove.Ptr >= 0)
                        {
                            var removedBigMap = Cache.BigMaps.Get(remove.Ptr);

                            Db.TryAttach(removedBigMap);
                            removedBigMap.Active = false;
                            removedBigMap.LastLevel = diff.op.Level;
                            removedBigMap.LastTimestamp = diff.op.Timestamp;
                            removedBigMap.Updates++;

                            bigMapUpdate = new BigMapUpdate
                            {
                                Id = Cache.Chain.NextBigMapUpdateId(),
                                ChainId = Cache.Chain.Get().Id,
                                Action = BigMapAction.Remove,
                                BigMapId = removedBigMap.Id,
                                Level = diff.op.Level,
                                Timestamp = diff.op.Timestamp,
                                TransactionId = (diff.op as TransactionOperation)?.Id,
                                OriginationId = (diff.op as OriginationOperation)?.Id
                            };
                            //Db.BigMapUpdates.Add(bigMapUpdate);
                            bigMapUpdates.Add(bigMapUpdate);
                            Updates.Add((removedBigMap, null, bigMapUpdate, diff.op));
                            diff.op.BigMapUpdates = (diff.op.BigMapUpdates ?? 0) + 1;
                        }
                        break;
                    default:
                        break;
                }
            }
            if (bigMapUpdates.Count != 0)
                BigMapUpdate.Write((Db.Database.GetDbConnection() as NpgsqlConnection)!, bigMapUpdates);
        }

        int GetOrigin(CopyDiff copy)
        {
            return Diffs
                .FirstOrDefault(x => x.diff.Action == BigMapDiffAction.Copy && x.diff.Ptr == copy.SourcePtr).diff is CopyDiff prevCopy
                    ? GetOrigin(prevCopy)
                    : copy.SourcePtr;
        }

        protected virtual BigMapTag GetTags(L1Contract contract, TreeView node) => BigMaps.GetTags(contract, node);

        public virtual async Task Revert(L1Block block)
        {
            if (block.Events.HasFlag(L1BlockEvents.Bigmaps))
            {
                var bigmaps = await Db.BigMaps.Where(x => x.ChainId == block.ChainId && x.LastLevel == block.Level).ToListAsync();
                var keys = await Db.BigMapKeys.Where(x => x.ChainId == block.ChainId && x.LastLevel == block.Level).ToListAsync();
                var updates = await Db.BigMapUpdates
                    .AsNoTracking()
                    .Where(x => x.ChainId == block.ChainId && x.Level == block.Level)
                    .Select(x => new
                    {
                        x.BigMapId,
                        x.BigMapKeyId
                    })
                    .ToListAsync();

                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "BigMapUpdates"
                    WHERE "ChainId" = {0}
                    AND "Level" = {1}
                    """, block.ChainId, block.Level);
                Cache.Chain.ReleaseBigMapUpdateId(updates.Count);

                var updatesCountByKey = updates
                    .Where(x => x.BigMapKeyId != null)
                    .GroupBy(x => x.BigMapKeyId!.Value)
                    .ToDictionary(x => x.Key, x => x.Count());

                var updatesCountByBigMap = updates
                    .GroupBy(x => x.BigMapId)
                    .ToDictionary(x => x.Key, x => x.Count());

                foreach (var key in keys)
                {
                    var bigmap = bigmaps.First(x => x.Id == key.BigMapId);
                    Cache.BigMaps.Add(bigmap);
                    Cache.BigMapKeys.Add(key);

                    if (key.FirstLevel == block.Level)
                    {
                        if (key.Active) bigmap.ActiveKeys--;
                        bigmap.TotalKeys--;
                        Db.BigMapKeys.Remove(key);
                        Cache.BigMapKeys.Remove(key);
                        Cache.Chain.ReleaseBigMapKeyId();
                    }
                    else
                    {
                        var prevUpdate = await Db.BigMapUpdates
                            .Where(x => x.BigMapKeyId == key.Id)
                            .OrderByDescending(x => x.Id)
                            .FirstAsync();

                        var prevActive = prevUpdate.Action != BigMapAction.RemoveKey;
                        if (key.Active && !prevActive)
                            bigmap.ActiveKeys--;
                        else if (!key.Active && prevActive)
                            bigmap.ActiveKeys++;

                        key.Active = prevActive;
                        key.JsonValue = prevUpdate.JsonValue!;
                        key.RawValue = prevUpdate.RawValue!;
                        key.LastLevel = prevUpdate.Level;
                        key.LastTimestamp = prevUpdate.Timestamp;
                        key.Updates -= updatesCountByKey.GetValueOrDefault(key.Id);
                    }
                }

                foreach (var bigmap in bigmaps)
                {
                    Cache.BigMaps.Add(bigmap);
                    if (bigmap.FirstLevel == block.Level)
                    {
                        Db.BigMaps.Remove(bigmap);
                        Cache.BigMaps.Remove(bigmap);
                        Cache.Chain.ReleaseBigMapId();
                    }
                    else
                    {
                        bigmap.Active = true;
                        bigmap.Updates -= updatesCountByBigMap.GetValueOrDefault(bigmap.Id);
                        if (bigmap.Updates > 1)
                        {
                            var lastUpdate = await Db.BigMapUpdates
                                .Where(x => x.BigMapId == bigmap.Id)
                                .OrderByDescending(x => x.Id)
                                .FirstAsync();

                            bigmap.LastLevel = lastUpdate.Level;
                            bigmap.LastTimestamp = lastUpdate.Timestamp;
                        }
                        else
                        {
                            bigmap.LastLevel = bigmap.FirstLevel;
                            bigmap.LastTimestamp = bigmap.FirstTimestamp;
                        }
                    }
                }
            }
        }
    }
}
