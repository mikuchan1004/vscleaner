using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSCleaner.Models
{
    public class FolderItem
    {
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        // 포맷팅된 용량 (기존 FormatSize 로직 활용)
        public string FormattedSize => FormatSize(SizeBytes);

        private static string FormatSize(long bytes)
        {
            string[] Suffix = ["B", "KB", "MB", "GB", "TB"];
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }
            return $"{dblSByte:0.##} {Suffix[i]}";
        }
    }
}
