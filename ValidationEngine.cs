using System;
using System.Collections.Generic;
using System.Linq;

namespace RoomNavigationAlgorithm
{
    internal class ValidationReport
    {
        public bool IsBeatable { get; set; } = false;
        public List<string> WinningPath { get; set; } = new List<string>();
        public int TotalMoves => WinningPath.Count;
        public int GenerationAttempts { get; set; } = 1;
    }

    internal class StateNode//represents a timeline
    {
        public Room CurrentRoom { get; }
        public Dictionary<KeyType, int> Inventory { get; }
        public HashSet<string> UnlockedConsumableDoors { get; }
        public List<string> PathLog { get; }
        public HashSet<string> PickedUpItems { get; }

        public StateNode(Room room, Dictionary<KeyType, int> inventory, HashSet<string> unlockedDoors, List<string> pathLog, HashSet<string> pickedUpItems)
        {
            CurrentRoom = room;//the properties that are taken note of in each state
            Inventory = new Dictionary<KeyType, int>(inventory);
            UnlockedConsumableDoors = new HashSet<string>(unlockedDoors);
            PathLog = new List<string>(pathLog);
            PickedUpItems = new HashSet<string>(pickedUpItems);
        }

        public string GetStateHash()//hashing states to prevent loops, shows rooms and inventory but could be anything
        {
            string invStr = string.Join("_", Inventory.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}"));
            return $"Room_{CurrentRoom.X}_{CurrentRoom.Y}_Inv_{invStr}_Unlocks_{UnlockedConsumableDoors.Count}";
        }

        public static string GetDoorId(Room r1, Room r2)//to get a door from any direction
        {
            int minX = Math.Min(r1.X, r2.X);
            int minY = Math.Min(r1.Y, r2.Y);
            int maxX = Math.Max(r1.X, r2.X);
            int maxY = Math.Max(r1.Y, r2.Y);
            return $"{minX}_{minY}_{maxX}_{maxY}";
        }
    }

