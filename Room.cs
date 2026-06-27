using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RoomNavigationAlgorithm
{
    internal class Room
    {
        public int X { get; set; }
        public int Y { get; set; }
        public KeyType? Item { get; set; }
        public bool IsExplored { get; set; } = false;
        public bool IsFinish { get; set; } = false;
        public int ZoneId { get; set; }//for mode A, what key zone a room belongs to, debugging

        public Dictionary<Direction, Door> Doors { get; set; } = new Dictionary<Direction, Door>();

        public Room(int x, int y)
        {
            X = x;
            Y = y;
        }

        public SolidColorBrush GetRoomColor()//making a subtle unique room colour from coordinates to indicate room changes
        {
            byte r = (byte)(200 + (Math.Abs(X * 37) % 55));
            byte g = (byte)(200 + (Math.Abs(Y * 13) % 55));
            byte b = (byte)(200 + (Math.Abs((X + Y) * 23) % 55));

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
