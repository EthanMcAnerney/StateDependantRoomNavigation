using RoomNavigationAlgorithm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoomNavigationAlgorithm
{
    internal class MapGenerator
    {
        public Dictionary<(int x, int y), Room> Grid { get; private set; }
        public int ItemChance { get; private set; }
        public int LockChance { get; private set; }

        public int LastValidSubSeed { get; private set; }
        public Dictionary<int, KeyType> ZoneThemes { get; private set; } = new Dictionary<int, KeyType>();

        public void GenerateEmptyGrid(int size)
        {
            Grid = new Dictionary<(int x, int y), Room>();
            int bound = size / 2;//setting size 11 is +/-5

            for (int x = -bound; x <= bound; x++)//making empty rooms
            {
                for (int y = -bound; y <= bound; y++)
                {
                    Grid.Add((x, y), new Room(x, y));
                }
            }

            foreach (var kvp in Grid)//link rooms with their doors, only east and north need done
            {
                var coord = kvp.Key;
                var currentRoom = kvp.Value;

                if (Grid.TryGetValue((coord.x, coord.y + 1), out Room northRoom))//y+1 north
                {
                    if (!currentRoom.Doors.ContainsKey(Direction.North))
                    {
                        var newDoor = new Door { RoomA = currentRoom, RoomB = northRoom };
                        currentRoom.Doors[Direction.North] = newDoor;
                        northRoom.Doors[Direction.South] = newDoor;
                    }
                }

                if (Grid.TryGetValue((coord.x + 1, coord.y), out Room eastRoom))//x+1 east
                {
                    if (!currentRoom.Doors.ContainsKey(Direction.East))
                    {
                        var newDoor = new Door { RoomA = currentRoom, RoomB = eastRoom };
                        currentRoom.Doors[Direction.East] = newDoor;
                        eastRoom.Doors[Direction.West] = newDoor;
                    }
                }
            }
        }

        public void SetStartAndGoal(int size, int seed)
        {
            Random rng = new Random(seed);
            int bound = size / 2;

            int side = rng.Next(4); //0 north, 1 east, 2 south, 3 west
            int goalX = 0;
            int goalY = 0;

            switch (side)//a random border coordinate is used for the finish room
            {
                case 0: goalY = bound; goalX = rng.Next(-bound, bound + 1); break;
                case 1: goalX = bound; goalY = rng.Next(-bound, bound + 1); break;
                case 2: goalY = -bound; goalX = rng.Next(-bound, bound + 1); break;
                case 3: goalX = -bound; goalY = rng.Next(-bound, bound + 1); break;
            }

            Grid[(goalX, goalY)].IsFinish = true;//chosen room set to finish
        }

        public void GenerateMode1_Zones(int size, int masterSeed)//mode 1 uses zones, each zone has a strong key and is more densly populated with its locks making it harder until you navigate to the key
            //there is always a path to the strong key in a zone making it possible and getting the zone's key will make it easier to explore, this repeats for all 4 zones, there is an order and the
            //beatable route uses the previous xompletedd zones in mind when generating, although it isnt neccessary to follow, the starting zone is smaller, the final zone has the finish line locked by all of that zones colour
        {
            Random rng = new Random(masterSeed);
            //tuning

            double zone1SizePercent = 0.10;//map % of how big zone 1 can get
            int seedDistanceDivisor = 3; //map size dived by x, is minimum distance when zones are placed to grow

            int webPreviousKeyChance = 80;//web is the beatable failsafe path, the chance its locks has a previous zones lock or open
            int webOpenChance = 20;

            if (webPreviousKeyChance + webOpenChance != 100)
                throw new Exception("web needs to be 100");

            //zone tuning
            int chancePrimaryLock = 35;//the chance for the lock to belong to the zone key
            int chancePreviousLock = 25;//the chance for the zone to contain older locks
            int chanceConsumable = 5;//silver locks       
            int chanceChaos = 20;//chance for future keys
            int chanceOpen = 15; //chanmce for open

            if (chancePrimaryLock + chancePreviousLock + chanceConsumable + chanceChaos + chanceOpen != 100)
                throw new Exception("zone tuning needs to be 100");

            //item tuning
            int lockpickCount = (size <= 7) ? 1 : (size == 11) ? 2 : 3;
            int silverCount = size / 2;

            //part1 making zones
            GenerateEmptyGrid(size);
            int bound = size / 2;

            List<Room> edgeRooms = Grid.Values.Where(r => Math.Abs(r.X) == bound || Math.Abs(r.Y) == bound).ToList();
            Room finishRoom = edgeRooms[rng.Next(edgeRooms.Count)];
            finishRoom.IsFinish = true;

            int totalRooms = Grid.Count;
            int z1Target = Math.Max(8, (int)(totalRooms * zone1SizePercent));
            int z2Target = (totalRooms - z1Target) / 3;
            int z3Target = (totalRooms - z1Target) / 3;
            int[] targetSizes = { 0, z1Target, z2Target, z3Target, 9999 };
            int[] currentSizes = { 0, 0, 0, 0, 0 };

            List<Room>[] zoneFrontiers = new List<Room>[5];
            for (int i = 1; i <= 4; i++) zoneFrontiers[i] = new List<Room>();

            Room startRoom = Grid[(0, 0)];
            startRoom.ZoneId = 1;//start room is always zone 1
            zoneFrontiers[1].Add(startRoom);
            currentSizes[1]++;

            finishRoom.ZoneId = 4;//finish room is always in zone 4
            zoneFrontiers[4].Add(finishRoom);
            currentSizes[4]++;

            int minDistance = size / seedDistanceDivisor;
            for (int i = 2; i <= 3; i++)
            {
                Room candidate;
                bool isTooClose;
                int safetyNet = 0;
                do
                {
                    int rx = rng.Next(-bound, bound + 1);
                    int ry = rng.Next(-bound, bound + 1);
                    candidate = Grid[(rx, ry)];

                    isTooClose = false;
                    if (Math.Abs(candidate.X - startRoom.X) + Math.Abs(candidate.Y - startRoom.Y) < minDistance) isTooClose = true;
                    if (Math.Abs(candidate.X - finishRoom.X) + Math.Abs(candidate.Y - finishRoom.Y) < minDistance) isTooClose = true;
                    safetyNet++;
                }
                while ((candidate.ZoneId != 0 || isTooClose) && safetyNet < 100);

                candidate.ZoneId = i;
                zoneFrontiers[i].Add(candidate);
                currentSizes[i]++;
            }

            (int dx, int dy)[] directions = { (0, 1), (1, 0), (0, -1), (-1, 0) };
            bool stillGrowing = true;

            while (stillGrowing)
            {
                stillGrowing = false;
                List<int> zonesToExpand = new List<int> { 1, 2, 3, 4 }.OrderBy(x => rng.Next()).ToList();//4 zones

                foreach (int z in zonesToExpand)
                {
                    if (currentSizes[z] >= targetSizes[z] || zoneFrontiers[z].Count == 0) continue;

                    int fIndex = rng.Next(zoneFrontiers[z].Count);
                    Room expandFrom = zoneFrontiers[z][fIndex];

                    List<Room> unclaimedNeighbors = new List<Room>();
                    foreach (var dir in directions)
                    {
                        int nx = expandFrom.X + dir.dx;
                        int ny = expandFrom.Y + dir.dy;
                        if (Grid.ContainsKey((nx, ny)) && Grid[(nx, ny)].ZoneId == 0) unclaimedNeighbors.Add(Grid[(nx, ny)]);
                    }

                    if (unclaimedNeighbors.Count > 0)//still unclaimed rooms
                    {
                        Room claimed = unclaimedNeighbors[rng.Next(unclaimedNeighbors.Count)];
                        claimed.ZoneId = z;
                        zoneFrontiers[z].Add(claimed);
                        currentSizes[z]++;
                        stillGrowing = true;//continue
                    }
                    else
                    {
                        zoneFrontiers[z].RemoveAt(fIndex);
                    }
                }

                if (!stillGrowing && Grid.Values.Any(r => r.ZoneId == 0))//if growing has stopped and a room doesnt have a zone
                {
                    bool fixedOrphan = false;
                    foreach (var orphan in Grid.Values.Where(r => r.ZoneId == 0).ToList())
                    {
                        foreach (var dir in directions)
                        {
                            int nx = orphan.X + dir.dx;
                            int ny = orphan.Y + dir.dy;

                            if (Grid.ContainsKey((nx, ny)) && Grid[(nx, ny)].ZoneId != 0)//give room its neighbours zone
                            {
                                orphan.ZoneId = Grid[(nx, ny)].ZoneId;
                                fixedOrphan = true;
                                break;
                            }
                        }
                    }

                    if (fixedOrphan) stillGrowing = true;//one more time for safety
                    else break;
                }
            }


            //part2 the route
            List<KeyType> sequence = new List<KeyType> { KeyType.Red, KeyType.Blue, KeyType.Green, KeyType.Yellow }.OrderBy(x => rng.Next()).ToList();//sequence of zones
            ZoneThemes.Clear();
            for (int i = 1; i <= 4; i++) ZoneThemes[i] = sequence[i - 1];

            Room[] keyRooms = new Room[5];//disperse keys, one of each strong in their own zone
            keyRooms[0] = startRoom;
            for (int i = 1; i <= 4; i++)
            {
                List<Room> zRooms = Grid.Values.Where(r => r.ZoneId == i && !r.IsFinish && r.Item == null && r != startRoom).ToList();
                List<Room> innerRooms = zRooms.Where(r => r.Doors.Values.All(d => d.RoomA.ZoneId == i && d.RoomB.ZoneId == i)).ToList();

                if (innerRooms.Count > 0) keyRooms[i] = innerRooms[rng.Next(innerRooms.Count)];
                else if (zRooms.Count > 0) keyRooms[i] = zRooms[rng.Next(zRooms.Count)];
                else
                {
                    List<Room> globalEmpty = Grid.Values.Where(r => !r.IsFinish && r.Item == null && r != startRoom).ToList();
                    keyRooms[i] = globalEmpty[rng.Next(globalEmpty.Count)];
                }
                keyRooms[i].Item = sequence[i - 1];
            }


            Dictionary<Door, List<KeyType>> safeThreadDoors = new Dictionary<Door, List<KeyType>>();//dictionary to remeber the current keys avaliable at a door point

            List<Door> CarvePath(Room start, Room target)
            {
                Dictionary<Room, Door> parentDoor = new Dictionary<Room, Door>();
                Dictionary<Room, Room> parentRoom = new Dictionary<Room, Room>();
                Queue<Room> q = new Queue<Room>();
                HashSet<Room> visited = new HashSet<Room>();

                q.Enqueue(start);
                visited.Add(start);

                while (q.Count > 0)
                {
                    Room curr = q.Dequeue();
                    if (curr == target) break;

                    var doors = curr.Doors.Values.OrderBy(x => rng.Next()).ToList();
                    foreach (Door d in doors)
                    {
                        Room next = (d.RoomA == curr) ? d.RoomB : d.RoomA;

                        if (next == finishRoom && target != finishRoom) continue;//cant go through finish room

                        if (!visited.Contains(next))
                        {
                            visited.Add(next);
                            parentRoom[next] = curr;
                            parentDoor[next] = d;
                            q.Enqueue(next);
                        }
                    }
                }

                List<Door> path = new List<Door>();
                Room step = target;
                while (step != start && parentRoom.ContainsKey(step))
                {
                    path.Add(parentDoor[step]);
                    step = parentRoom[step];
                }
                return path;
            }

            for (int tier = 1; tier <= 4; tier++)//connections and key assigning
            {
                List<KeyType> availableKeys = sequence.Take(tier - 1).ToList();//what keys avalaibale
                foreach (Door d in CarvePath(keyRooms[tier - 1], keyRooms[tier]))
                {
                    if (!safeThreadDoors.ContainsKey(d)) safeThreadDoors[d] = availableKeys;//if a door appears in the route twice, its first appearanece is teh resitriction
                }
            }

            List<KeyType> allKeys = sequence.Take(4).ToList();
            foreach (Door d in CarvePath(keyRooms[4], finishRoom))
            {
                if (!safeThreadDoors.ContainsKey(d)) safeThreadDoors[d] = allKeys;
            }


            //applying locks outside route
            HashSet<Door> uniqueDoors = new HashSet<Door>();
            foreach (var room in Grid.Values) foreach (var door in room.Doors.Values) uniqueDoors.Add(door);

            foreach (Door door in uniqueDoors)
            {
                int zA = door.RoomA.ZoneId;
                int zB = door.RoomB.ZoneId;
                int z = (zA == zB) ? zA : Math.Min(zA, zB);
                if (z == 0) z = 1;

                if (safeThreadDoors.ContainsKey(door))
                {
                    List<KeyType> chronologicallyAllowedKeys = safeThreadDoors[door];

                    int localWebPrev = webPreviousKeyChance;
                    int localWebOpen = webOpenChance;

                    if (chronologicallyAllowedKeys.Count == 0)
                    {
                        localWebOpen += localWebPrev;
                        localWebPrev = 0;
                    }

                    int roll = rng.Next(100);
                    if (roll < localWebPrev) { door.RequiredKey = chronologicallyAllowedKeys[rng.Next(chronologicallyAllowedKeys.Count)]; door.IsLocked = true; }
                    else { door.RequiredKey = KeyType.None; door.IsLocked = false; }
                }
                else
                {
                    //per zone door assign for non essential route doors
                    KeyType primaryLock = sequence[z - 1];
                    List<KeyType> previousLocks = sequence.Take(z - 1).ToList();
                    List<KeyType> futureLocks = sequence.Skip(z).ToList();

                    int localPrim = chancePrimaryLock;
                    int localPrev = chancePreviousLock;
                    int localCons = chanceConsumable;
                    int localChaos = chanceChaos;
                    int localOpen = chanceOpen;

                    if (previousLocks.Count == 0) { localOpen += localPrev; localPrev = 0; }
                    if (futureLocks.Count == 0) { localOpen += localChaos; localChaos = 0; }

                    int roll = rng.Next(100);
                    if (roll < localPrim) { door.RequiredKey = primaryLock; door.IsLocked = true; }
                    else if (roll < localPrim + localPrev) { door.RequiredKey = previousLocks[rng.Next(previousLocks.Count)]; door.IsLocked = true; }
                    else if (roll < localPrim + localPrev + localCons) { door.RequiredKey = KeyType.Silver; door.IsLocked = true; door.IsConsumable = true; }
                    else if (roll < localPrim + localPrev + localCons + localChaos) { door.RequiredKey = futureLocks[rng.Next(futureLocks.Count)]; door.IsLocked = true; }
                    else { door.RequiredKey = KeyType.None; door.IsLocked = false; }
                }
            }

            
            KeyType finalKey = sequence[3];
            foreach (Door d in finishRoom.Doors.Values)//every finish room door is locked with the final zone's key
            {
                d.RequiredKey = finalKey;
                d.IsLocked = true;
                d.IsConsumable = false;
            }

            List<Room> emptyRooms = Grid.Values.Where(r => !r.IsFinish && r.Item == null && r != startRoom).ToList();//scatter consumables anywhere, promotes sequences breaks and exploring

            for (int i = 0; i < lockpickCount; i++)
            {
                if (emptyRooms.Count > 0) { int index = rng.Next(emptyRooms.Count); emptyRooms[index].Item = KeyType.Lockpick; emptyRooms.RemoveAt(index); }
            }
            for (int i = 0; i < silverCount; i++)
            {
                if (emptyRooms.Count > 0) { int index = rng.Next(emptyRooms.Count); emptyRooms[index].Item = KeyType.Silver; emptyRooms.RemoveAt(index); }
            }
        }

        public void GenerateMode2_RandTilBeat(int size, int subSeed)//has a set lock and consumable chance and 1 of each strong key, randomises until a beatable seed is produced, the sequence of checks and final product can be replecated accuratly with seeds
        {
            GenerateEmptyGrid(size);//rooms and doors
            SetStartAndGoal(size, subSeed);

            Random rng = new Random(subSeed);

            ItemChance = 10;//x% chance for consumables tune later
            LockChance = 80;//x% chance for locks

            List<Room> availableRooms = Grid.Values.Where(r => !r.IsFinish).ToList();//all rooms except finish line

            List<KeyType> strongKeys = new List<KeyType> { KeyType.Red, KeyType.Blue, KeyType.Green, KeyType.Yellow };
            foreach (KeyType key in strongKeys)//scatter one of each strong key
            {
                if (availableRooms.Count > 0)
                {
                    int index = rng.Next(availableRooms.Count);
                    availableRooms[index].Item = key;
                    availableRooms.RemoveAt(index);//remove room, cant have 2 items
                }
            }

            List<KeyType> consumables = new List<KeyType> { KeyType.Silver, KeyType.Lockpick };//consumables in the leftover rooms
            foreach (var room in availableRooms)
            {
                if (rng.Next(100) < ItemChance)
                {
                    room.Item = consumables[rng.Next(consumables.Count)];
                }
            }

            HashSet<Door> uniqueDoors = new HashSet<Door>();//locks on doors
            foreach (var room in Grid.Values)
            {
                foreach (var door in room.Doors.Values)
                {
                    uniqueDoors.Add(door);
                }
            }

            List<KeyType> validLocks = new List<KeyType> { KeyType.Red, KeyType.Blue, KeyType.Green, KeyType.Yellow, KeyType.Silver };
            foreach (var door in uniqueDoors)
            {
                if (rng.Next(100) < LockChance)
                {
                    door.RequiredKey = validLocks[rng.Next(validLocks.Count)];
                    door.IsLocked = true;
                    door.IsConsumable = (door.RequiredKey == KeyType.Silver);
                }
            }
        }

        public ValidationReport GenerateMode2_ValidatedScramble(int size, int masterSeed, IProgress<int> progress, CancellationToken cancelToken)//basically just runs the validation of the random generation
        {
            Random masterRng = new Random(masterSeed);
            ValidationEngine validator = new ValidationEngine();
            int attempts = 0;

            while (true)
            {
                if (cancelToken.IsCancellationRequested) return null;//can be cancelled

                attempts++;
                if (attempts % 10 == 0) progress?.Report(attempts);//update every ten

                int subSeed = masterRng.Next();
                GenerateMode2_RandTilBeat(size, subSeed);//generate a new one

                ValidationReport report = validator.CheckIfBeatable(this, false, null, null, cancelToken);

                if (report == null) return null;

                if (report.IsBeatable)//seed is beatable
                {
                    report.GenerationAttempts = attempts;
                    LastValidSubSeed = subSeed;
                    return report;
                }
            }
        }


        public void GenerateMode3_Chaos(int size, int seed)//toatally random (door lock chance was tuned), door lock and item chance is random and there can be any amount of any key, not always beatable
        {
            GenerateEmptyGrid(size);//make map
            SetStartAndGoal(size, seed);//set finish

            Random rng = new Random(seed);//only one random
            Array allKeyTypes = Enum.GetValues(typeof(KeyType));//key types
            ItemChance = rng.Next(0, 100);//between 0 and 100% chance to have items in each room or a lock on each door, randomness
            LockChance = rng.Next(50, 100);

            List<KeyType> validKeys = new List<KeyType>();
            foreach (KeyType key in allKeyTypes)
            {
                if (key != KeyType.None) validKeys.Add(key);
            }

            foreach (var room in Grid.Values)
            {
                if (rng.Next(100) < ItemChance)//chance for any item in a room, even duplicates of strong keys
                {
                    room.Item = validKeys[rng.Next(validKeys.Count)];
                }
            }

            HashSet<Door> uniqueDoors = new HashSet<Door>();//making shared doors unique
            foreach (var room in Grid.Values)
            {
                foreach (var door in room.Doors.Values)
                {
                    uniqueDoors.Add(door);
                }
            }
            List<KeyType> validLocks = new List<KeyType> { KeyType.Red, KeyType.Blue, KeyType.Green, KeyType.Yellow, KeyType.Silver };

            foreach (var door in uniqueDoors)
            {
                if (rng.Next(100) < LockChance) //chance to lock
                {
                    door.RequiredKey = validLocks[rng.Next(validLocks.Count)];
                    door.IsLocked = true;
                    door.IsConsumable = (door.RequiredKey == KeyType.Silver);
                }
            }
        }

    }
}
