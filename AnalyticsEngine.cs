using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomNavigationAlgorithm
{
    internal class AnalyticsEngine
    {
        private class AnalyticsState
        {
            public Room CurrentRoom { get; set; }
            public List<KeyType> Inventory { get; set; } = new List<KeyType>();
            public HashSet<Room> PickedUpItems { get; set; } = new HashSet<Room>();
            public HashSet<Door> UnlockedDoors { get; set; } = new HashSet<Door>();
            public HashSet<Room> VisitedRooms { get; set; } = new HashSet<Room>();

            public AnalyticsState Parent { get; set; }
            public string ActionMessage { get; set; }
            public int StepCount { get; set; }
            public int TimeSinceProgress { get; set; } = 0;

            public string GetBasicHash()
            {
                var sortedInv = Inventory.OrderBy(k => k).Select(k => k.ToString());
                var sortedUnl = UnlockedDoors.OrderBy(d => Math.Min(d.RoomA.X, d.RoomB.X)).Select(d => $"{Math.Min(d.RoomA.X, d.RoomB.X)},{Math.Min(d.RoomA.Y, d.RoomB.Y)}");
                return $"R:{CurrentRoom.X},{CurrentRoom.Y}|I:{string.Join(",", sortedInv)}|U:{string.Join(";", sortedUnl)}";
            }

            public string GetCompletionHash()
            {
                var sortedVis = VisitedRooms.OrderBy(r => r.X).ThenBy(r => r.Y).Select(r => $"{r.X},{r.Y}");
                return GetBasicHash() + $"|V:{string.Join(";", sortedVis)}";
            }

            public AnalyticsState Clone()
            {
                return new AnalyticsState
                {
                    CurrentRoom = this.CurrentRoom,
                    Inventory = new List<KeyType>(this.Inventory),
                    PickedUpItems = new HashSet<Room>(this.PickedUpItems),
                    UnlockedDoors = new HashSet<Door>(this.UnlockedDoors),
                    VisitedRooms = new HashSet<Room>(this.VisitedRooms),
                    Parent = this,
                    StepCount = this.StepCount + 1,
                    TimeSinceProgress = this.TimeSinceProgress
                };
            }

            public string ReconstructSpoilerLog(string title)
            {
                List<string> steps = new List<string>();
                AnalyticsState curr = this;
                while (curr != null)
                {
                    if (!string.IsNullOrEmpty(curr.ActionMessage)) steps.Insert(0, curr.ActionMessage);
                    curr = curr.Parent;
                }

                if (steps.Count > 0 && !steps.Last().Contains("Goal Reached") && title != "100% Explorable")//goal reached doesnt matter in 100% explorable
                {
                    steps.Add("Goal Reached");
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{title.ToUpper()} SPOILER LOG");
                sb.AppendLine($"Total Moves: {StepCount}");
                sb.AppendLine($"Total Items Picked Up: {PickedUpItems.Count}");
                sb.AppendLine($"Total Doors Unlocked: {UnlockedDoors.Count}\n");

                foreach (string step in steps) sb.AppendLine(step);
                return sb.ToString();
            }
        }

        internal class BasicAnalyticsReport
        {
            public bool Is100PercentExplorable { get; set; }
            public string RouteExplorable { get; set; } = "No valid route found.";
            public string RouteSteps { get; set; } = "No valid route found.";
            public string RouteLocks { get; set; } = "No valid route found.";
        }

        internal BasicAnalyticsReport RunBasicAnalytics(MapGenerator map, CancellationToken token)
        {
            BasicAnalyticsReport report = new BasicAnalyticsReport();
            Room startRoom = map.Grid.Values.FirstOrDefault(r => r.X == 0 && r.Y == 0);
            if (startRoom == null) return report;

            int totalRoomsOnMap = map.Grid.Values.Count(r => r.Doors.Count > 0 || r == startRoom);

            report.RouteSteps = GetLeastSteps(startRoom, totalRoomsOnMap, token);//least steps
            if (report.RouteSteps == "Cancelled.") return null;

            report.RouteLocks = GetLeastLocks(startRoom, totalRoomsOnMap, token);//least locks
            if (report.RouteLocks == "Cancelled.") return null;

            return report;
        }

        internal string Run100PercentCompletion(MapGenerator map, CancellationToken token)
        {
            Room startRoom = map.Grid.Values.FirstOrDefault(r => r.X == 0 && r.Y == 0);
            if (startRoom == null) return "No valid route found.";

            int totalRooms = map.Grid.Values.Count(r => r.Doors.Count > 0 || r == startRoom);
            int totalItems = map.Grid.Values.Count(r => r.Item != null && r.Item != KeyType.None);
            int totalLocked = map.Grid.Values.SelectMany(r => r.Doors.Values).Distinct().Count(d => d.IsLocked);

            return Get100PercentCompletion(startRoom, totalRooms - 1, totalItems, totalLocked, token);
        }
        internal string RunExplorableAnalytics(MapGenerator map, CancellationToken token)
        {
            Room startRoom = map.Grid.Values.FirstOrDefault(r => r.X == 0 && r.Y == 0);
            if (startRoom == null) return "No valid route found.";

            int totalRoomsOnMap = map.Grid.Values.Count(r => r.Doors.Count > 0 || r == startRoom);
            return Get100PercentExplorable(startRoom, totalRoomsOnMap, token);
        }
        private string Get100PercentExplorable(Room start, int targetRooms, CancellationToken token)
        {
            int maxPossibleScore = (targetRooms * 2) + 50;
            List<Queue<AnalyticsState>> priorityBuckets = new List<Queue<AnalyticsState>>();
            for (int i = 0; i <= maxPossibleScore; i++) priorityBuckets.Add(new Queue<AnalyticsState>());

            Dictionary<string, int> bestScores = new Dictionary<string, int>();

            var startState = CreateStartState(start);
            priorityBuckets[1].Enqueue(startState);

            while (true)
            {
                if (token.IsCancellationRequested) return "Cancelled.";

                int highestPopulatedBucket = -1;
                for (int i = priorityBuckets.Count - 1; i >= 0; i--)
                {
                    if (priorityBuckets[i].Count > 0)
                    {
                        highestPopulatedBucket = i;
                        break;
                    }
                }

                if (highestPopulatedBucket == -1) break;

                var curr = priorityBuckets[highestPopulatedBucket].Dequeue();

                foreach (var next in GetValidMoves(curr, true))
                {
                    if (next.CurrentRoom.IsFinish)
                    {
                        if (curr.VisitedRooms.Count == targetRooms - 1 && !curr.VisitedRooms.Contains(next.CurrentRoom))//finish line must be the last room and end the 100% completion
                        {
                            return next.ReconstructSpoilerLog("100% Explorable");
                        }
                        continue;
                    }

                    if (next.VisitedRooms.Count == targetRooms)
                    {
                        return next.ReconstructSpoilerLog("100% Explorable");
                    }

                    if (next.TimeSinceProgress > targetRooms) continue;

                    string pruneKey = next.GetCompletionHash();
                    if (!bestScores.ContainsKey(pruneKey) || next.StepCount < bestScores[pruneKey])
                    {
                        bestScores[pruneKey] = next.StepCount;
                        int progressScore = (next.VisitedRooms.Count * 2) + next.PickedUpItems.Count;
                        while (progressScore >= priorityBuckets.Count)//stops chaos mode from breaking on checks
                        {
                            priorityBuckets.Add(new Queue<AnalyticsState>());
                        }
                        priorityBuckets[progressScore].Enqueue(next);
                    }
                }
            }
            return "Cannot reach every room in the map.";
        }

        private string GetLeastSteps(Room start, int limit, CancellationToken token)
        {
            Queue<AnalyticsState> queue = new Queue<AnalyticsState>();
            HashSet<string> visited = new HashSet<string>();

            var startState = CreateStartState(start);
            queue.Enqueue(startState);
            visited.Add(startState.GetBasicHash());

            while (queue.Count > 0)
            {
                if (token.IsCancellationRequested) return "Cancelled.";
                var curr = queue.Dequeue();

                if (curr.CurrentRoom.IsFinish) return curr.ReconstructSpoilerLog("Least Steps");

                foreach (var next in GetValidMoves(curr, false))
                {
                    if (next.TimeSinceProgress > limit) continue;

                    string hash = next.GetBasicHash();
                    if (!visited.Contains(hash))
                    {
                        visited.Add(hash);
                        queue.Enqueue(next);
                    }
                }
            }
            return "Impossible to beat.";
        }

        private string GetLeastLocks(Room start, int limit, CancellationToken token)
        {
            List<Queue<AnalyticsState>> buckets = new List<Queue<AnalyticsState>>();
            for (int i = 0; i < 100; i++) buckets.Add(new Queue<AnalyticsState>());
            HashSet<string> visited = new HashSet<string>();

            buckets[0].Enqueue(CreateStartState(start));

            for (int cost = 0; cost < buckets.Count; cost++)
            {
                while (buckets[cost].Count > 0)
                {
                    if (token.IsCancellationRequested) return "Cancelled.";
                    var curr = buckets[cost].Dequeue();

                    if (curr.CurrentRoom.IsFinish) return curr.ReconstructSpoilerLog("Least Locks");

                    foreach (var next in GetValidMoves(curr, false))
                    {
                        if (next.TimeSinceProgress > limit) continue;

                        string hash = next.GetBasicHash();
                        if (!visited.Contains(hash))
                        {
                            visited.Add(hash);
                            buckets[next.UnlockedDoors.Count].Enqueue(next);
                        }
                    }
                }
            }
            return "Impossible to beat.";
        }

        private string Get100PercentCompletion(Room start, int targetRooms, int targetItems, int targetLocks, CancellationToken token)
        {
            int maxPossibleScore = targetItems + targetLocks + 1;
            List<Queue<AnalyticsState>> priorityBuckets = new List<Queue<AnalyticsState>>();
            for (int i = 0; i <= maxPossibleScore; i++) priorityBuckets.Add(new Queue<AnalyticsState>());

            Dictionary<string, int> bestScores = new Dictionary<string, int>();

            var startState = CreateStartState(start);
            priorityBuckets[0].Enqueue(startState);

            while (true)
            {
                if (token.IsCancellationRequested) return "Cancelled.";

                int highestPopulatedBucket = -1;
                for (int i = priorityBuckets.Count - 1; i >= 0; i--)
                {
                    if (priorityBuckets[i].Count > 0)
                    {
                        highestPopulatedBucket = i;
                        break;
                    }
                }

                if (highestPopulatedBucket == -1) break;

                var curr = priorityBuckets[highestPopulatedBucket].Dequeue();

                foreach (var next in GetValidMoves(curr, true))
                {
                    if (next.CurrentRoom.IsFinish)
                    {
                        if (curr.PickedUpItems.Count == targetItems && curr.VisitedRooms.Count >= targetRooms && curr.UnlockedDoors.Count == targetLocks)
                        {
                            return next.ReconstructSpoilerLog("100% Completion");
                        }
                        continue;
                    }

                    if (next.TimeSinceProgress > targetRooms) continue;

                    string pruneKey = next.GetCompletionHash();
                    if (!bestScores.ContainsKey(pruneKey) || next.StepCount < bestScores[pruneKey])
                    {
                        bestScores[pruneKey] = next.StepCount;
                        int progressScore = next.PickedUpItems.Count + next.UnlockedDoors.Count;
                        priorityBuckets[progressScore].Enqueue(next);
                    }
                }
            }
            return "Cannot reach finish befire 100% clearing the map";
        }

        private AnalyticsState CreateStartState(Room start)
        {
            var s = new AnalyticsState { CurrentRoom = start, StepCount = 0, ActionMessage = $"Spawned at [{start.X},{start.Y}]." };
            s.VisitedRooms.Add(start);
            if (start.Item != null && start.Item != KeyType.None)
            {
                s.Inventory.Add(start.Item.Value);
                s.PickedUpItems.Add(start);
                s.ActionMessage += $" Picked up {start.Item.Value}.";
            }
            return s;
        }

        private List<AnalyticsState> GetValidMoves(AnalyticsState curr, bool is100PercentRun)
        {
            List<AnalyticsState> moves = new List<AnalyticsState>();

            foreach (var door in curr.CurrentRoom.Doors.Values)
            {
                Room nextRoom = (door.RoomA == curr.CurrentRoom) ? door.RoomB : door.RoomA;
                string dir = "";
                if (nextRoom.X > curr.CurrentRoom.X) dir = "East";
                else if (nextRoom.X < curr.CurrentRoom.X) dir = "West";
                else if (nextRoom.Y > curr.CurrentRoom.Y) dir = "North";
                else if (nextRoom.Y < curr.CurrentRoom.Y) dir = "South";

                if (is100PercentRun && nextRoom.IsFinish && door.IsLocked && !curr.UnlockedDoors.Contains(door))//unlocking a door used to send you through it but because 100% completion needs all doors unlocked before finishing a way to unlock a door without traversing was implemented
                {
                    bool canUnlock = false;
                    bool consumed = false;
                    KeyType usedKey = KeyType.None;

                    if (!door.IsConsumable && curr.Inventory.Contains(door.RequiredKey)) canUnlock = true;
                    else if (door.IsConsumable && curr.Inventory.Contains(KeyType.Silver)) { canUnlock = true; consumed = true; usedKey = KeyType.Silver; }
                    else if (door.IsConsumable && curr.Inventory.Contains(KeyType.Lockpick)) { canUnlock = true; consumed = true; usedKey = KeyType.Lockpick; }

                    if (canUnlock)
                    {
                        var unlockState = curr.Clone();
                        string itemUsed = consumed ? usedKey.ToString() : door.RequiredKey.ToString() + " Key";
                        unlockState.ActionMessage = $"Stayed in [{curr.CurrentRoom.X},{curr.CurrentRoom.Y}] and unlocked {dir} door with {itemUsed}.";
                        if (consumed) unlockState.Inventory.Remove(usedKey);
                        unlockState.UnlockedDoors.Add(door);

                        int oldScore = curr.VisitedRooms.Count + curr.PickedUpItems.Count + curr.UnlockedDoors.Count;
                        int newScore = unlockState.VisitedRooms.Count + unlockState.PickedUpItems.Count + unlockState.UnlockedDoors.Count;
                        unlockState.TimeSinceProgress = (newScore > oldScore) ? 0 : curr.TimeSinceProgress + 1;

                        moves.Add(unlockState);
                    }
                }

                bool canEnter = false;
                bool moveConsumed = false;
                KeyType moveUsedKey = KeyType.None;
                string action = $"Moved {dir} to [{nextRoom.X},{nextRoom.Y}]";
                bool unlockingDuringMove = false;

                if (!door.IsLocked || curr.UnlockedDoors.Contains(door)) canEnter = true;
                else if (!door.IsConsumable && curr.Inventory.Contains(door.RequiredKey))
                {
                    canEnter = true; unlockingDuringMove = true;
                    action += $" (Unlocked with {door.RequiredKey} Key)";
                }
                else if (door.IsConsumable && curr.Inventory.Contains(KeyType.Silver))
                {
                    canEnter = true; moveConsumed = true; moveUsedKey = KeyType.Silver; unlockingDuringMove = true;
                    action += $" (Unlocked with Silver Key)";
                }
                else if (door.IsConsumable && curr.Inventory.Contains(KeyType.Lockpick))
                {
                    canEnter = true; moveConsumed = true; moveUsedKey = KeyType.Lockpick; unlockingDuringMove = true;
                    action += $" (Unlocked with Lockpick)";
                }

                if (canEnter)
                {
                    var next = curr.Clone();
                    next.CurrentRoom = nextRoom;
                    next.ActionMessage = action + ".";
                    next.VisitedRooms.Add(nextRoom);

                    if (moveConsumed)
                    {
                        next.Inventory.Remove(moveUsedKey);
                        next.UnlockedDoors.Add(door);
                    }
                    else if (unlockingDuringMove) next.UnlockedDoors.Add(door);

                    if (nextRoom.Item != null && nextRoom.Item != KeyType.None && !next.PickedUpItems.Contains(nextRoom))
                    {
                        next.Inventory.Add(nextRoom.Item.Value);
                        next.PickedUpItems.Add(nextRoom);
                        next.ActionMessage += $" Picked up {nextRoom.Item.Value}.";
                    }

                    int oldScore = curr.VisitedRooms.Count + curr.PickedUpItems.Count + curr.UnlockedDoors.Count;
                    int newScore = next.VisitedRooms.Count + next.PickedUpItems.Count + next.UnlockedDoors.Count;
                    next.TimeSinceProgress = (newScore > oldScore) ? 0 : curr.TimeSinceProgress + 1;

                    moves.Add(next);
                }
            }
            return moves;
        }
    }
}
