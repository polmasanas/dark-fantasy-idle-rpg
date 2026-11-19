namespace DarkFantasy.Core.Random
{
    /// <summary>
    /// The only source of randomness in Game.Core (decision D-013). Implementations must be
    /// deterministic for a given state so fights and offline simulation are reproducible.
    /// </summary>
    public interface IRng
    {
        /// <summary>Next 32 random bits.</summary>
        uint NextUInt();

        /// <summary>Uniform double in [0, 1) with 53 bits of precision.</summary>
        double NextDouble();

        /// <summary>Uniform double in [min, max).</summary>
        double Range(double min, double max);

        /// <summary>True with probability p (p &lt;= 0 never, p &gt;= 1 always).</summary>
        bool Chance(double p);
    }
}