    internal class ValidationEngine
    {
        public ValidationReport CheckIfBeatable(MapGenerator map, bool isCurrentRunCheck = false, Room livePlayerRoom = null, Dictionary<KeyType, int> livePlayerInv = null, System.Threading.CancellationToken? cancelToken = null)
        {
            ValidationReport report = new ValidationReport();
            Queue<StateNode> queue = new Queue<StateNode>();
            HashSet<string> visitedStates = new HashSet<string>();

            
            Room startRoom;//starting state
            var initialInventory = new Dictionary<KeyType, int>();
            var initialLog = new List<string>();
            var initialPickedUp = new HashSet<string>();
            var initialUnlockedDoors = new HashSet<string>();

            if (isCurrentRunCheck && livePlayerRoom != null && livePlayerInv != null)//this is for the current state of the game, checking if the player is softlocked mid game, not if the entire thing is possible
            {
                startRoom = map.Grid[(livePlayerRoom.X, livePlayerRoom.Y)];//players current position

                initialInventory = new Dictionary<KeyType, int>(livePlayerInv);//temp player gets the real inventory
                initialLog.Add($"Validation started mid-run at [{startRoom.X},{startRoom.Y}].");

                if (startRoom.Item != null && startRoom.Item != KeyType.None)//pick up starting rooms item
                {
                    if (!initialInventory.ContainsKey(startRoom.Item.Value))
                        initialInventory[startRoom.Item.Value] = 0;

                    initialInventory[startRoom.Item.Value]++;

                    initialPickedUp.Add($"{startRoom.X}_{startRoom.Y}");
                    initialLog.Add($"Picked up {startRoom.Item.Value} from current room.");
                }

                foreach (var kvp in map.Grid)//check the map for any already open doors
                {
                    foreach (var door in kvp.Value.Doors.Values)
                    {
                        if (!door.IsLocked && door.IsConsumable)
                        {
                            initialUnlockedDoors.Add(StateNode.GetDoorId(door.RoomA, door.RoomB));
                        }
                    }

                    if (kvp.Value.Item == null || kvp.Value.Item == KeyType.None)//check the map for any missing items so they aren't picked up again
                    {
                        initialPickedUp.Add($"{kvp.Value.X}_{kvp.Value.Y}");
                    }
                }
            }

            else//this is to check if a fresh seed is beatable, from the start
            {
                startRoom = map.Grid[(0, 0)];//start at 0,0

                if (startRoom.Item != null && startRoom.Item != KeyType.None)//is there a starting room item
                {
                    initialInventory[startRoom.Item.Value] = 1;
                    initialPickedUp.Add($"{startRoom.X}_{startRoom.Y}");
                    initialLog.Add($"Spawned at [0,0]. Picked up {startRoom.Item.Value}.");
                }
                else
                {
                    initialLog.Add("Spawned at [0,0].");
                }
            }

            
            StateNode initialState = new StateNode(startRoom, initialInventory, initialUnlockedDoors, initialLog, initialPickedUp);//making the first state

            queue.Enqueue(initialState);
            visitedStates.Add(initialState.GetStateHash());

            while (queue.Count > 0)//breadth first state space search
            {
                if (cancelToken.HasValue && cancelToken.Value.IsCancellationRequested)
                {
                    return null;
                }

                StateNode currentState = queue.Dequeue();
                Room room = currentState.CurrentRoom;

                if (room.IsFinish)//check for finish room
                {
                    report.IsBeatable = true;
                    currentState.PathLog.Add("Goal Reached. Run Completed.");
                    report.WinningPath = currentState.PathLog;
                    return report;
                }

                foreach (var doorKvp in room.Doors)//check each door in the current room
                {
                    Direction dir = doorKvp.Key;
                    Door door = doorKvp.Value;
                    Room nextRoom = door.GetOtherRoom(room);
                    string doorId = StateNode.GetDoorId(room, nextRoom);

                    bool canEnter = false;
                    bool consumedItem = false;
                    KeyType itemConsumed = KeyType.None;
                    string unlockAction = "";

                    if (!door.IsLocked || currentState.UnlockedConsumableDoors.Contains(doorId))//door is open or unlocked
                    {
                        canEnter = true;
                    }

                    else if (!door.IsConsumable && currentState.Inventory.ContainsKey(door.RequiredKey) && currentState.Inventory[door.RequiredKey] > 0)//door is locked by a strong key
                    {
                        canEnter = true;
                        unlockAction = $" (Unlocked with {door.RequiredKey} Key)";
                    }

                    else if (door.IsConsumable || currentState.Inventory.ContainsKey(KeyType.Lockpick))//door needs a consumable, silver key or lockpick
                    {

                        if (currentState.Inventory.ContainsKey(door.RequiredKey) && currentState.Inventory[door.RequiredKey] > 0)//try silver key first
                        {
                            canEnter = true;
                            consumedItem = true;
                            itemConsumed = door.RequiredKey;
                            unlockAction = $" (Consumed {door.RequiredKey})";
                        }

                        else if (currentState.Inventory.ContainsKey(KeyType.Lockpick) && currentState.Inventory[KeyType.Lockpick] > 0)//lockpick fallback
                        {
                            canEnter = true;
                            consumedItem = true;
                            itemConsumed = KeyType.Lockpick;
                            unlockAction = $" (Used Lockpick on {door.RequiredKey} door)";
                        }
                    }

                    if (canEnter)//clone and add the new state
                    {
                        Dictionary<KeyType, int> nextInventory = new Dictionary<KeyType, int>(currentState.Inventory);//copy current data
                        HashSet<string> nextUnlockedDoors = new HashSet<string>(currentState.UnlockedConsumableDoors);
                        List<string> nextLog = new List<string>(currentState.PathLog);

                        if (consumedItem)//implement the action
                        {
                            nextUnlockedDoors.Add(doorId);
                            if (itemConsumed != KeyType.None)
                            {
                                nextInventory[itemConsumed]--;
                            }
                        }

                        string step = $"Moved {dir} to [{nextRoom.X},{nextRoom.Y}]{unlockAction}.";//log the action

                        HashSet<string> nextPickedUpItems = new HashSet<string>(currentState.PickedUpItems);
                        string roomId = $"{nextRoom.X}_{nextRoom.Y}";

                        if (nextRoom.Item != null && nextRoom.Item != KeyType.None && !nextPickedUpItems.Contains(roomId))//is there an item on the ground
                        {
                            if (!nextInventory.ContainsKey(nextRoom.Item.Value)) nextInventory[nextRoom.Item.Value] = 0;
                            nextInventory[nextRoom.Item.Value]++;
                            
                            nextPickedUpItems.Add(roomId);//item picked up
                            
                            step += $" Picked up {nextRoom.Item.Value}.";
                        }
                        nextLog.Add(step);

                        StateNode nextState = new StateNode(nextRoom, nextInventory, nextUnlockedDoors, nextLog, nextPickedUpItems);//next state
                        string stateHash = nextState.GetStateHash();

                        if (!visitedStates.Contains(stateHash))//make sure it's not a duplicate
                        {
                            visitedStates.Add(stateHash);
                            queue.Enqueue(nextState);
                        }
                    }
                }
            }

            report.IsBeatable = false;//if the queue is empty it's not beatable
            return report;
        }
    }
}