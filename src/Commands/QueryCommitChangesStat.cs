using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Calculate the changes statistics (changed files / insertions / deletions) of commits.
    ///     The result of each commit is reported by a callback as soon as it has been parsed, so the caller
    ///     can update the UI progressively instead of waiting for the whole history to be walked.
    /// </summary>
    public class QueryCommitChangesStat : Command
    {
        private const char HEADER_PREFIX = '\u0001';

        public QueryCommitChangesStat(string repo, string limits)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --no-show-signature --numstat --format=%x01%H%x00%P {limits}";
        }

        /// <summary>
        ///     Run the query.
        /// </summary>
        /// <param name="onCommit">Called (in the background thread) with SHA, changed files, insertions and deletions.</param>
        /// <param name="maxCommits">Stop after this count of commits have been reported. `0` means no limit.</param>
        /// <param name="token">Used to cancel the running git process.</param>
        public async Task RunAsync(Action<string, int, int, int> onCommit, int maxCommits, CancellationToken token)
        {
            CancellationToken = token;

            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                RaiseException(App.Text("Notification.QueryChangesStatFailed", e.Message));
                return;
            }

            // Make sure the process will be killed when the query is cancelled (refreshing histories for example).
            using var registration = token.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        Native.OS.TerminateProcess(proc);
                }
                catch
                {
                    // Ignored.
                }
            });

            var sha = string.Empty;
            var isMerge = false;
            var files = 0;
            var insertions = 0;
            var deletions = 0;
            var reported = 0;

            void Report()
            {
                if (string.IsNullOrEmpty(sha) || isMerge)
                    return;

                onCommit(sha, files, insertions, deletions);
                reported++;
            }

            try
            {
                while (await proc.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    if (line.Length == 0)
                        continue;

                    if (line[0] == HEADER_PREFIX)
                    {
                        Report();

                        var parts = line.Split('\0');
                        sha = parts[0][1..];
                        isMerge = parts.Length > 1 && parts[1].IndexOf(' ') >= 0;
                        files = 0;
                        insertions = 0;
                        deletions = 0;

                        if (maxCommits > 0 && reported >= maxCommits)
                        {
                            sha = string.Empty;
                            break;
                        }

                        continue;
                    }

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

                Report();
            }
            catch (OperationCanceledException)
            {
                // Ignored.
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    RaiseException(App.Text("Notification.QueryChangesStatFailed", e.Message));
            }
            finally
            {
                if (!proc.HasExited)
                {
                    try
                    {
                        proc.Kill(true);
                    }
                    catch
                    {
                        // Ignored.
                    }
                }
            }
        }
    }
}
