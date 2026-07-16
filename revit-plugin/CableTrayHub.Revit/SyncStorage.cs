using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System.Text.Json;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Menyimpan pemetaan "jalur -> cable tray yang dipilih user" DI DALAM
    /// file Revit (Extensible Storage), sehingga PULL berikutnya tidak perlu
    /// select tray ulang. Kunci = route key ("Panel A→Panel B"),
    /// nilai = daftar UniqueId elemen cable tray.
    /// </summary>
    public static class SyncStorage
    {
        private static readonly Guid SchemaGuid = new("A3F8B2D1-6C4E-4A7B-9E0D-1F2A3B4C5D6E");
        private const string SchemaName = "CableTrayHubSync";
        private const string FieldName = "Json";
        private const string StorageName = "CableTrayHub_SyncStorage";

        private static Schema GetSchema()
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null) return schema;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(FieldName, typeof(string));
            return builder.Finish();
        }

        private static DataStorage FindStorage(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .FirstOrDefault(ds => ds.Name == StorageName);
        }

        /// <summary>Membaca seluruh pemetaan jalur -> tray UniqueIds.</summary>
        public static Dictionary<string, List<string>> Load(Document doc)
        {
            try
            {
                DataStorage storage = FindStorage(doc);
                if (storage == null) return new Dictionary<string, List<string>>();

                Entity entity = storage.GetEntity(GetSchema());
                if (entity == null || !entity.IsValid())
                    return new Dictionary<string, List<string>>();

                string json = entity.Get<string>(FieldName);
                if (string.IsNullOrEmpty(json))
                    return new Dictionary<string, List<string>>();

                return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                       ?? new Dictionary<string, List<string>>();
            }
            catch
            {
                return new Dictionary<string, List<string>>();
            }
        }

        /// <summary>Menyimpan pemetaan (panggil di dalam Transaction aktif).</summary>
        public static void Save(Document doc, Dictionary<string, List<string>> map)
        {
            DataStorage storage = FindStorage(doc);
            if (storage == null)
            {
                storage = DataStorage.Create(doc);
                storage.Name = StorageName;
            }

            var entity = new Entity(GetSchema());
            entity.Set(FieldName, JsonSerializer.Serialize(map));
            storage.SetEntity(entity);
        }
    }
}
