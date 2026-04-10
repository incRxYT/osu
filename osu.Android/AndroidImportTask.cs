// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Net;
using Android.Provider;
using osu.Game.Database;

namespace osu.Android
{
    public class AndroidImportTask : ImportTask
    {
        private readonly ContentResolver contentResolver;
        private readonly Uri uri;

        private AndroidImportTask(Stream stream, string filename, ContentResolver contentResolver, Uri uri)
            : base(stream, filename)
        {
            this.contentResolver = contentResolver;
            this.uri = uri;
        }

        public override void DeleteFile()
        {
            contentResolver.Delete(uri, null, null);
        }

        public static async Task<AndroidImportTask?> Create(ContentResolver contentResolver, Uri uri)
        {
            string filename;
            long? fileSize = null;

            // Only request the two columns we actually need — avoids fetching all columns.
            // Cursor is disposed immediately after reading to release native resources.
            using (var cursor = contentResolver.Query(uri, new[] { IOpenableColumns.DisplayName, IOpenableColumns.Size }, null, null, null))
            {
                if (cursor == null || !cursor.MoveToFirst())
                    return null;

                filename = cursor.GetString(cursor.GetColumnIndex(IOpenableColumns.DisplayName))
                           ?? uri.Path ?? string.Empty;

                int sizeColumn = cursor.GetColumnIndex(IOpenableColumns.Size);
                if (!cursor.IsNull(sizeColumn))
                    fileSize = cursor.GetLong(sizeColumn);
            }

            // Pre-size the MemoryStream if we know the file size — avoids repeated
            // internal buffer reallocations during CopyToAsync for large beatmap files.
            var copy = fileSize.HasValue ? new MemoryStream((int)fileSize.Value) : new MemoryStream();

            using (var stream = contentResolver.OpenInputStream(uri))
            {
                if (stream == null)
                    return null;

                await stream.CopyToAsync(copy).ConfigureAwait(false);
            }

            return new AndroidImportTask(copy, filename, contentResolver, uri);
        }
    }
}
