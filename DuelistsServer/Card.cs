using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    internal class Card
    {
        public string Name;
        public int MinAttack;
        public int MaxAttack;
        public int Defense;
        public int Bonus;

        public Card(string name, int minA, int maxA, int def, int bonus)
        {
            Name = name; MinAttack = minA; MaxAttack = maxA; Defense = def; Bonus = bonus;
        }
    }
}
