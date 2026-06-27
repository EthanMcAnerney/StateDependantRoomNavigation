using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomNavigationAlgorithm
{
    internal class Player
    {
        public Room CurrentRoom { get; set; }
        public Dictionary<KeyType, int> Keys { get; set; } = new Dictionary<KeyType, int>();
        public KeyType? SelectedKey { get; set; } = null;

        public void PickUpItem()
        {
            if (CurrentRoom.Item != null && CurrentRoom.Item != KeyType.None)
            {
                KeyType foundItem = CurrentRoom.Item.Value;

                if (!Keys.ContainsKey(foundItem))
                {
                    Keys[foundItem] = 0;
                }
                Keys[foundItem]++;
                CurrentRoom.Item = null;//remove item from room
            }
        }

        public bool HasKey(KeyType requiredKey)//can a door be unlocked
        {
            return Keys.ContainsKey(requiredKey) && Keys[requiredKey] > 0;
        }
    }
}
