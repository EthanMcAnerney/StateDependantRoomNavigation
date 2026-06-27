using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RoomNavigationAlgorithm
{
    public partial class MainWindow : Window
    {
        private MapGenerator currentMap;
        private Player player1;
        private int activeSeed;
        private int activeGridSize;
        private ValidationReport seedReport;
        private ValidationReport currentRunReport;
        private CancellationTokenSource _generationCancelToken;
        private int activeSubSeed;
        private bool isMapRevealed = false;
        private System.Threading.CancellationTokenSource monteCarloCts;
        private string cachedRoute100 = "";
        private string cachedRouteSteps = "";
        private string cachedRouteLocks = "";
        private SpoilerLogWindow _currentLogWindow;
        private CancellationTokenSource _analyticsCancelToken;
        private string cachedRouteExplorable = "";


        public MainWindow()
        {
            InitializeComponent();
        }

        private void SeedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)//numbers only
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void PurposeButton_Click(object sender, RoutedEventArgs e)//background about the project
        {
            string purposeText =
                "The purpose of this project was to solve and explore the architectural challenge of state dependent procedural generation. I was inspired by the routing complexities of " +
                "video game randomizers that require logic (for glitchless playthroughs) such as Ocarina of Time." +
                " This project models mutating topological graphs where inventory " +
                "changes dynamically alter traversal rules. The system was engineered to solve the Traveling Salesperson Problem within these " +
                "evolving constraints, utilizing state memoization and heuristic pruning to bypass combinatorial " +
                "explosions. The result is an engine that constructs highly constrained logic mazes and simulates parallel timelines to calculate " +
                "perfect completion routes.";

            MessageBox.Show(purposeText, "Project Purpose", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RulesButton_Click(object sender, RoutedEventArgs e)//rules of the project
        {
            string rulesText =
                "The goal is to traverse to the finish room, marked in green on the map. \n" +
                "The player will start in the middle of the map and the finish room will be along the map perimeter.\n" +
                "Rooms can be traversed by interacting with the doorways in each room located in the cardinal directions.\n" +
                "Doors can be locked or open, when locked doors will require a specific key to open:\n" +
                "The keys:\nThere are strong keys which can open unlimited doors of thier colour: Red, Blue, Green, Yellow\n" +
                "Then there are consumables which can only open one door before being consumed\nThere is a consumable key: Silver Key" +
                "\nThere is a consumable item: Lockpick (It can open any door and acts as a wildcard)\n" +
                "Mode A: Zones\nThis mode is always beatable, there is one of each strong key which has its own zone on the map, inside this zone " +
                "the locks of that key will be more prominent, making it challenging until beaten, after which it is possible to explore the next random zone " +
                "before finally finishing. This generation allows for freedom of choices without strict pathways, this is further supported by the lockpick.\n" +
                "Mode B: Random Scamble With Validation\n" +
                "This mode only allows one of each strong key and has a set consumable and door locking chance, it randomly places locks and keys and then checks if it's beatable, if not it trys again. " +
                "This method of generation allows for interesting maps, but may prove restricted or challenging at other times, every map generated can be replicated with the same seed and is beatable since it is pre validated.\n" +
                "Mode C: Chaos\n This mode has very little rules. The finish room is still on the border, and lock chance has a tuned range but other than that there can be any item anywhere and any lock anywhere, maps may not be beatable, this is to show why rules and design are needed and allow for more interesting validation tests to explore.";

            MessageBox.Show(rulesText, "Game Rules", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)//generate button
        {
            ResetAnalyticsUI();
            currentRunReport = null;
            seedReport = null;

            SeedStatusText.Text = "? Unknown";
            SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);

            CurrentStatusText.Text = "? Unknown";
            CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);

            if (string.IsNullOrWhiteSpace(SeedTextBox.Text))
            {
                MessageBox.Show("Please enter a numeric seed.", "Missing Seed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int currentSeed = int.Parse(SeedTextBox.Text);
            int gridSize = SizeComboBox.SelectedIndex == 0 ? 7 : SizeComboBox.SelectedIndex == 1 ? 11 : 15;//getting grid size
            activeSeed = currentSeed;
            activeGridSize = gridSize;

            currentMap = new MapGenerator();

            if (ModeComboBox.SelectedIndex == 0)//run the selected generation method
            {
                BtnGenerate.Content = "GENERATING...";
                BtnGenerate.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                BtnGenerate.IsEnabled = false;
                BtnGenerate.IsHitTestVisible = false;

                try
                {
                    currentMap.GenerateMode1_Zones(gridSize, activeSeed);//generate mode 1

                    ValidationEngine engine = new ValidationEngine();//run validator to get log data
                    seedReport = await Task.Run(() => engine.CheckIfBeatable(currentMap, false));

                    if (seedReport != null && seedReport.IsBeatable)
                    {
                        SeedDisplayBlock.Text = $"Seed: {activeSeed}";
                        ModeDisplayBlock.Text = "Mode A: Zones";
                        DensityDisplayBlock.Text = "Density: Standard Zone Distributions";

                        SeedStatusText.Text = "Possible";
                        SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);

                       
                        MainMenuPanel.Visibility = Visibility.Collapsed;
                        GamePanel.Visibility = Visibility.Visible;

                        player1 = new Player();
                        player1.CurrentRoom = currentMap.Grid[(0, 0)];
                        UpdateRoomUI();
                    }
                    else//failsafe
                    {
                        MessageBox.Show("Mode A generated an unbeatable map. Report the seed please.", "Generation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    BtnGenerate.Content = "GENERATE MAP";
                    BtnGenerate.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                    BtnGenerate.IsEnabled = true;
                    BtnGenerate.IsHitTestVisible = true;
                }
            }
            else if (ModeComboBox.SelectedIndex == 1)//random then validate
            {
                BtnGenerate.Content = "GENERATING...";//UI to show generations
                BtnGenerate.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);

                BtnGenerate.IsEnabled = false;
                BtnGenerate.IsHitTestVisible = false;

                BtnCancel.Visibility = Visibility.Visible;
                TxtGenerateStatus.Text = "Attempts: 0";

                _generationCancelToken = new CancellationTokenSource();
                var progressReporter = new Progress<int>(value =>
                {
                    TxtGenerateStatus.Text = $"Attempts: {value}";
                });

                try//generate the maps in the background
                {
                    seedReport = await Task.Run(() => currentMap.GenerateMode2_ValidatedScramble(gridSize, activeSeed, progressReporter, _generationCancelToken.Token));

                    if (seedReport == null)//cancel check
                    {
                        TxtGenerateStatus.Text = "Generation aborted.";
                        return;
                    }

                    activeSubSeed = currentMap.LastValidSubSeed;//need for seed resets
                    SeedDisplayBlock.Text = $"Seed: {activeSeed}";
                    ModeDisplayBlock.Text = $"Mode B: (Found in {seedReport.GenerationAttempts} attempts)";
                    DensityDisplayBlock.Text = $"Items: {currentMap.ItemChance}% | Locks: {currentMap.LockChance}%";

                    SeedStatusText.Text = "Possible";
                    SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);



                    MainMenuPanel.Visibility = Visibility.Collapsed;
                    GamePanel.Visibility = Visibility.Visible;

                    player1 = new Player();
                    player1.CurrentRoom = currentMap.Grid[(0, 0)];
                    UpdateRoomUI();
                }
                finally
                {
                    BtnGenerate.Content = "GENERATE MAP";
                    BtnGenerate.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                    BtnGenerate.IsEnabled = true;
                    BtnGenerate.IsHitTestVisible = true;
                    BtnCancel.Visibility = Visibility.Collapsed;
                    _generationCancelToken?.Dispose();
                }
            }
            else if (ModeComboBox.SelectedIndex == 2)
            {
                currentMap.GenerateMode3_Chaos(gridSize, currentSeed);
                player1 = new Player();
                player1.CurrentRoom = currentMap.Grid[(0, 0)];

                SeedDisplayBlock.Text = $"Seed: {currentSeed}";
                ModeDisplayBlock.Text = "Mode: Chaos";
                DensityDisplayBlock.Text = $"Items: {currentMap.ItemChance}% | Locks: {currentMap.LockChance}%";
                MainMenuPanel.Visibility = Visibility.Collapsed;//transition ui
                GamePanel.Visibility = Visibility.Visible;
                UpdateRoomUI();
            }

        }
        private void UpdateRoomUI()
        {
            Room room = player1.CurrentRoom;
            CoordsDisplay.Text = $"Current Room: [{room.X}, {room.Y}]";
            player1.CurrentRoom.IsExplored = true;

            UpdateButton(BtnNorth, Direction.North, room);//update doors
            UpdateButton(BtnEast, Direction.East, room);
            UpdateButton(BtnSouth, Direction.South, room);
            UpdateButton(BtnWest, Direction.West, room);

            if (room.Item != null && room.Item != KeyType.None)//update item
            {
                ItemButton.Content = $"Pick up: {room.Item}";
                ItemButton.Visibility = Visibility.Visible;
            }
            else
            {
                ItemButton.Visibility = Visibility.Collapsed;
            }

            //update inventory
            int r = player1.Keys.ContainsKey(KeyType.Red) ? player1.Keys[KeyType.Red] : 0;
            int b = player1.Keys.ContainsKey(KeyType.Blue) ? player1.Keys[KeyType.Blue] : 0;
            int g = player1.Keys.ContainsKey(KeyType.Green) ? player1.Keys[KeyType.Green] : 0;
            int y = player1.Keys.ContainsKey(KeyType.Yellow) ? player1.Keys[KeyType.Yellow] : 0;
            int s = player1.Keys.ContainsKey(KeyType.Silver) ? player1.Keys[KeyType.Silver] : 0;
            int lp = player1.Keys.ContainsKey(KeyType.Lockpick) ? player1.Keys[KeyType.Lockpick] : 0;

            RadRed.Content = $"Red: {r}";
            RadBlue.Content = $"Blue: {b}";
            RadGreen.Content = $"Green: {g}";
            RadYellow.Content = $"Yellow: {y}";
            RadSilver.Content = $"Silver: {s}";
            RadLockpick.Content = $"Lockpicks: {lp}";

            RoomPanel.Background = player1.CurrentRoom.GetRoomColor();

            UpdateMinimap();
        }

        private void UpdateButton(Button btn, Direction dir, Room room)//door button text
        {
            if (room.Doors.ContainsKey(dir))
            {
                Door door = room.Doors[dir];
                btn.IsEnabled = true;

                btn.Background = GetDoorColor(door);

                if (door.IsLocked)
                {
                    btn.Content = $"{dir.ToString().ToUpper()}\n({door.RequiredKey} - Locked)";
                }
                else if (door.RequiredKey != KeyType.None)
                {
                    btn.Content = $"{dir.ToString().ToUpper()}\n({door.RequiredKey} - Open)";//doesnt turn into a hallway, helps to not blend rooms together
                    btn.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    btn.Content = $"{dir.ToString().ToUpper()}\n(Hallway)";
                    btn.Foreground = new SolidColorBrush(Colors.Black);
                }
            }
            else
            {
                btn.IsEnabled = false;
                btn.Content = $"{dir.ToString().ToUpper()}\n(Wall)";
                btn.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                btn.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            KeyType foundItem = player1.CurrentRoom.Item.Value;
            player1.PickUpItem();
            UpdateRoomUI();
        }

        private void ReturnToMenu_Click(object sender, RoutedEventArgs e)
        {
            SeedStatusText.Text = "? Unknown";
            SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);

            CurrentStatusText.Text = "? Unknown";
            CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);


            GamePanel.Visibility = Visibility.Collapsed;
            MainMenuPanel.Visibility = Visibility.Visible;
            currentRunReport = null;
            seedReport = null;
            TxtGenerateStatus.Text = "";
            TxtExplorable.Text = "Fully Explorable: ?";
            TxtCompletion.Text = "100% Clear: ?";
            TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
            TxtCompletion.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
        }

        private void CopySeed_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(SeedTextBox.Text);
            MessageBox.Show("Seed copied to clipboard", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetSeed_Click(object sender, RoutedEventArgs e)
        {
            var response = MessageBox.Show("Do you want to restart this seed? All inventory items and progress will be lost.", "Reset Seed", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (response == MessageBoxResult.Yes)
            {
                currentMap = new MapGenerator();

                if (ModeComboBox.SelectedIndex == 1)//reconstruct the map based on mode
                {
                    currentMap.GetType().GetProperty("LastValidSubSeed").SetValue(currentMap, activeSubSeed);//subseed needs to be used in the nonvalidated scramble for reproduceability
                    currentMap.GenerateMode2_RandTilBeat(activeGridSize, currentMap.LastValidSubSeed);

                    currentMap.GetType().GetProperty("LastValidSubSeed").SetValue(currentMap, activeSubSeed);//remember sub seed again just incase
                }
                else if (ModeComboBox.SelectedIndex == 0)//run mode a again
                {
                    currentMap.GenerateMode1_Zones(activeGridSize, activeSeed);
                }
                else//run mode c
                {
                    currentMap.GenerateMode3_Chaos(activeGridSize, activeSeed);
                }

                player1 = new Player();//new player at start room
                player1.CurrentRoom = currentMap.Grid[(0, 0)];

                RadRed.IsChecked = false;//UI restart, fixes key selection issue on restart
                RadBlue.IsChecked = false;
                RadGreen.IsChecked = false;
                RadYellow.IsChecked = false;
                RadSilver.IsChecked = false;
                RadLockpick.IsChecked = false;
                UpdateRoomUI();
                CurrentStatusText.Text = "? Unknown";
                CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
                currentRunReport = null;

                MessageBox.Show("Seed reset.", "Restarted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Inventory_Select(object sender, RoutedEventArgs e)
        {
            RadioButton clickedRad = sender as RadioButton;
            player1.SelectedKey = (KeyType)Enum.Parse(typeof(KeyType), clickedRad.Tag.ToString());
        }

        private void Door_Click(object sender, RoutedEventArgs e)
        {
            Button clickedBtn = sender as Button;
            Direction dir = clickedBtn == BtnNorth ? Direction.North : clickedBtn == BtnEast ? Direction.East : clickedBtn == BtnSouth ? Direction.South : Direction.West;

            if (!player1.CurrentRoom.Doors.ContainsKey(dir)) return;
            Door selectedDoor = player1.CurrentRoom.Doors[dir];

            if (!selectedDoor.IsLocked)//door is open
            {
                player1.CurrentRoom = selectedDoor.GetOtherRoom(player1.CurrentRoom);
                player1.CurrentRoom.IsExplored = true;
                UpdateRoomUI();

                if (player1.CurrentRoom.IsFinish)//finish check
                    MessageBox.Show("You found the Finish Line!", "VICTORY", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }
            //doors are locked
            if (player1.SelectedKey == null)//no key selected
            {
                MessageBox.Show("Please select a key from your inventory.", "No Key Equipped", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KeyType equipped = player1.SelectedKey.Value;
            KeyType required = selectedDoor.RequiredKey;

            if (!player1.HasKey(equipped))//no keys of selected type
            {
                MessageBox.Show($"You don't have any {equipped} keys in your inventory.", "Missing Item", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (equipped == required || equipped == KeyType.Lockpick)//is key compatabile/or is lockpick
            {
                if (equipped == KeyType.Silver || equipped == KeyType.Lockpick)//confirmation if its a consumable
                {
                    var response = MessageBox.Show($"Do you want to consume your {equipped} on this {required} Door?", "Confirm Use", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (response == MessageBoxResult.No) return;//stop if no
                }

                selectedDoor.Unlock(player1, equipped);//unlock the door, no check for strong keys
                CurrentStatusText.Text = "? Unknown";
                CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
                currentRunReport = null;
                UpdateRoomUI();
            }
            else
            {
                MessageBox.Show($"Incompatible key. A {equipped} Key cannot open a {required} Door.", "Wrong Key", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMinimap()
        {
            MinimapCanvas.Children.Clear();

            int roomSize = 20;
            int spacing = 6;
            int bound = activeGridSize / 2;

            MinimapCanvas.Width = activeGridSize * (roomSize + spacing);
            MinimapCanvas.Height = activeGridSize * (roomSize + spacing);

            foreach (var kvp in currentMap.Grid)
            {
                Room room = kvp.Value;

                double drawX = (room.X + bound) * (roomSize + spacing);
                double drawY = (-room.Y + bound) * (roomSize + spacing);

                if (room.IsExplored || isMapRevealed)//draw doors
                {
                    foreach (var doorKvp in room.Doors)
                    {
                        Direction dir = doorKvp.Key;
                        Door door = doorKvp.Value;

                        Rectangle doorRect = new Rectangle
                        {
                            Fill = GetDoorColor(door),
                            Width = (dir == Direction.North || dir == Direction.South) ? 8 : 4,
                            Height = (dir == Direction.East || dir == Direction.West) ? 8 : 4
                        };

                        double doorX = drawX;
                        double doorY = drawY;

                        if (dir == Direction.North) { doorX += 6; doorY -= 4; }
                        if (dir == Direction.South) { doorX += 6; doorY += roomSize; }
                        if (dir == Direction.East) { doorX += roomSize; doorY += 6; }
                        if (dir == Direction.West) { doorX -= 4; doorY += 6; }

                        Canvas.SetLeft(doorRect, doorX);
                        Canvas.SetTop(doorRect, doorY);
                        Canvas.SetZIndex(doorRect, 0); // Bottom Layer
                        MinimapCanvas.Children.Add(doorRect);
                    }
                }

                Rectangle rect = new Rectangle
                {
                    Width = roomSize,
                    Height = roomSize,
                    StrokeThickness = 1
                };

                if (!room.IsExplored && !room.IsFinish && !isMapRevealed)//hide unexplored rooms except finish room
                {
                    rect.Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                    rect.Stroke = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
                else
                {
                    if (room.IsFinish)
                    {
                        rect.Fill = new SolidColorBrush(Colors.LimeGreen);//light up finidh room
                    }
                    else
                    {
                        rect.Fill = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    }

                    rect.Stroke = new SolidColorBrush(Colors.Black);

                    if (room == player1.CurrentRoom)
                    {
                        rect.Stroke = new SolidColorBrush(Colors.White);
                        rect.StrokeThickness = 3;
                    }
                    if (isMapRevealed && ModeComboBox.SelectedIndex == 0 && room.ZoneId > 0 && currentMap.ZoneThemes.ContainsKey(room.ZoneId))//for seeing zones in mode a, debugging
                    {
                        KeyType theme = currentMap.ZoneThemes[room.ZoneId];
                        switch (theme)
                        {
                            case KeyType.Red: rect.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 200, 200)); break; // Light Red
                            case KeyType.Blue: rect.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 200, 200, 255)); break; // Light Blue
                            case KeyType.Green: rect.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 200, 255, 200)); break; // Light Green
                            case KeyType.Yellow: rect.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 200)); break; // Light Yellow
                        }
                    }
                }

                Canvas.SetLeft(rect, drawX);
                Canvas.SetTop(rect, drawY);
                Canvas.SetZIndex(rect, 1);
                MinimapCanvas.Children.Add(rect);

                if ((room.IsExplored || isMapRevealed) && room.Item != null && room.Item != KeyType.None)//items in rooms
                {
                    Ellipse itemDot = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = new SolidColorBrush(Colors.Gold),
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(itemDot, drawX + (roomSize / 2) - 5);
                    Canvas.SetTop(itemDot, drawY + (roomSize / 2) - 5);
                    Canvas.SetZIndex(itemDot, 2);//make it ontop of the room
                    MinimapCanvas.Children.Add(itemDot);
                }
            }
        }

        private SolidColorBrush GetDoorColor(Door door)
        {
            if (!door.IsLocked)
                return new SolidColorBrush(Colors.White);//door colour white if open ( or hallway)

            switch (door.RequiredKey)//colours for locked doors
            {
                case KeyType.Red: return new SolidColorBrush(Colors.Red);
                case KeyType.Blue: return new SolidColorBrush(Colors.DodgerBlue);
                case KeyType.Green: return new SolidColorBrush(Colors.LimeGreen);
                case KeyType.Yellow: return new SolidColorBrush(Colors.Yellow);
                case KeyType.Silver: return new SolidColorBrush(Colors.SlateGray);
                default: return new SolidColorBrush(Colors.DarkGray);
            }
        }

        private async void VerifySeed_Click(object sender, RoutedEventArgs e)
        {
            SeedStatusText.Text = "...";
            SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

            Button btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            MapGenerator pristineMap = new MapGenerator();//regenerate a fresh map for checking

            if (ModeComboBox.SelectedIndex == 1)
            {
                pristineMap.GenerateMode2_RandTilBeat(activeGridSize, activeSubSeed);
            }
            else if (ModeComboBox.SelectedIndex == 0)
            {
                pristineMap.GenerateMode1_Zones(activeGridSize, activeSeed);
            }
            else
            {
                pristineMap.GenerateMode3_Chaos(activeGridSize, activeSeed);
            }

            ValidationEngine engine = new ValidationEngine();

            seedReport = await Task.Run(() => engine.CheckIfBeatable(pristineMap, false));

            if (seedReport.IsBeatable)//ui update
            {
                SeedStatusText.Text = "Possible";
                SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);

            }
            else
            {
                SeedStatusText.Text = "Unbeatable";
                SeedStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private async void VerifyCurrent_Click(object sender, RoutedEventArgs e)
        {
            CurrentStatusText.Text = "...";
            CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

            Button btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            Room livePlayerRoom = player1.CurrentRoom;//get everything about the player's state, room, doors, inventory

            Dictionary<KeyType, int> livePlayerInv = new Dictionary<KeyType, int>(player1.Keys);
            ValidationEngine engine = new ValidationEngine();
            currentRunReport = await Task.Run(() => engine.CheckIfBeatable(currentMap, true, livePlayerRoom, livePlayerInv));

            if (currentRunReport.IsBeatable)
            {
                CurrentStatusText.Text = "Is Beatable";
                CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                CurrentStatusText.Text = "Softlocked";
                CurrentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private void SeedSpoiler_Click(object sender, RoutedEventArgs e)
        {
            if (seedReport == null)
            {
                MessageBox.Show("Please click 'Verify Seed' first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!seedReport.IsBeatable)
            {
                MessageBox.Show("This seed is unbeatable. No winning path exists.", "Unbeatable Seed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string header = $"SEED SPOILER LOG\nSeed: {activeSeed}\nGrid Size: {activeGridSize}x{activeGridSize}\nTotal Moves: {seedReport.TotalMoves}\n\n";
            string logContent = header + string.Join("\n", seedReport.WinningPath);

            SpoilerLogWindow spoilerWindow = new SpoilerLogWindow(logContent);
            spoilerWindow.Owner = this;//so it doesnt fall behind main window and can be used to guide easily
            spoilerWindow.Show();//show make it moveable
        }

        private void CurrentSpoiler_Click(object sender, RoutedEventArgs e)
        {
            if (currentRunReport == null)
            {
                MessageBox.Show("Please click 'Check State' first to generate the report for your current position.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!currentRunReport.IsBeatable)
            {
                MessageBox.Show("You are currently softlocked. No winning path exists from your current state.", "Softlocked", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string header = $"CURRENT STATE SPOILER LOG\n" +
                            $"Seed: {activeSeed}\n" +
                            $"Grid Size: {activeGridSize}x{activeGridSize}\n" +
                            $"Starting Room: [{player1.CurrentRoom.X},{player1.CurrentRoom.Y}]\n" +
                            $"Remaining Moves to Win: {currentRunReport.TotalMoves}\n\n";

            string logContent = header + string.Join("\n", currentRunReport.WinningPath);

            SpoilerLogWindow spoilerWindow = new SpoilerLogWindow(logContent);
            spoilerWindow.Owner = this;
            spoilerWindow.Show();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            BtnCancel.IsEnabled = false;
            BtnCancel.Content = "STOPPING...";

            if (_generationCancelToken != null)
            {
                _generationCancelToken.Cancel();
            }

            if (monteCarloCts != null)
            {
                monteCarloCts.Cancel();
            }
        }
        private void BtnRevealMap_Click(object sender, RoutedEventArgs e)
        {
            isMapRevealed = !isMapRevealed;//toggle

            BtnRevealMap.Content = isMapRevealed ? "Hide Map" : "Reveal Map";

            UpdateMinimap();
        }

        private async void BtnMonteCarlo_Click(object sender, RoutedEventArgs e)
        {
            BtnGenerate.IsEnabled = false;//freeze UI
            ModeComboBox.IsEnabled = false;
            SizeComboBox.IsEnabled = false;
            SeedTextBox.IsEnabled = false;

            BtnMonteCarlo.IsHitTestVisible = false;
            BtnMonteCarlo.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Goldenrod);
            BtnMonteCarlo.Content = "SIMULATING 10,000 SEEDS...";

            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            BtnCancel.Content = "CANCEL";

            TxtMonteCarloProgress.Visibility = Visibility.Visible;
            TxtMonteCarloProgress.Text = "Checked: 0 / 10000";
            TxtGenerateStatus.Text = "";

            int modeIndex = ModeComboBox.SelectedIndex;
            string modeName = ((ComboBoxItem)ModeComboBox.SelectedItem).Content.ToString();

            int gridSize = activeGridSize;
            if (gridSize < 7) gridSize = 7;

            monteCarloCts = new System.Threading.CancellationTokenSource();

            var results = await Task.Run(() => RunMonteCarloSimulation(modeIndex, gridSize, monteCarloCts.Token));//background

            if (results == null || monteCarloCts.IsCancellationRequested)
            {
                TxtGenerateStatus.Text = "Simulation Aborted.";
            }
            else
            {
                int passed = results.Count(r => r.passed);
                int failed = results.Count(r => !r.passed);
                double winRate = Math.Round(((double)passed / 10000) * 100, 2);

                TxtMonteCarloSettings.Text = $"Mode: {modeName} | Grid: {gridSize}x{gridSize}";
                TxtMonteCarloPass.Text = $"Passed: {passed}";
                TxtMonteCarloFail.Text = $"Failed: {failed}";
                TxtMonteCarloRate.Text = $"Win Rate: {winRate}%";

                ListMonteCarloProof.Items.Clear();
                foreach (var result in results)
                {
                    string status = result.passed ? "PASS" : "X X X X FAIL";
                    ListMonteCarloProof.Items.Add($"Seed: {result.seed.ToString("D8")}  -  {status}");
                }

                MonteCarloPanel.Visibility = Visibility.Visible;
            }

            BtnGenerate.IsEnabled = true;
            ModeComboBox.IsEnabled = true;
            SizeComboBox.IsEnabled = true;
            SeedTextBox.IsEnabled = true;

            BtnMonteCarlo.IsHitTestVisible = true;
            BtnMonteCarlo.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#673AB7"));
            BtnMonteCarlo.Content = "RUN MONTE CARLO (10k SEEDS)";

            BtnCancel.Visibility = Visibility.Collapsed;
            TxtMonteCarloProgress.Visibility = Visibility.Collapsed;

            if (monteCarloCts != null)
            {
                monteCarloCts.Dispose();
                monteCarloCts = null;
            }
        }

        private List<(int seed, bool passed)> RunMonteCarloSimulation(int modeIndex, int size, System.Threading.CancellationToken token)
        {
            List<(int seed, bool passed)> simResults = new List<(int, bool)>();
            Random rand = new Random();
            ValidationEngine validator = new ValidationEngine();

            for (int i = 1; i <= 10000; i++)//10000
            {
                if (token.IsCancellationRequested) return null;

                int currentSeed = rand.Next(1, 10000001);//10,000 seed of 1 million are checked
                MapGenerator simMap = new MapGenerator();

                if (modeIndex == 0)
                {
                    simMap.GenerateMode1_Zones(size, currentSeed);
                }
                else if (modeIndex == 1)
                {
                    simMap.GenerateMode2_ValidatedScramble(size, currentSeed, null, token);//cancel can be passed into this

                    if (token.IsCancellationRequested) return null;
                }
                else
                {
                    simMap.GenerateMode3_Chaos(size, currentSeed);
                }

                var report = validator.CheckIfBeatable(simMap, false);
                simResults.Add((currentSeed, report != null && report.IsBeatable));

                if (i % 50 == 0)//update ui every 50
                {
                    Dispatcher.Invoke(() => TxtMonteCarloProgress.Text = $"Checked: {i} / 10000");
                }
            }

            return simResults.OrderBy(r => r.seed).ToList();//order
        }

        private void BtnCloseMonteCarlo_Click(object sender, RoutedEventArgs e)
        {
            MonteCarloPanel.Visibility = Visibility.Collapsed;
            TxtGenerateStatus.Text = "";
        }

        private void BtnViewRoutingLog_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLogWindow != null && _currentLogWindow.IsLoaded)
            {
                _currentLogWindow.Activate();
                return;
            }

            _currentLogWindow = new SpoilerLogWindow(cachedRoute100, cachedRouteSteps, cachedRouteLocks, cachedRouteExplorable);
            _currentLogWindow.Owner = this;
            _currentLogWindow.Show(); 
        }

        private async void BtnGenerateBasicAnalytics_Click(object sender, RoutedEventArgs e)//can take awhile on large maps, proabably the can every room be visited but trying to keep it all in one spot
        {
            MapGenerator newMap = new MapGenerator();//map cloning again, (make a method would save lines)

            if (ModeComboBox.SelectedIndex == 1)
            {
                newMap.GenerateMode2_RandTilBeat(activeGridSize, activeSubSeed);
            }
            else if (ModeComboBox.SelectedIndex == 0)
            {
                newMap.GenerateMode1_Zones(activeGridSize, activeSeed);
            }
            else
            {
                newMap.GenerateMode3_Chaos(activeGridSize, activeSeed);
            }

            if (newMap == null) return;

            BtnGenerateBasicAnalytics.IsEnabled = false;
            BtnGenerate100Percent.IsEnabled = false;
            BtnMainMenu.IsEnabled = false;
            BtnGenerateExplorable.IsEnabled = false;
            BtnCancelAnalytics.Visibility = Visibility.Visible;


            _analyticsCancelToken = new CancellationTokenSource();

            try
            {
                AnalyticsEngine engine = new AnalyticsEngine();
                var report = await Task.Run(() => engine.RunBasicAnalytics(newMap, _analyticsCancelToken.Token));

                // If report is null, the user clicked Cancel.
                if (report == null)
                {

                }
                else
                {
                    cachedRouteSteps = report.RouteSteps;
                    cachedRouteLocks = report.RouteLocks;
                    BtnViewRoutingLog.IsEnabled = true;
                }
            }
            finally
            {
                BtnGenerateBasicAnalytics.IsEnabled = true;
                BtnGenerate100Percent.IsEnabled = true;
                BtnMainMenu.IsEnabled = true;
                BtnGenerateExplorable.IsEnabled = true;
                BtnCancelAnalytics.Visibility = Visibility.Collapsed;
            }
        }
        private async void BtnGenerate100Percent_Click(object sender, RoutedEventArgs e)
        {
            MapGenerator newMap = new MapGenerator();//map cloning

            if (ModeComboBox.SelectedIndex == 1)
            {
                newMap.GenerateMode2_RandTilBeat(activeGridSize, activeSubSeed);
            }
            else if (ModeComboBox.SelectedIndex == 0)
            {
                newMap.GenerateMode1_Zones(activeGridSize, activeSeed);
            }
            else
            {
                newMap.GenerateMode3_Chaos(activeGridSize, activeSeed);
            }

            if (newMap == null) return;

            BtnGenerateBasicAnalytics.IsEnabled = false;
            BtnGenerate100Percent.IsEnabled = false;
            BtnMainMenu.IsEnabled = false;
            BtnGenerateExplorable.IsEnabled = false;
            BtnGenerate100Percent.Content = "100% Completion: ...";
            BtnCancelAnalytics.Visibility = Visibility.Visible;

            TxtCompletion.Text = "100% Completion: ...";
            TxtCompletion.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

            _analyticsCancelToken = new CancellationTokenSource();

            try
            {
                AnalyticsEngine engine = new AnalyticsEngine();
                cachedRoute100 = await Task.Run(() => engine.Run100PercentCompletion(newMap, _analyticsCancelToken.Token));

                if (cachedRoute100 == "Cancelled.")
                {
                    TxtCompletion.Text = "100% Completion: CANCELLED";
                    TxtCompletion.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                }
                else if (cachedRoute100.Contains("Cannot reach"))
                {
                    TxtCompletion.Text = "100% Completion: NO";
                    TxtCompletion.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                else
                {
                    TxtCompletion.Text = "100% Completion: YES";
                    TxtCompletion.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                }
                BtnViewRoutingLog.IsEnabled = true;
            }
            finally
            {
                BtnGenerateBasicAnalytics.IsEnabled = true;
                BtnGenerate100Percent.IsEnabled = true;
                BtnMainMenu.IsEnabled = true;
                BtnGenerateExplorable.IsEnabled = true;
                BtnGenerate100Percent.Content = "GEN 100% COMPLETION";
                BtnCancelAnalytics.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancelAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (_analyticsCancelToken != null)
            {
                _analyticsCancelToken.Cancel();
                TxtGenerateStatus.Text = "Cancelled Analytics.";
            }
        }
        private void ResetAnalyticsUI()//reset
        {
            cachedRoute100 = "";
            cachedRouteSteps = "";
            cachedRouteLocks = "";
            cachedRouteExplorable = "";

            TxtExplorable.Text = "100% Explorable: ?";
            TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);

            BtnViewRoutingLog.IsEnabled = false;
            BtnViewRoutingLog.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#607D8B"));
        }

        private async void BtnGenerateExplorable_Click(object sender, RoutedEventArgs e)
        {
            MapGenerator newMap = new MapGenerator();//map cloning

            if (ModeComboBox.SelectedIndex == 1)
            {
                newMap.GenerateMode2_RandTilBeat(activeGridSize, activeSubSeed);
            }
            else if (ModeComboBox.SelectedIndex == 0)
            {
                newMap.GenerateMode1_Zones(activeGridSize, activeSeed);
            }
            else
            {
                newMap.GenerateMode3_Chaos(activeGridSize, activeSeed);
            }

            if (newMap == null) return;

            BtnGenerateExplorable.IsEnabled = false;
            BtnGenerateBasicAnalytics.IsEnabled = false;
            BtnGenerate100Percent.IsEnabled = false;
            BtnMainMenu.IsEnabled = false;
            BtnCancelAnalytics.Visibility = Visibility.Visible;

            TxtExplorable.Text = "100% Explorable: ...";
            TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

            _analyticsCancelToken = new CancellationTokenSource();

            try
            {
                AnalyticsEngine engine = new AnalyticsEngine();
                cachedRouteExplorable = await Task.Run(() => engine.RunExplorableAnalytics(newMap, _analyticsCancelToken.Token));

                if (cachedRouteExplorable == "Cancelled.")
                {
                    TxtExplorable.Text = "Explorable: CANCELLED";
                    TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                }
                else if (cachedRouteExplorable.Contains("Cannot reach"))
                {
                    TxtExplorable.Text = "100% Explorable: NO";
                    TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                else
                {
                    TxtExplorable.Text = "100% Explorable: YES";
                    TxtExplorable.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                }

                BtnViewRoutingLog.IsEnabled = true;
            }
            finally
            {
                BtnGenerateExplorable.IsEnabled = true;
                BtnGenerateBasicAnalytics.IsEnabled = true;
                BtnGenerate100Percent.IsEnabled = true;
                BtnMainMenu.IsEnabled = true;
                BtnCancelAnalytics.Visibility = Visibility.Collapsed;
            }
        }
    }
}