using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitVersion;
using LibGit2Sharp;

namespace GitVersionCore.GusFlow
{
    public class LastPlannedReleaseFinder
    {
        public VersionTaggedCommit FindLastVersionBeforeBranch(IRepository repository, Branch branch, bool ignoreHotFixVersions = false)
        {
            var openedReleases = GetOpenedReleases(repository, branch).ToList();
            var finishedReleases = GetFinishedReleases(repository, branch);

            var allReleases = openedReleases.Concat(finishedReleases);
            if (ignoreHotFixVersions)
            {
                allReleases = allReleases.Where(x => x.SemVer.Patch == 0);
            }

            return allReleases.OrderByDescending(x => x.SemVer).FirstOrDefault();
        }

        private IEnumerable<VersionTaggedCommit> GetOpenedReleases(IRepository repository, Branch branch)
        {
            var develop = repository.FindBranch("develop");
            var releaseBranches = from b in repository.Branches
                                  where b.Name.StartsWith("release/")
                                  let branchStart = repository.Commits.FindMergeBase(b.Tip, develop.Tip)
                                  where branchStart != null
                                  select new
                                  {
                                      Branch = b,
                                      StartCommit = branchStart
                                  };

            var versionedReleases = from r in releaseBranches
                                    let version = r.Branch.Name.Split('/').Last()
                                    let semVer = GitHelper.CreateSemanticVersion(version)
                                    where semVer != null
                                    select new VersionTaggedCommit(r.StartCommit, semVer);

            return versionedReleases.Where(x => x.Commit.When() < branch.Tip.When());
        }

        private IEnumerable<VersionTaggedCommit> GetFinishedReleases(IRepository repository, Branch branch)
        {
            var master = repository.FindBranch("master");
            var releaseTags = from t in repository.Tags
                              let semVer = GitHelper.CreateSemanticVersion(t.Name)
                              where semVer != null
                              let commit = t.PeeledTarget() as Commit
                              where commit != null
                              select new
                              {
                                  Tag = t,
                                  Commit = commit,
                                  Version = semVer
                              };

            var versionedTags = from t in releaseTags
                                let sharedCommit = repository.FindFirstNotSharedCommit(branch.Tip, t.Commit)
                                where sharedCommit != null
                                let startCommit = sharedCommit
                                select new VersionTaggedCommit(startCommit, t.Version);

            return versionedTags.Where(x => x.Commit.When() < branch.Tip.When());
        }
    }
}
