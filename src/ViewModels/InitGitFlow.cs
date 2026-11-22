using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public partial class InitGitFlow : Popup
    {
        [GeneratedRegex(@"^[\w\-/\.]+$")]
        private static partial Regex REG_TAG_PREFIX();

        [LocalizedRequired("Validate.ProductionBranchRequired")]
        [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
        public string Production
        {
            get => _production;
            set => SetProperty(ref _production, value, true);
        }

        [LocalizedRequired("Validate.DevelopmentBranchRequired")]
        [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
        public string Develop
        {
            get => _develop;
            set => SetProperty(ref _develop, value, true);
        }

        [LocalizedRequired("Validate.FeaturePrefixRequired")]
        [LocalizedRegularExpression(@"^[\w\-\.]+/$", "Validate.BadFeaturePrefix")]
        public string FeaturePrefix
        {
            get => _featurePrefix;
            set => SetProperty(ref _featurePrefix, value, true);
        }

        [LocalizedRequired("Validate.ReleasePrefixRequired")]
        [LocalizedRegularExpression(@"^[\w\-\.]+/$", "Validate.BadReleasePrefix")]
        public string ReleasePrefix
        {
            get => _releasePrefix;
            set => SetProperty(ref _releasePrefix, value, true);
        }

        [LocalizedRequired("Validate.HotfixPrefixRequired")]
        [LocalizedRegularExpression(@"^[\w\-\.]+/$", "Validate.BadHotfixPrefix")]
        public string HotfixPrefix
        {
            get => _hotfixPrefix;
            set => SetProperty(ref _hotfixPrefix, value, true);
        }

        [CustomValidation(typeof(InitGitFlow), nameof(ValidateTagPrefix))]
        public string TagPrefix
        {
            get => _tagPrefix;
            set => SetProperty(ref _tagPrefix, value, true);
        }

        public InitGitFlow(Repository repo)
        {
            _repo = repo;

            var localBranches = new List<string>();
            foreach (var branch in repo.Branches)
            {
                if (branch.IsLocal)
                    localBranches.Add(branch.Name);
            }

            if (localBranches.Contains("master"))
                _production = "master";
            else if (localBranches.Contains("main"))
                _production = "main";
            else if (localBranches.Count > 0)
                _production = localBranches[0];
            else
                _production = "main";
        }

        public static ValidationResult ValidateBaseBranch(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is InitGitFlow initializer)
            {
                if (!Models.RefName.IsValidBranchName(name))
                    return new ValidationResult(App.Text("Validate.BadBranchName"));

                if (initializer._production == initializer._develop)
                    return new ValidationResult(App.Text("Validate.DevelopSameAsProduction"));
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateTagPrefix(string tagPrefix, ValidationContext ctx)
        {
            if (!string.IsNullOrWhiteSpace(tagPrefix) && !REG_TAG_PREFIX().IsMatch(tagPrefix))
                return new ValidationResult(App.Text("Validate.BadTagPrefix"));

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Init git-flow ...";

            var log = _repo.CreateLog("Gitflow - Init");
            Use(log);

            bool succ;
            var current = _repo.CurrentBranch;

            var productionBranch = _repo.Branches.Find(x => x.IsLocal && x.Name.Equals(_production, StringComparison.Ordinal));
            if (productionBranch == null)
            {
                succ = await new Commands.Branch(_repo.FullPath, _production)
                    .Use(log)
                    .CreateAsync(current.Head, true);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            var developBranch = _repo.Branches.Find(x => x.IsLocal && x.Name.Equals(_develop, StringComparison.Ordinal));
            if (developBranch == null)
            {
                succ = await new Commands.Branch(_repo.FullPath, _develop)
                    .Use(log)
                    .CreateAsync(current.Head, true);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            succ = await new Commands.GitFlow(_repo.FullPath)
                .Use(log)
                .InitAsync(_production, _develop, _featurePrefix, _releasePrefix, _hotfixPrefix, _tagPrefix);

            log.Complete();

            if (succ)
            {
                var gitflow = new Models.GitFlow();
                gitflow.ProductionBranch = _production;
                gitflow.DevelopmentBranch = _develop;
                gitflow.FeaturePrefix = _featurePrefix;
                gitflow.ReleasePrefix = _releasePrefix;
                gitflow.HotfixPrefix = _hotfixPrefix;
                _repo.GitFlow = gitflow;
            }

            return succ;
        }

        private readonly Repository _repo;
        private string _production;
        private string _develop = "develop";
        private string _featurePrefix = "feature/";
        private string _releasePrefix = "release/";
        private string _hotfixPrefix = "hotfix/";
        private string _tagPrefix = string.Empty;
    }
}
