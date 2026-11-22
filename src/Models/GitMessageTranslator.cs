using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    /// <summary>
    ///     Git always prints its messages in English (this app forces the `C` locale so that the output can be
    ///     parsed safely). This helper turns the well-known messages into the current UI language, so that users
    ///     of a translated UI are not confused by English notifications.
    ///
    ///     Only the lines that can be recognized are translated. Unknown lines are kept as-is to avoid losing
    ///     information. It is a no-op for the locales that have no translated rules.
    /// </summary>
    public static class GitMessageTranslator
    {
        private class Rule
        {
            public Regex Pattern { get; set; }

            /// <summary>
            ///     `null` means this line should be removed (like the trailing hints printed by git itself).
            /// </summary>
            public string Key { get; set; }
        }

        public static string Translate(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            if (!IsChineseUI())
                return message;

            var lines = message.Replace("\r\n", "\n").Split('\n');
            var builder = new StringBuilder(message.Length);
            var translatedCount = 0;
            var keptEnglish = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                if (TryMatch(line, out var translated))
                {
                    translatedCount++;
                    if (!string.IsNullOrEmpty(translated))
                        builder.Append(translated).Append('\n');
                }
                else if (line.StartsWith("hint:", StringComparison.OrdinalIgnoreCase))
                {
                    // Hints are suggestions printed by git itself. Ignore them to reduce the noise.
                    continue;
                }
                else
                {
                    keptEnglish = true;
                    builder.Append(line).Append('\n');
                }
            }

            var result = builder.ToString().TrimEnd();
            if (translatedCount == 0 || string.IsNullOrEmpty(result))
                return message;

            if (keptEnglish)
            {
                // Some lines could not be translated. Keep the original text as the detail, so nothing is lost.
                return $"{App.Text("GitError.DetailKept")}\n\n{result}";
            }

            return result;
        }

        private static bool TryMatch(string line, out string translated)
        {
            foreach (var rule in RULES)
            {
                var match = rule.Pattern.Match(line);
                if (!match.Success)
                    continue;

                if (rule.Key == null)
                {
                    translated = null;
                    return true;
                }

                var args = new List<object>();
                for (var i = 1; i < match.Groups.Count; i++)
                    args.Add(DescribePushRejectReason(match.Groups[i].Value));

                translated = App.Text(rule.Key, args.ToArray());
                return true;
            }

            translated = null;
            return false;
        }

        private static string DescribePushRejectReason(string reason)
        {
            return reason switch
            {
                "fetch first" => App.Text("GitError.Reason.FetchFirst"),
                "non-fast-forward" => App.Text("GitError.Reason.NonFastForward"),
                "stale info" => App.Text("GitError.Reason.StaleInfo"),
                "remote rejected" => App.Text("GitError.Reason.RemoteRejected"),
                "needs force" => App.Text("GitError.Reason.NeedsForce"),
                _ => reason,
            };
        }

        private static bool IsChineseUI()
        {
            var key = App.ActiveLocaleKey;
            return key != null && key.StartsWith("zh_CN", StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Rule[] RULES =
        [
            // ---- Push ----
            new Rule { Key = "GitError.PushingTo", Pattern = new Regex(@"^Pushing to (.+)$") },
            new Rule { Key = "GitError.PushTarget", Pattern = new Regex(@"^To (\S+)$") },
            new Rule { Key = "GitError.PushRejected", Pattern = new Regex(@"^!\s+\[rejected\]\s+(\S+)\s+->\s+(\S+)\s+\((.+)\)$") },
            new Rule { Key = "GitError.PushRejectedRemote", Pattern = new Regex(@"^!\s+\[remote rejected\]\s+(\S+)\s+->\s+(\S+)\s+\((?:remote: )?(.+)\)$") },
            new Rule { Key = "GitError.PushFailedRefs", Pattern = new Regex(@"^(?:error: )?failed to push some refs to '(.+)'$") },
            new Rule { Key = "GitError.EverythingUpToDate", Pattern = new Regex(@"^Everything up-to-date\.?$") },
            new Rule { Key = "GitError.RemoteHasNewWork", Pattern = new Regex(@"^hint: Updates were rejected because the remote contains work that you do") },
            new Rule { Key = "GitError.BranchBehindRemote", Pattern = new Regex(@"^hint: Updates were rejected because the tip of your current branch is behind") },
            new Rule { Key = "GitError.TagExistsOnRemote", Pattern = new Regex(@"^hint: Updates were rejected because the tag already exists in the remote\.?$") },
            new Rule { Key = "GitError.PushRequiresForce", Pattern = new Regex(@"^hint: Updates were rejected because a pushed branch tip is behind") },
            new Rule { Key = "GitError.Diverged", Pattern = new Regex(@"^Your branch and '(.+)' have diverged,?$") },
            new Rule { Key = null, Pattern = new Regex(@"^and have \d+ and \d+ different commits? each,? respectively\.?$") },
            new Rule { Key = null, Pattern = new Regex(@"^\(use ""git pull"".*\)$") },

            // ---- Authentication / network ----
            new Rule { Key = "GitError.RepositoryNotFoundShort", Pattern = new Regex(@"^remote: Repository not found\.?$") },
            new Rule { Key = "GitError.RemotePermissionDenied", Pattern = new Regex(@"^remote: Permission to (.+) denied to (.+)\.?$") },
            new Rule { Key = "GitError.RepositoryNotFound", Pattern = new Regex(@"^(?:fatal: )?repository '(.+)' not found$") },
            new Rule { Key = "GitError.NotARepoPath", Pattern = new Regex(@"^(?:fatal: )?'?(.+)'? does not appear to be a git repository$") },
            new Rule { Key = "GitError.CouldNotReadRemote", Pattern = new Regex(@"^(?:fatal: )?Could not read from remote repository\.?$") },
            new Rule { Key = "GitError.CheckAccess", Pattern = new Regex(@"^Please make sure you have the correct access rights") },
            new Rule { Key = null, Pattern = new Regex(@"^and the repository exists\.$") },
            new Rule { Key = "GitError.PublicKeyDenied", Pattern = new Regex(@"^(?:\S+: )?Permission denied \(publickey.*\)\.?$") },
            new Rule { Key = "GitError.PermissionDenied", Pattern = new Regex(@"^Permission denied \((.+)\)\.?$") },
            new Rule { Key = "GitError.HostKeyFailed", Pattern = new Regex(@"^Host key verification failed\.?$") },
            new Rule { Key = "GitError.UnableToAccess", Pattern = new Regex(@"^(?:fatal: )?unable to access '(.+)': (.+)$") },
            new Rule { Key = "GitError.AuthFailed", Pattern = new Regex(@"^(?:fatal: )?Authentication failed for '(.+)'$") },
            new Rule { Key = "GitError.CouldNotReadUsername", Pattern = new Regex(@"^(?:fatal: )?could not read Username for '(.+)': (.+)$") },
            new Rule { Key = "GitError.SshConnectFailed", Pattern = new Regex(@"^ssh: connect to host (\S+) port (\d+): (.+)$") },
            new Rule { Key = "GitError.CouldNotResolveHost", Pattern = new Regex(@"^(?:fatal: )?(?:Could not resolve host|Could not resolve hostname)[: ]*(.*)$") },
            new Rule { Key = "GitError.RemoteHungUp", Pattern = new Regex(@"^(?:fatal: )?the remote end hung up unexpectedly\.?$") },
            new Rule { Key = "GitError.EarlyEOF", Pattern = new Regex(@"^(?:fatal: )?early EOF$") },
            new Rule { Key = "GitError.RpcFailed", Pattern = new Regex(@"^(?:error: )?RPC failed.*$") },
            new Rule { Key = "GitError.RemoteMessage", Pattern = new Regex(@"^remote: (?!Repository not found|Permission to)(.+)$") },

            // ---- Local operations ----
            new Rule { Key = "GitError.LocalChangesOverwritten", Pattern = new Regex(@"^(?:error: )?Your local changes to the following files would be overwritten by (merge|checkout|pull|switch|rebase)\.?:?$") },
            new Rule { Key = "GitError.UntrackedOverwritten", Pattern = new Regex(@"^(?:error: )?The following untracked working tree files would be overwritten") },
            new Rule { Key = "GitError.MergeConflict", Pattern = new Regex(@"^CONFLICT \(([^)]+)\): (.+)$") },
            new Rule { Key = "GitError.UnrelatedHistories", Pattern = new Regex(@"^(?:fatal: )?refusing to merge unrelated histories\.?$") },
            new Rule { Key = "GitError.NoTrackingInfo", Pattern = new Regex(@"^There is no tracking information for the current branch\.?$") },
            new Rule { Key = "GitError.NoUpstream", Pattern = new Regex(@"^(?:fatal: )?The current branch (.+) has no upstream branch\.?$") },
            new Rule { Key = "GitError.NothingToCommit", Pattern = new Regex(@"^nothing to commit, working tree clean$") },
            new Rule { Key = "GitError.BranchExists", Pattern = new Regex(@"^(?:fatal: )?[Aa] branch named '(.+)' already exists\.?$") },
            new Rule { Key = "GitError.PathspecNoMatch", Pattern = new Regex(@"^(?:error: |fatal: )?pathspec '(.+)' did not match") },
            new Rule { Key = "GitError.NotAGitRepository", Pattern = new Regex(@"^(?:fatal: )?not a git repository.*$") },
            new Rule { Key = "GitError.BadRevision", Pattern = new Regex(@"^(?:fatal: )?(?:bad revision|bad object|invalid object name|ambiguous argument) '?([^':]*)'?(:.*)?$") },
            new Rule { Key = "GitError.GpgSignFailed", Pattern = new Regex(@"^(?:error: )?gpg failed to sign the data.*$") },
            new Rule { Key = "GitError.NoEmailConfigured", Pattern = new Regex(@"^(?:fatal: )?unable to auto-detect email address.*$") },
            new Rule { Key = "GitError.LockFileExists", Pattern = new Regex(@"^(?:fatal: )?Unable to create '(.+)': File exists\.?$") },
            new Rule { Key = "GitError.CannotLockRef", Pattern = new Regex(@"^(?:error: )?cannot lock ref '(.+)'") },
            new Rule { Key = "GitError.NotOnBranch", Pattern = new Regex(@"^(?:fatal: )?You are not currently on a branch\.?$") },
            new Rule { Key = "GitError.UnmergedFiles", Pattern = new Regex(@"^(?:error: )?Committing is not possible because you have unmerged files\.?$") },

            // ---- Useless tails printed by git ----
            new Rule { Key = null, Pattern = new Regex(@"^See git-.*\(1\) for details\.$") },
            new Rule { Key = null, Pattern = new Regex(@"^Use '--' to separate paths from revisions.*$") },
            new Rule { Key = null, Pattern = new Regex(@"^'git <command>.*'$") },
            new Rule { Key = null, Pattern = new Regex(@"^Please specify which branch you want to (merge with|rebase onto)\.$") },
        ];
    }
}
