using System;

namespace SoDArchipelago
{
    // Copy order comes from the server's full received list, so reconnects and batch sizes never change the remainder.
    internal static class StardustAllocation
    {
        public static int Cumulative(int copies, int total, int slots)
        {
            if (copies < 0 || total < 0 || slots <= 0) throw new ArgumentOutOfRangeException();
            return checked(copies * (total / slots) + Math.Min(copies, total % slots));
        }

        public static int Value(int copy, int total, int slots) =>
            Cumulative(copy, total, slots) - Cumulative(copy - 1, total, slots);
    }
}
