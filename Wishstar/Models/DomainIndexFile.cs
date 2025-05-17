namespace Wishstar.Models {
    public static class DomainIndexFileParser {
        public static DomainIndex[] Parse(string[] lines) {
            List<DomainIndex> indices = [];
            foreach (string index in lines) {
                if (!index.Contains(':') || !index.Contains("->")) {
                    continue;
                }

                try {
                    string[] parts = index.Split(':');
                    string domain = parts[0].Trim();
                    string[] fileParts = parts[1].Split("->");
                    string virtualFileName = fileParts[0].Trim();
                    string physicalFileName = fileParts[1].Trim();

                    indices.Add(new DomainIndex {
                        Domain = domain,
                        VirtualFileName = virtualFileName,
                        PhysicalFileName = physicalFileName
                    });
                } catch (Exception) {
                    continue;
                }
            }

            return [.. indices];
        }

        public static string Serialize(DomainIndex[] indices) {
            List<string> lines = [];
            foreach (DomainIndex index in indices) {
                lines.Add($"{index.Domain}: {index.VirtualFileName} -> {index.PhysicalFileName}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public class DomainIndex {
        public required string Domain { get; init; }
        public required string VirtualFileName { get; init; }
        public required string PhysicalFileName { get; init; }
    }

    public class DomainIndexFile {
        public IReadOnlyList<DomainIndex> Indices = [];
        private readonly List<DomainIndex> _Indices = [];
        private readonly string _FilePath;

        private DomainIndexFile(string filePath) {
            _FilePath = filePath;
            Indices = DomainIndexFileParser.Parse(File.ReadAllLines(filePath));
        }

        public static DomainIndexFile Load(string filePath) {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException(filePath);
            }

            return new DomainIndexFile(filePath);
        }

        public void Add(string domain, string virtualFileName, string physicalFileName) {
            _Indices.Add(new DomainIndex {
                Domain = domain,
                VirtualFileName = virtualFileName,
                PhysicalFileName = physicalFileName
            });

            File.WriteAllText(_FilePath, DomainIndexFileParser.Serialize([.. _Indices]));
        }

        public void Remove(DomainIndex index) {
            _Indices.Remove(index);
            File.WriteAllText(_FilePath, DomainIndexFileParser.Serialize([.. _Indices]));
        }
    }
}