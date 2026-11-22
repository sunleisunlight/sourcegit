using System;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Calculate the total line statistics (insertions / deletions) of the working copy changes.
    /// </summary>
    public class QueryChangesLineStat : Command
    {
        public QueryChangesLineStat(string repo, bool staged)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = staged
                ? "diff --no-color --no-ext-diff --numstat --cached"
                : "diff --no-color --no-ext-diff --numstat";
            _staged = staged;
        }

        public bool Staged
        {
            get => _staged;
        }

        public async Task<(int Files, int Insertions, int Deletions)> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return (0, 0, 0);

            var files = 0;
            var insertions = 0;
            var deletions = 0;

            using var reader = new StringReader(rs.StdOut);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                    continue;

                var fields = line.Split('\t');
                if (fields.Length < 3)
                    continue;

                files++;

                // `-` means the file is binary or a submodule, which has no line based statistics.
                if (fields[0] == "-" || fields[1] == "-")
                    continue;

                if (int.TryParse(fields[0], out var added))
                    insertions += added;
                if (int.TryParse(fields[1], out var removed))
                    deletions += removed;
            }

            return (files, insertions, deletions);
        }

        private bool _staged = false;
    }
}
