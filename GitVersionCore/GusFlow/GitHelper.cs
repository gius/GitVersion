using System;
using System.Collections.Generic;
using System.Linq;
using GitVersion;
using System.Text;
using LibGit2Sharp;

namespace GitVersionCore.GusFlow
{
    public static class GitHelper
    {
        public static Branch FindParentNamedBranch(this IRepository repository, Branch branch)
        {
            bool isBranchHead = repository.Branches.Contains(branch);
            return isBranchHead ? branch : FindParentNamedBranch(repository, branch.Tip);
        }

        public static Branch FindParentNamedBranch(this IRepository repository, Commit commit)
        {
            var possibleBranches = from b in repository.Branches
                                   where !b.IsRemote
                                   where b.Commits.Contains(commit)
                                   orderby b.Name == "develop" descending, b.Name == "master" descending // commit will probably be included in several branches 
                                   select b;

            return possibleBranches.First();
        }

        public static SemanticVersion CreateSemanticVersion(string input)
        {
            SemanticVersion result;
            if (SemanticVersion.TryParse(input, out result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public static Commit FindFirstNotSharedCommit(this IRepository repository, Commit thisCommit, Commit anotherCommit)
        {
            var thisHistory = repository.Commits.QueryBy(new CommitFilter { Since = thisCommit });
            var anotherHistory = repository.Commits.QueryBy(new CommitFilter { Since = anotherCommit });

            var diff = thisHistory.Except(anotherHistory);
            return diff.LastOrDefault();
        }
    }
}
