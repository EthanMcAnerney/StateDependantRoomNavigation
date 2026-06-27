using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RoomNavigationAlgorithm
{
    internal class Door
    {
        public Room RoomA { get; set; }
        public Room RoomB { get; set; }
        public KeyType RequiredKey { get; set; }
        public bool IsLocked { get; set; }
        public bool IsConsumable { get; set; }

        public bool Unlock(Player player, KeyType selectedKey)
        {
            if (!IsLocked) return true;//already open

            if ((selectedKey == RequiredKey || selectedKey == KeyType.Lockpick) && player.HasKey(selectedKey))//did they choose the right key and do they have it
            {
                if (selectedKey == KeyType.Silver || selectedKey == KeyType.Lockpick)//if its a consumable decrement their count in inventory
                {
                    player.Keys[selectedKey]--;
                }

                IsLocked = false;//unlock door
                return true;
            }

            return false;//unlock failed
        }

        public Room GetOtherRoom(Room currentRoom)
        {
            if (currentRoom == RoomA) return RoomB;
            if (currentRoom == RoomB) return RoomA;
            return null;//shouldnt happen
        }
    }
}


