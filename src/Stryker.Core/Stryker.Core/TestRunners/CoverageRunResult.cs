using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Stryker.Core.TestRunners
{
    public enum CoverageConfidence
    {
        Exact,
        Normal,
        Dubious,
        UnexpectedCase
    }

    [Flags]
    public enum MutationTestingRequirements
    {
        None = 0,
        Static = 1,
        CoveredOutsideTest = 2,
        NeedEarlyActivation = 4,
        AgainstAllTests = 8,
        NotCovered = 256
    }

    public class CoverageRunResult
    {
        private readonly Dictionary<int, MutationTestingRequirements> _mutationFlags = new();

        public Guid TestId { get; }

        public IReadOnlyCollection<int> MutationsCovered => _mutationFlags.Keys;

        public MutationTestingRequirements this[int mutation] => _mutationFlags.ContainsKey(mutation)
            ? _mutationFlags[mutation]
            : MutationTestingRequirements.NotCovered;

        public CoverageConfidence Confidence { get; private set; }

        public IReadOnlyCollection<int> LeakedMutations  => _mutationFlags
            .Where(p => p.Value.HasFlag(MutationTestingRequirements.CoveredOutsideTest)).Select(p => p.Key).ToImmutableArray();

        public CoverageRunResult(Guid testId, CoverageConfidence confidence, IEnumerable<int> coveredMutations,
            IEnumerable<int> detectedStaticMutations, IEnumerable<int> leakedMutations)
        {
            TestId = testId;
            foreach (var coveredMutation in coveredMutations)
            {
                _mutationFlags[coveredMutation] = MutationTestingRequirements.None;
            }

            foreach (var detectedStaticMutation in detectedStaticMutations)
            {
                _mutationFlags[detectedStaticMutation] = MutationTestingRequirements.Static;
            }

            foreach (var leakedMutation in leakedMutations)
            {
                _mutationFlags[leakedMutation] = confidence == CoverageConfidence.Exact ? MutationTestingRequirements.NeedEarlyActivation: MutationTestingRequirements.CoveredOutsideTest;
            }

            Confidence = confidence;
        }

        public void Merge(CoverageRunResult coverageRunResult)
        {
            Confidence = (CoverageConfidence)Math.Min((int)Confidence, (int)coverageRunResult.Confidence);
            foreach (var mutationFlag in coverageRunResult._mutationFlags)
            {
                if (_mutationFlags.ContainsKey(mutationFlag.Key))
                {
                    _mutationFlags[mutationFlag.Key] |= mutationFlag.Value;
                }
                else
                {
                    _mutationFlags[mutationFlag.Key] = mutationFlag.Value;
                }
            }
        }

        public void ConfirmCoverageForLeakedMutations(ISet<int> mutationSeenInSetup)
        {
            foreach (var i in mutationSeenInSetup)
            {
                if (!_mutationFlags.ContainsKey(i))
                {
                    _mutationFlags[i] = MutationTestingRequirements.NeedEarlyActivation;
                    continue;
                }
                _mutationFlags[i] = (_mutationFlags[i] & ~MutationTestingRequirements.CoveredOutsideTest) | MutationTestingRequirements.NeedEarlyActivation;
            }
        }
    }
}
