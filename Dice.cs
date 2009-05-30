using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HeavyDuck.Dnd
{
    public static class Dice
    {
        private static readonly Random m_random = new Random();

        public static int Roll(int count, int sides)
        {
            int sum = 0;

            for (int i = 0; i < count; ++i)
                sum += m_random.Next(sides) + 1;

            return sum;
        }
    }
}
