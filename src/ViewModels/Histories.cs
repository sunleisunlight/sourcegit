using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Histories : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsSearchingCommits
        {
            get => _isSearchingCommits;
            set
            {
                if (value)
                    _repo.SelectedViewIndex = 0;
                else
                    _searchCommitContext.EndSearch();

                SetProperty(ref _isSearchingCommits, value);
            }
        }

        public SearchCommitContext SearchCommitContext
        {
            get => _searchCommitContext;
        }

        public bool EnableTopoOrder
        {
            get => _repo.UIStates.EnableTopoOrderInHistory;
            set
            {
                if (value != _repo.UIStates.EnableTopoOrderInHistory)
                {
                    _repo.UIStates.EnableTopoOrderInHistory = value;
                    _repo.RefreshCommits();
                }
            }
        }

        public bool CanToggleReflog
        {
            get => !_repo.UIStates.FocusOnCurrentBranch;
        }

        public bool IsAuthorColumnVisible
        {
            get => _repo.UIStates.IsAuthorColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsAuthorColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsAuthorColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSHAColumnVisible
        {
            get => _repo.UIStates.IsSHAColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsSHAColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsSHAColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAuthorTimeColumnVisible
        {
            get => _repo.UIStates.IsAuthorTimeColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsAuthorTimeColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsAuthorTimeColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCommitTimeColumnVisible
        {
            get => _repo.UIStates.IsCommitTimeColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsCommitTimeColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsCommitTimeColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChangesStatColumnVisible
        {
            get => Preferences.Instance.ShowChangesStatInHistories;
            set
            {
                if (Preferences.Instance.ShowChangesStatInHistories != value)
                {
                    Preferences.Instance.ShowChangesStatInHistories = value;
                    OnPropertyChanged();

                    if (value)
                        _repo.RefreshCommits();
                }
            }
        }

        /// <summary>
        ///     The `changes` column is auto-sized by its content, so that numbers like `+2199 -8` are never truncated.
        /// </summary>
        public double ChangesStatColumnMinWidth
        {
            get => _changesStatColumnMinWidth;
            private set => SetProperty(ref _changesStatColumnMinWidth, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            set
            {
                GenerateGraph(value);
                if (SetProperty(ref _commits, value))
                {
                    _widestStatTextLength = 0;
                    ChangesStatColumnMinWidth = DefaultChangesStatColumnWidth;
                    PostCommitsChanged();
                }
            }
        }

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public Models.CommitGraphHighlighting GraphHighlighting
        {
            get => _repo.UIStates.GraphHighlighting;
            set
            {
                if (_repo.UIStates.GraphHighlighting != value)
                {
                    _repo.UIStates.GraphHighlighting = value;
                    GenerateGraph(_commits);
                }
            }
        }

        public List<Models.Commit> SelectedCommits
        {
            get => _selectedCommits;
            set
            {
                var oldCount = _selectedCommits.Count;
                if (SetProperty(ref _selectedCommits, value) && oldCount + value.Count > 0)
                    PostSelectedCommitsChanged();
            }
        }

        public object DetailContext
        {
            get => _detailContext;
            set
            {
                if (SetProperty(ref _detailContext, value))
                    OnPropertyChanged(nameof(IsOpenAsStandaloneVisible));
            }
        }

        public Models.Bisect Bisect
        {
            get => _bisect;
            private set => SetProperty(ref _bisect, value);
        }

        public Models.Branch CurrentBranch
        {
            get => _currentBranch;
            set => SetProperty(ref _currentBranch, value);
        }

        public bool HasSingleRemote
        {
            get => _hasSingleRemote;
            set => SetProperty(ref _hasSingleRemote, value);
        }

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => _repo.IssueTrackers;
        }

        public GridLength LeftArea
        {
            get => _leftArea;
            set => SetProperty(ref _leftArea, value);
        }

        public GridLength RightArea
        {
            get => _rightArea;
            set => SetProperty(ref _rightArea, value);
        }

        public GridLength TopArea
        {
            get => _topArea;
            set => SetProperty(ref _topArea, value);
        }

        public GridLength BottomArea
        {
            get => _isCollapseDetails ? new GridLength(28, GridUnitType.Pixel) : _bottomArea;
            set
            {
                if (!Preferences.Instance.UseTwoColumnsLayoutInHistories && !_isCollapseDetails)
                    SetProperty(ref _bottomArea, value);
            }
        }

        public double AuthorColumnWidth
        {
            get => _repo.UIStates.AuthorColumnWidth;
            set => _repo.UIStates.AuthorColumnWidth = value;
        }

        public bool IsOpenAsStandaloneVisible
        {
            get => DetailContext is CommitDetail or RevisionCompare;
        }

        public bool IsCollapseDetails
        {
            get => _isCollapseDetails;
            set
            {
                if (!Preferences.Instance.UseTwoColumnsLayoutInHistories && SetProperty(ref _isCollapseDetails, value))
                {
                    OnPropertyChanged(nameof(TopArea));
                    OnPropertyChanged(nameof(BottomArea));
                }
            }
        }

        public Histories(Repository repo)
        {
            _repo = repo;
            _commitDetailSharedData = new CommitDetailSharedData();
            _searchCommitContext = new SearchCommitContext(repo.FullPath, this);
        }

        public bool HasShowFlag(Models.HistoryShowFlags flag)
        {
            return _repo.UIStates.HistoryShowFlags.HasFlag(flag);
        }

        public void ToggleShowFlag(Models.HistoryShowFlags flag)
        {
            if (HasShowFlag(flag))
                _repo.UIStates.HistoryShowFlags -= flag;
            else
                _repo.UIStates.HistoryShowFlags |= flag;
            _repo.RefreshCommits();
        }

        public Models.BisectState UpdateBisectInfo()
        {
            var test = Path.Combine(_repo.GitDir, "BISECT_START");
            if (!File.Exists(test))
            {
                Bisect = null;
                return Models.BisectState.None;
            }

            var head = new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResult();
            var info = new Models.Bisect();
            var markedHead = false;
            var dir = Path.Combine(_repo.GitDir, "refs", "bisect");
            if (Directory.Exists(dir))
            {
                var files = new DirectoryInfo(dir).GetFiles();
                foreach (var file in files)
                {
                    var sha = File.ReadAllText(file.FullName).Trim();
                    if (!markedHead)
                        markedHead = head.Equals(sha, StringComparison.Ordinal);

                    if (file.Name.StartsWith("bad"))
                        info.Bads.Add(sha);
                    else if (file.Name.StartsWith("good"))
                        info.Goods.Add(sha);
                    else if (file.Name.StartsWith("skip"))
                        info.Skipped.Add(sha);
                }
            }

            Bisect = info;

            if (info.Bads.Count == 0)
                return Models.BisectState.WaitingForFirstBad;

            if (markedHead)
                return Models.BisectState.WaitingForCheckoutAnother;

            if (info.Goods.Count == 0)
                return Models.BisectState.WaitingForFirstGood;

            return Models.BisectState.WaitingForMark;
        }

        public void NavigateTo(string commitSHA)
        {
            var commit = _commits.Find(x => x.SHA.StartsWith(commitSHA, StringComparison.Ordinal));
            if (commit != null)
            {
                SelectedCommits = [commit];
                return;
            }

            Task.Run(async () =>
            {
                var c = await new Commands.QuerySingleCommit(_repo.FullPath, commitSHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _ignoreSelectionChange = true;
                    SelectedCommits = [];

                    if (_detailContext is CommitDetail detail)
                    {
                        detail.Commit = c;
                    }
                    else
                    {
                        var commitDetail = new CommitDetail(_repo, _commitDetailSharedData);
                        commitDetail.Commit = c;
                        DetailContext = commitDetail;
                    }

                    _ignoreSelectionChange = false;
                });
            });
        }

        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public void CheckoutCommitDetached(Models.Commit c)
        {
            if (!c.IsCurrentHead && _repo.CanCreatePopup())
                _repo.ShowPopup(new CheckoutDetached(_repo, c));
        }

        public async Task<bool> CheckoutBranchByDecoratorAsync(Models.Decorator decorator)
        {
            if (decorator == null)
                return false;

            if (decorator.Type == Models.DecoratorType.CurrentBranchHead ||
                decorator.Type == Models.DecoratorType.CurrentCommitHead)
                return true;

            if (decorator.Type == Models.DecoratorType.LocalBranchHead)
            {
                var b = _repo.Branches.Find(x => x.Name == decorator.Name);
                if (b == null)
                    return false;

                await _repo.CheckoutBranchAsync(b);
                return true;
            }

            if (decorator.Type == Models.DecoratorType.RemoteBranchHead)
            {
                var rb = _repo.Branches.Find(x => x.FriendlyName == decorator.Name);
                if (rb == null)
                    return false;

                var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                if (lb == null || lb.Ahead.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CreateBranch(_repo, rb));
                }
                else if (lb.Behind.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                }
                else if (!lb.IsCurrent)
                {
                    await _repo.CheckoutBranchAsync(lb);
                }

                return true;
            }

            return false;
        }

        public async Task CheckoutBranchByCommitAsync(Models.Commit commit)
        {
            if (commit.IsCurrentHead)
                return;

            Models.Branch firstRemoteBranch = null;
            foreach (var d in commit.Decorators)
            {
                if (d.Type == Models.DecoratorType.LocalBranchHead)
                {
                    var b = _repo.Branches.Find(x => x.Name == d.Name);
                    if (b == null)
                        continue;

                    await _repo.CheckoutBranchAsync(b);
                    return;
                }

                if (d.Type == Models.DecoratorType.RemoteBranchHead)
                {
                    var rb = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                    if (rb == null)
                        continue;

                    var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                    if (lb != null && lb.Behind.Count > 0 && lb.Ahead.Count == 0)
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                        return;
                    }

                    firstRemoteBranch ??= rb;
                }
            }

            if (_repo.CanCreatePopup())
            {
                if (firstRemoteBranch != null)
                    _repo.ShowPopup(new CreateBranch(_repo, firstRemoteBranch));
                else if (!_repo.IsBare)
                    _repo.ShowPopup(new CheckoutDetached(_repo, commit));
            }
        }

        public async Task CherryPickAsync(Models.Commit commit)
        {
            if (_repo.CanCreatePopup())
            {
                if (commit.Parents.Count <= 1)
                {
                    _repo.ShowPopup(new CherryPick(_repo, [commit]));
                }
                else
                {
                    var parents = new List<Models.Commit>();
                    foreach (var sha in commit.Parents)
                    {
                        var parent = _commits.Find(x => x.SHA.Equals(sha, StringComparison.Ordinal));
                        if (parent == null)
                            parent = await new Commands.QuerySingleCommit(_repo.FullPath, sha).GetResultAsync();

                        if (parent != null)
                            parents.Add(parent);
                    }

                    _repo.ShowPopup(new CherryPick(_repo, commit, parents));
                }
            }
        }

        public async Task<string> GetCommitFullMessageAsync(Models.Commit commit)
        {
            return await new Commands.QueryCommitFullMessage(_repo.FullPath, commit.SHA)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public async Task<Models.Commit> CompareWithHeadAsync(Models.Commit commit)
        {
            var head = _commits.Find(x => x.IsCurrentHead);
            if (head == null)
            {
                _searchCommitContext.Selected = null;
                head = await new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResultAsync();
                if (head != null)
                    DetailContext = new RevisionCompare(_repo, commit, head);

                return null;
            }

            return head;
        }

        public void CompareWithWorktree(Models.Commit commit)
        {
            DetailContext = new RevisionCompare(_repo, commit, null);
        }

        public void LoadCommitChangesStat(string limits, CancellationToken token)
        {
            if (_commits.Count == 0 || !Preferences.Instance.ShowChangesStatInHistories)
                return;

            var commits = new Dictionary<string, Models.Commit>(_commits.Count);
            foreach (var c in _commits)
                commits[c.SHA] = c;

            var max = Math.Min(_commits.Count, MaxCommitsWithChangesStat);

            Task.Run(async () =>
            {
                var pending = new List<(Models.Commit Commit, int Files, int Insertions, int Deletions)>(256);

                void Flush()
                {
                    if (pending.Count == 0)
                        return;

                    var batch = pending.ToArray();
                    pending.Clear();
                    Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var (commit, files, insertions, deletions) in batch)
                            commit.SetChangesStat(files, insertions, deletions);

                        UpdateChangesStatColumnWidth(batch);
                    });
                }

                await new Commands.QueryCommitChangesStat(_repo.FullPath, limits)
                    .RunAsync((sha, files, insertions, deletions) =>
                    {
                        if (commits.TryGetValue(sha, out var commit))
                        {
                            pending.Add((commit, files, insertions, deletions));
                            if (pending.Count >= 128)
                                Flush();
                        }
                    }, max, token)
                    .ConfigureAwait(false);

                Flush();
            }, token);
        }

        private void UpdateChangesStatColumnWidth((Models.Commit Commit, int Files, int Insertions, int Deletions)[] batch)
        {
            var unit = App.Text("Histories.Changes.Unit");
            var fontSize = Preferences.Instance.DefaultFontSize;
            var width = _changesStatColumnMinWidth;

            foreach (var (commit, _, _, _) in batch)
            {
                if (!commit.HasChangesStat)
                    continue;

                var text = $"{commit.ChangesCount} {unit}   {commit.InsertionsText} {commit.DeletionsText}";
                if (text.Length < _widestStatTextLength)
                    continue;

                _widestStatTextLength = text.Length;

                var desired = MeasureStatWidth(text, fontSize);
                if (desired > width)
                    width = desired;
            }

            width = Math.Clamp(width, DefaultChangesStatColumnWidth, MaxChangesStatColumnWidth);
            if (width > _changesStatColumnMinWidth)
                ChangesStatColumnMinWidth = width;
        }

        /// <summary>
        ///     Measure the width of a single statistics text with the font used by the commit list.
        /// </summary>
        private static double MeasureStatWidth(string text, double fontSize)
        {
            try
            {
                // Use exactly the same font family as the commit list (`Fonts.Default`) does.
                var family = App.Current?.FindResource("Fonts.Default") as FontFamily;
                if (family == null)
                {
                    var name = Preferences.Instance.DefaultFontFamily;
                    family = string.IsNullOrWhiteSpace(name) ? FontFamily.Default : new FontFamily(name);
                }

                var typeface = new Typeface(family);
                var formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black);

                // The leading number is bold and the cell has paddings, so add a little margin here.
                return Math.Ceiling(formatted.Width * 1.06) + 24;
            }
            catch
            {
                // Never let the measurement break the history list.
                return (text.Length * fontSize * 0.62) + 24;
            }
        }

        private void PostCommitsChanged()
        {
            if (_selectedCommits.Count == 0)
                return;

            if (_commits.Count == 0 || _selectedCommits.Count > 20)
            {
                SelectedCommits = [];
                return;
            }

            var set = new HashSet<string>();
            foreach (var c in _selectedCommits)
                set.Add(c.SHA);

            var selected = new List<Models.Commit>();
            foreach (var c in _commits)
            {
                if (set.Contains(c.SHA))
                {
                    selected.Add(c);
                    set.Remove(c.SHA);
                    if (set.Count == 0)
                        break;
                }
            }

            SelectedCommits = selected;
        }

        private void PostSelectedCommitsChanged()
        {
            if (_ignoreSelectionChange)
                return;

            if (_selectedCommits.Count == 0)
            {
                _searchCommitContext.Selected = null;
                DetailContext = Models.Null.Instance;
            }
            else if (_selectedCommits.Count == 1)
            {
                var c = _selectedCommits[0];
                if (_isSearchingCommits)
                {
                    if (_searchCommitContext.Selected == null || !_searchCommitContext.Selected.SHA.Equals(c.SHA, StringComparison.Ordinal))
                        _searchCommitContext.Selected = _searchCommitContext.Results?.Find(x => x.SHA.Equals(c.SHA, StringComparison.Ordinal));
                }

                if (_detailContext is CommitDetail detail)
                    detail.Commit = c;
                else
                    DetailContext = new CommitDetail(_repo, _commitDetailSharedData) { Commit = c };
            }
            else if (_selectedCommits.Count == 2)
            {
                _searchCommitContext.Selected = null;

                if (_detailContext is RevisionCompare compare)
                    compare.SetTargets(_selectedCommits[1], _selectedCommits[0]);
                else
                    DetailContext = new RevisionCompare(_repo, _selectedCommits[1], _selectedCommits[0]);
            }
            else
            {
                _searchCommitContext.Selected = null;
                DetailContext = new Models.Count(_selectedCommits.Count);
            }

            if (_repo.UIStates.GraphHighlighting >= Models.CommitGraphHighlighting.SelectedCommitsOnly)
                GenerateGraph(_commits);
        }

        private void GenerateGraph(List<Models.Commit> commits)
        {
            var firstParentOnly = _repo.UIStates.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly);
            var highlighting = _repo.UIStates.GraphHighlighting;
            var extraHeads = new HashSet<string>();

            if (highlighting >= Models.CommitGraphHighlighting.SelectedCommitsOnly)
            {
                foreach (var c in _selectedCommits)
                    extraHeads.Add(c.SHA);
            }

            Graph = Models.CommitGraph.Generate(commits, firstParentOnly, highlighting, extraHeads);
        }

        private const int MaxCommitsWithChangesStat = 5000;
        private const double DefaultChangesStatColumnWidth = 96;
        private const double MaxChangesStatColumnWidth = 480;

        private Repository _repo = null;
        private Models.Branch _currentBranch = null;
        private bool _hasSingleRemote = false;
        private CommitDetailSharedData _commitDetailSharedData = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = [];
        private Models.CommitGraph _graph = null;
        private List<Models.Commit> _selectedCommits = [];
        private Models.Bisect _bisect = null;
        private object _detailContext = Models.Null.Instance;
        private bool _ignoreSelectionChange = false;

        private bool _isSearchingCommits = false;
        private SearchCommitContext _searchCommitContext = null;

        private GridLength _leftArea = new(1, GridUnitType.Star);
        private GridLength _rightArea = new(1, GridUnitType.Star);
        private GridLength _topArea = new(1, GridUnitType.Star);
        private GridLength _bottomArea = new(1, GridUnitType.Star);
        private bool _isCollapseDetails = false;
        private double _changesStatColumnMinWidth = DefaultChangesStatColumnWidth;
        private int _widestStatTextLength = 0;
    }
}
