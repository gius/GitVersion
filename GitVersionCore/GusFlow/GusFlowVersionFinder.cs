using System;
using System.Linq;
using GitVersion;
using LibGit2Sharp;

namespace GitVersionCore.GusFlow
{
    public class GusFlowVersionFinder
    {
        public SemanticVersion FindVersion(GitVersionContext context)
        {
            var version = GetTaggedVersion(context);
            if (version != null)
            {
                return version;
            }

            var parentBranch = context.Repository.FindParentNamedBranch(context.CurrentBranch);

            if (parentBranch.Name == "develop")
            {
                return GetVersionForDevelop(context, parentBranch);
            }
            else if (parentBranch.Name.StartsWith("feature/"))
            {
                return GetVersionForFeature(context, parentBranch);
            }
            else if (parentBranch.Name.StartsWith("release/"))
            {
                return GetVersionForRelease(context, parentBranch);
            }
            else if (parentBranch.Name.StartsWith("hotfix/"))
            {
                return GetVersionForHotfix(context, parentBranch);
            }
            else
            {
                return GetUnknownVersion(context, parentBranch);
            }
        }

        private SemanticVersion GetTaggedVersion(GitVersionContext context)
        {
            var versionTags = from t in context.Repository.Tags
                              where t.PeeledTarget() == context.CurrentBranch.Tip
                              select GitHelper.CreateSemanticVersion(t.Name);

            var version = versionTags.Where(x => x != null).OrderByDescending(x => x).FirstOrDefault();

            if (version != null)
            {
                version.BuildMetaData = new SemanticVersionBuildMetaData
                {
                    Branch = "master",
                    CommitsSinceTag = 0,
                    Sha = context.CurrentBranch.Tip.Sha,
                    ReleaseDate = new ReleaseDate
                    {
                        OriginalCommitSha = context.CurrentBranch.Tip.Sha,
                        OriginalDate = context.CurrentBranch.Tip.When()
                    }
                };
            }

            return version;
        }

        private SemanticVersion GetVersionForDevelop(GitVersionContext context, Branch parentBranch)
        {
            var currentVersion = GetVersionInfoForSpecificBranch(context.Repository, context.CurrentBranch);

            return BuildSemanticVersion(
                currentVersion.VersionSource.SemVer.Major,
                currentVersion.VersionSource.SemVer.Minor + 1,
                0,
                currentVersion.CommitsSinceVersionSource,
                "beta",
                "develop",
                context.CurrentBranch.Tip);
        }

        private SemanticVersion GetVersionForFeature(GitVersionContext context, Branch parentBranch)
        {
            var currentVersion = GetVersionInfoForSpecificBranch(context.Repository, context.CurrentBranch);

            return BuildSemanticVersion(
                currentVersion.VersionSource.SemVer.Major,
                currentVersion.VersionSource.SemVer.Minor + 1,
                0,
                currentVersion.CommitsSinceVersionSource,
                parentBranch.Name.Replace("feature/", "alpha/"),
                parentBranch.Name,
                context.CurrentBranch.Tip);
        }

        private SemanticVersion GetVersionForRelease(GitVersionContext context, Branch parentBranch)
        {
            var develop = context.Repository.FindBranch("develop");
            var branchStart = context.Repository.Commits.FindMergeBase(context.CurrentBranch.Tip, develop.Tip);

            var currentVersion = SemanticVersion.Parse(parentBranch.Name.Split('/').Last());
            var numberOfCommitsInBranch = context.CurrentBranch.Commits.TakeWhile(x => x != branchStart).Count();

            return BuildSemanticVersion(
                currentVersion.Major,
                currentVersion.Minor,
                0,
                numberOfCommitsInBranch,
                "rc",
                parentBranch.Name,
                context.CurrentBranch.Tip);
        }

        private SemanticVersion GetVersionForHotfix(GitVersionContext context, Branch parentBranch)
        {
            var master = context.Repository.FindBranch("master");
            var branchStart = context.Repository.Commits.FindMergeBase(context.CurrentBranch.Tip, master.Tip);

            var currentVersion = SemanticVersion.Parse(parentBranch.Name.Split('/').Last());
            var numberOfCommitsInBranch = context.CurrentBranch.Commits.TakeWhile(x => x != branchStart).Count();

            return BuildSemanticVersion(
                currentVersion.Major,
                currentVersion.Minor,
                currentVersion.Patch,
                numberOfCommitsInBranch,
                "patch",
                parentBranch.Name,
                context.CurrentBranch.Tip);
        }

        private SemanticVersion GetUnknownVersion(GitVersionContext context, Branch parentBranch)
        {
            var currentVersion = GetVersionInfoForSpecificBranch(context.Repository, context.CurrentBranch);

            return BuildSemanticVersion(
                currentVersion.VersionSource.SemVer.Major,
                currentVersion.VersionSource.SemVer.Minor + 1,
                0,
                currentVersion.CommitsSinceVersionSource,
                "alpha/unknown",
                parentBranch.Name,
                context.CurrentBranch.Tip);
        }

        private VersionInfo GetVersionInfoForSpecificBranch(IRepository repository, Branch branch)
        {
            var lastPlannedRelease = new LastPlannedReleaseFinder().FindLastVersionBeforeBranch(repository, branch, true);
            if (lastPlannedRelease == null)
            {
                lastPlannedRelease = new VersionTaggedCommit(branch.Commits.Last(), new SemanticVersion());
            }

            var numberOfCommitsSinceRelease = branch.Commits.TakeWhile(x => x != lastPlannedRelease.Commit).Count();

            return new VersionInfo
            {
                VersionSource = lastPlannedRelease,
                CommitsSinceVersionSource = numberOfCommitsSinceRelease
            };
        }

        private static SemanticVersion BuildSemanticVersion(int major, int minor, int patch, int commitsSinceLastTag, string preReleaseTag, string branchName, Commit sourceCommit)
        {
            return new SemanticVersion
            {
                Major = major,
                Minor = minor,
                Patch = patch,
                PreReleaseTag = new SemanticVersionPreReleaseTag(preReleaseTag, commitsSinceLastTag),
                BuildMetaData = new SemanticVersionBuildMetaData
                {
                    Branch = branchName,
                    CommitsSinceTag = commitsSinceLastTag,
                    Sha = sourceCommit.Sha,
                    ReleaseDate = new ReleaseDate
                    {
                        OriginalCommitSha = sourceCommit.Sha,
                        OriginalDate = sourceCommit.When(),
                        CommitSha = sourceCommit.Sha,
                        Date = sourceCommit.When()
                    }
                }
            };
        }

        private class VersionInfo
        {
            public VersionTaggedCommit VersionSource { get; set; }
            public int CommitsSinceVersionSource { get; set; }
        }
    }
}