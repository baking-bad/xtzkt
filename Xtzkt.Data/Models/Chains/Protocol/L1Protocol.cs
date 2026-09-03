using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class L1Protocol() : Protocol(Layer.L1)
{
    public required int FirstCycle { get; set; }
    public required int FirstCycleLevel { get; set; }

    public int RampUpCycles { get; set; }
    public int NoRewardCycles { get; set; }

    public int ConsensusRightsDelay { get; set; }
    public int BakerParametersActivationDelay { get; set; }

    public int BlocksPerCycle { get; set; }
    public int BlocksPerCommitment { get; set; }
    public int BlocksPerSnapshot { get; set; }
    public int BlocksPerVoting { get; set; }

    public int TimeBetweenBlocks { get; set; }

    public int AttestersPerBlock { get; set; }

    [Column(nameof(HardOperationGasLimit))]
    public int HardOperationGasLimit { get; set; }

    [Column(nameof(HardOperationStorageLimit))]
    public int HardOperationStorageLimit { get; set; }

    [Column(nameof(HardBlockGasLimit))]
    public int HardBlockGasLimit { get; set; }

    public long MinimalStake { get; set; }
    public long MinimalFrozenStake { get; set; }

    public long BlockDeposit { get; set; }
    public long BlockReward0 { get; set; }
    public long BlockReward1 { get; set; }
    public long MaxBakingReward { get; set; }

    public long AttestationDeposit { get; set; }
    public long AttestationReward0 { get; set; }
    public long AttestationReward1 { get; set; }
    public long MaxAttestationReward { get; set; }

    [Column(nameof(OriginationSize))]
    public int OriginationSize { get; set; }

    [Column(nameof(ByteCost))]
    public int ByteCost { get; set; }

    public int ProposalQuorum { get; set; }
    public int BallotQuorumMin { get; set; }
    public int BallotQuorumMax { get; set; }

    public int LBToggleThreshold { get; set; }

    public int ConsensusThreshold { get; set; }
    public int MinParticipationNumerator { get; set; }
    public int MinParticipationDenominator { get; set; }
    public int DenunciationPeriod { get; set; }
    public int SlashingDelay { get; set; }
    public int MaxDelegatedOverFrozenRatio { get; set; }
    public int MaxExternalOverOwnStakeRatio { get; set; }
    public int StakePowerMultiplier { get; set; }

    public int SmartRollupOriginationSize { get; set; }
    public long SmartRollupStakeAmount { get; set; }
    public int SmartRollupChallengeWindow { get; set; }
    public int SmartRollupCommitmentPeriod { get; set; }
    public int SmartRollupTimeoutPeriod { get; set; }

    public string? Dictator { get; set; }

    public int DoubleBakingSlashedPercentage { get; set; }
    public int DoubleConsensusSlashedPercentage { get; set; }

    public int NumberOfShards { get; set; }
    public int ToleratedInactivityPeriod { get; set; }

    #region helpers
    public int GetCycleStart(int cycle)
    {
        if (cycle < FirstCycle)
            throw new Exception("Cycle doesn't match the protocol");

        return FirstCycleLevel + (cycle - FirstCycle) * BlocksPerCycle;
    }
    public int GetCycleEnd(int cycle)
    {
        if (cycle < FirstCycle)
            throw new Exception("Cycle doesn't match the protocol");

        return GetCycleStart(cycle) + BlocksPerCycle - 1;
    }
    public int GetCycle(int level)
    {
        if (level < FirstLevel)
            throw new Exception("Level doesn't match the protocol");

        if (level < FirstCycleLevel)
            return FirstCycle - 1;

        return FirstCycle + (level - FirstCycleLevel) / BlocksPerCycle;
    }
    public bool IsCycleStart(int level)
    {
        if (level < FirstLevel)
            throw new Exception("Level doesn't match the protocol");

        return (level - FirstCycleLevel) % BlocksPerCycle == 0;
    }
    public bool IsCycleEnd(int level)
    {
        if (level < FirstLevel)
            throw new Exception("Level doesn't match the protocol");

        return (level + 1 - FirstCycleLevel) % BlocksPerCycle == 0;
    }

    [NotMapped]
    public int SnapshotsPerCycle => BlocksPerCycle / BlocksPerSnapshot;

    [NotMapped]
    public bool HasDictator => Dictator != null;
    #endregion
}

public static class L1ProtocolModel
{
    public static void BuildL1ProtocolModel(this ModelBuilder modelBuilder)
    {
    }
}
