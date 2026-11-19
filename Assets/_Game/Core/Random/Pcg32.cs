namespace DarkFantasy.Core.Random
{
    /// <summary>
    /// PCG32 (XSH-RR), M.E. O'Neill, pcg-random.org. Small, fast, and its whole state is two
    /// ulongs, which the save file stores so sequences resume exactly after loading.
    /// </summary>
    public sealed class Pcg32 : IRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const double TwoPow53 = 9007199254740992.0;

        private ulong _state;
        private readonly ulong _inc;

        public ulong State => _state;
        public ulong Increment => _inc;

        /// <param name="seed">Initial state seed.</param>
        /// <param name="stream">Stream selector; different streams give independent sequences.</param>
        public Pcg32(ulong seed, ulong stream = 54)
        {
            _state = 0UL;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private Pcg32(ulong state, ulong inc, bool _)
        {
            _state = state;
            _inc = inc;
        }

        /// <summary>Restores a generator from a saved <see cref="State"/> and <see cref="Increment"/>.</summary>
        public static Pcg32 FromState(ulong state, ulong increment) => new Pcg32(state, increment, true);

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        public double NextDouble()
        {
            ulong a = NextUInt() >> 5; // 27 bits
            ulong b = NextUInt() >> 6; // 26 bits
            return (a * 67108864.0 + b) / TwoPow53;
        }

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        public bool Chance(double p)
        {
            if (p <= 0.0) return false;
            if (p >= 1.0) return true;
            return NextDouble() < p;
        }
    }
}
