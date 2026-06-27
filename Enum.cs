using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomNavigationAlgorithm
{
         public enum Direction
        {
            North,
            East,
            South,
            West
        }

        public enum KeyType
        {
            None,//Open Doors
            Red, //Strong Keys, arent consumed after use
            Blue,
            Green,
            Yellow,
            Silver, //Consumed after use, only use on silver doors
            Lockpick //Can open any door, consumed on use
        }
}
