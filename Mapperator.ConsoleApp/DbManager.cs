﻿using System;
using OsuParsers.Database.Objects;
using OsuParsers.Decoders;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapperator.ConsoleApp.Exceptions;

namespace Mapperator.ConsoleApp {
    public static class DbManager {
        public static IEnumerable<DbBeatmap> GetCollection(string collectionName) {
            var osuDbPath = Path.Join(ConfigManager.Config.OsuPath, "osu!.db");
            var collectionPath = Path.Join(ConfigManager.Config.OsuPath, "collection.db");

            var db = DatabaseDecoder.DecodeOsu(osuDbPath);
            var collections = DatabaseDecoder.DecodeCollection(collectionPath);
            var beatmaps = db.Beatmaps;
            var collection = collections.Collections.FirstOrDefault(o => o.Name == collectionName);

            if (collection is null) {
                throw new CollectionNotFoundException(collectionName);
            }

            return collection.MD5Hashes.SelectMany(o => beatmaps.Where(b => b.MD5Hash == o));
        }

        public static List<DbBeatmap> GetAll() {
            var osuDbPath = Path.Join(ConfigManager.Config.OsuPath, "osu!.db");
            var db = DatabaseDecoder.DecodeOsu(osuDbPath);
            return db.Beatmaps;
        }
    }
}
