namespace GitVersion
{
    using System;
    using System.Linq;
    using GitVersionCore.GusFlow;
    using LibGit2Sharp;

    public class GitVersionFinder
    {
        public SemanticVersion FindVersion(GitVersionContext context)
        {
            EnsureMainTopologyConstraints(context);

            Logger.WriteInfo("GusFlow version strategy will be used");
            return new GusFlowVersionFinder().FindVersion(context);
        }

        void EnsureMainTopologyConstraints(GitVersionContext context)
        {
            EnsureLocalBranchExists(context.Repository, "master");
            EnsureLocalBranchExists(context.Repository, "develop");
        }

        void EnsureLocalBranchExists(IRepository repository, string branchName)
        {
            if (repository.FindBranch(branchName) != null)
            {
                return;
            }

            var existingBranches = string.Format("'{0}'", string.Join("', '", repository.Branches.Select(x => x.CanonicalName)));
            throw new ErrorException(string.Format("This repository doesn't contain a branch named '{0}'. Please create one. Existing branches: {1}", branchName, existingBranches));
        }
    }
}
