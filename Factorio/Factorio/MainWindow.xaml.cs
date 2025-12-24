using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Factorio
{
    public partial class MainWindow : Window
    {
        private string[] texturePaths = new string[]
        {
            @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\earth2.jpg",
            @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\earth3.jpg",
            @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\earth4.jpg"
        };

        private const int GridSize = 30;
        private int mapWidth = 6;
        private int mapHeight = 6;
        private Player player;
        private DispatcherTimer gameLoopTimer;
        private List<Resource> resources = new List<Resource>();
        private List<Smelter> smelters = new List<Smelter>();
        private List<Miner> miners = new List<Miner>();
        private List<Conveyor> conveyors = new List<Conveyor>();
        private List<ArmsFactory> armsFactories = new List<ArmsFactory>();
        private Random random = new Random();

        private bool isUpPressed = false;
        private bool isDownPressed = false;
        private bool isLeftPressed = false;
        private bool isRightPressed = false;
        private bool isMiningPressed = false;
        private bool isDeletingMode = false;
        private bool isBuildingMode = false;
        private string buildingToPlace = "";
        private Image buildingPreview;
        private bool isGridVisible = true;
        private bool isBuildingLine = false;
        private Point lineStartPoint;
        private bool isLineFirstClick = true;
        private List<Conveyor> linePreviewConveyors = new List<Conveyor>();
        private bool isSelectingConveyorForBuilding = false;
        private object selectedBuildingForConnection = null;
        private bool isConnectingInput = true;
        private List<Insect> insects = new List<Insect>();
        private List<Cannon> cannons = new List<Cannon>();

        //Инициализация
        public MainWindow()
        {
            InitializeComponent();
            GameCanvas.MouseDown += GameCanvas_MouseDown;
            GameCanvas.MouseMove += GameCanvas_MouseMove;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;
            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;

            CreateTileMapToFillWindow();
            InitializePlayer();
            InitializeGameLoop();
            SpawnInitialResources();

            player.SetInventoryPanel(InventoryPanel);
            player.SetSmelters(smelters);
            this.Focus();

            ShowGrid();
        }

        private Point SnapToGrid(Point point)
        {
            double snappedX = Math.Floor(point.X / GridSize) * GridSize;
            double snappedY = Math.Floor(point.Y / GridSize) * GridSize;
            return new Point(snappedX, snappedY);
        }

        private Point GetBuildingCenterOffset(string buildingType)
        {
            if (buildingType == "conveyor")
                return new Point(0, 0);

            var size = GetBuildingSize(buildingType);
            return new Point(size.Width / 2, size.Height / 2);
        }

        private Size GetBuildingSize(string buildingType)
        {
            return buildingType switch
            {
                "smelter" => new Size(180, 150),
                "miner" => new Size(90, 90),
                "conveyor" => new Size(30, 30),
                "arms_factory" => new Size(90, 120),
                "cannon" => new Size(60, 60),
                _ => new Size(0, 0)
            };
        }

        private void InitializeGameLoop()
        {
            gameLoopTimer = new DispatcherTimer();
            gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameLoopTimer.Tick += GameLoop_Tick;
            gameLoopTimer.Start();
        }

        // В методе GameLoop_Tick добавляем проверку коллизий жуков со зданиями
        private void GameLoop_Tick(object sender, EventArgs e)
        {
            UpdatePlayerMovement();
            player.UpdateAnimation();
            player.UpdateMining(resources, isMiningPressed);

            foreach (var miner in miners)
                if (miner.IsBuilt) miner.CheckPlacementOnResource(resources);

            UpdateInsects();
            UpdatePlayerHealth();

            // Добавляем эту часть: проверка коллизий жуков со зданиями
            foreach (var insect in insects)
            {
                if (!insect.IsDead)
                {
                    insect.CheckBuildingCollisions(miners, smelters, armsFactories, conveyors, cannons);
                }
            }

            // Добавляем удаление разрушенных зданий
            RemoveDestroyedBuildings();
        }

        // Добавляем метод для удаления разрушенных зданий (добавить в конец класса)
        private void RemoveDestroyedBuildings()
        {
            // Удаляем разрушенных добытчиков
            for (int i = miners.Count - 1; i >= 0; i--)
            {
                if (miners[i].IsDestroyed)
                {
                    miners[i].RemoveFromCanvas(GameCanvas);
                    miners.RemoveAt(i);
                }
            }

            // Удаляем разрушенные плавильни
            for (int i = smelters.Count - 1; i >= 0; i--)
            {
                if (smelters[i].IsDestroyed)
                {
                    smelters[i].RemoveFromCanvas(GameCanvas);
                    smelters.RemoveAt(i);
                }
            }

            // Удаляем разрушенные оружейные заводы
            for (int i = armsFactories.Count - 1; i >= 0; i--)
            {
                if (armsFactories[i].IsDestroyed)
                {
                    armsFactories[i].RemoveFromCanvas(GameCanvas);
                    armsFactories.RemoveAt(i);
                }
            }

            // Удаляем разрушенные конвейеры
            for (int i = conveyors.Count - 1; i >= 0; i--)
            {
                if (conveyors[i].IsDestroyed)
                {
                    conveyors[i].RemoveFromCanvas(GameCanvas);
                    conveyors.RemoveAt(i);
                }
            }

            // Удаляем разрушенные пушки
            for (int i = cannons.Count - 1; i >= 0; i--)
            {
                if (cannons[i].IsDestroyed)
                {
                    cannons[i].RemoveFromCanvas(GameCanvas);
                    cannons.RemoveAt(i);
                }
            }
        }

        private void UpdateInsects()
        {
            for (int i = insects.Count - 1; i >= 0; i--)
            {
                if (insects[i].IsDead)
                {
                    insects[i].RemoveFromCanvas(GameCanvas);
                    insects.RemoveAt(i);
                }
            }
        }

        //Ввод
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (isDeletingMode)
                        CancelDeletingMode();
                    else if (isBuildingMode)
                        CancelBuildingMode();
                    else if (isSelectingConveyorForBuilding)
                        CancelSelectConveyorForBuilding();
                    else
                        this.Close();
                    break;
                case Key.W: case Key.Up: isUpPressed = true; break;
                case Key.S: case Key.Down: isDownPressed = true; break;
                case Key.A: case Key.Left: isLeftPressed = true; break;
                case Key.D: case Key.Right: isRightPressed = true; break;
                case Key.Space: isMiningPressed = true; break;
                case Key.R: SpawnRandomResource(); break;
                case Key.I: ShowInventoryInfo(); break;

                case Key.Tab:
                    if (!isBuildingMode && !isBuildingLine && !isSelectingConveyorForBuilding)
                        OpenBuildMenu();
                    else
                    {
                        CancelBuildingMode();
                        CancelLineMode();
                        CancelSelectConveyorForBuilding();
                    }
                    e.Handled = true;
                    break;

                case Key.C:
                    if (isBuildingMode) CancelBuildingMode();
                    else if (isSelectingConveyorForBuilding) CancelSelectConveyorForBuilding();
                    else ShowMessage("Используйте ПРАВЫЙ клик по зданию для соединения с конвейером");
                    break;

                case Key.T: CreateTestSetup(); break;
                case Key.G: ToggleGrid(); break;

                case Key.M:
                    if (!isBuildingMode && !isBuildingLine && !isSelectingConveyorForBuilding)
                        SpawnInsects();
                    else
                        ShowInventoryInfo();
                    break;

                case Key.E:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        EmergencyCleanup();
                    break;
                case Key.Delete:
                    if (!isDeletingMode)
                        StartDeletingMode();
                    else
                        CancelDeletingMode();
                    break;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W: case Key.Up: isUpPressed = false; break;
                case Key.S: case Key.Down: isDownPressed = false; break;
                case Key.A: case Key.Left: isLeftPressed = false; break;
                case Key.D: case Key.Right: isRightPressed = false; break;
                case Key.Space: isMiningPressed = false; break;
            }
        }

        private void GameCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(GameCanvas);
            var snappedPosition = SnapToGrid(position);
            Point clickPoint = new Point(snappedPosition.X, snappedPosition.Y);

            if (isDeletingMode && e.LeftButton == MouseButtonState.Pressed)
            {
                DeleteBuildingAtPoint(clickPoint);
                return;
            }

            if (isDeletingMode && e.RightButton == MouseButtonState.Pressed)
            {
                CancelDeletingMode();
                return;
            }

            if (isSelectingConveyorForBuilding && e.LeftButton == MouseButtonState.Pressed)
            {
                var conveyor = FindConveyorAtPoint(clickPoint);
                if (conveyor != null)
                    CompleteConveyorConnection(conveyor);
                else
                    ShowMessage("Кликните на конвейер!");
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                if (isSelectingConveyorForBuilding)
                {
                    CancelSelectConveyorForBuilding();
                    return;
                }

                var building = FindBuildingAtPoint(clickPoint);
                if (building != null)
                {
                    ShowConnectionContextMenu(building, e.GetPosition(this));
                    return;
                }

                foreach (var smelter in smelters)
                    if (smelter.IsBuilt && smelter.IsPointInside(clickPoint))
                    { smelter.OpenInterface(); return; }

                foreach (var miner in miners)
                    if (miner.IsBuilt && miner.IsPointInside(clickPoint))
                    { miner.OpenInterface(); return; }

                foreach (var armsFactory in armsFactories)
                    if (armsFactory.IsBuilt && armsFactory.IsPointInside(clickPoint))
                    { armsFactory.OpenInterface(); return; }
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (isBuildingLine)
                {
                    HandleLineModeClick(clickPoint);
                    return;
                }

                if (isBuildingMode)
                {
                    HandleBuildingModeClick(clickPoint);
                    return;
                }
            }
        }

        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isBuildingMode && buildingPreview != null)
            {
                var position = e.GetPosition(GameCanvas);
                var snappedPosition = SnapToGrid(position);
                var offset = GetBuildingCenterOffset(buildingToPlace);
                var size = GetBuildingSize(buildingToPlace);

                double buildingX = snappedPosition.X - offset.X;
                double buildingY = snappedPosition.Y - offset.Y;
                buildingX = Math.Floor(buildingX / GridSize) * GridSize;
                buildingY = Math.Floor(buildingY / GridSize) * GridSize;

                Canvas.SetLeft(buildingPreview, buildingX);
                Canvas.SetTop(buildingPreview, buildingY);

                bool isValidPosition = IsBuildingPlacementValid(buildingX, buildingY, buildingToPlace);

                if (buildingToPlace == "miner" && isValidPosition)
                {
                    bool isOnResource = false;
                    Rect minerRect = new Rect(buildingX, buildingY, size.Width, size.Height);
                    foreach (var resource in resources)
                    {
                        Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                        if (minerRect.IntersectsWith(resourceRect))
                        {
                            isOnResource = true;
                            break;
                        }
                    }

                    isValidPosition = isValidPosition && isOnResource;
                    buildingPreview.Opacity = isOnResource ? 0.7 : 0.3;
                    BuildHintText.Text = isOnResource ? "Кликните для постройки на этом ресурсе" : "Добытчик должен быть построен НА РЕСУРСЕ!";
                }
                else
                {
                    buildingPreview.Opacity = isValidPosition ? 0.7 : 0.3;
                    BuildHintText.Text = isValidPosition ? buildingToPlace switch
                    {
                        "smelter" => "Кликните для постройки плавильни (10 камня + 5 угля)",
                        "conveyor" => "Кликните для постройки конвейера (2 железных слитка)",
                        "arms_factory" => "Кликните для постройки оружейного завода (15 камня + 10 жел.слитков + 10 мед.слитков)",
                        "cannon" => "Кликните для постройки пушки (5 жел.слитков + 6 деталей + 3 патрона)",
                        _ => "Кликните для постройки"
                    } : "Нельзя построить здесь (занято, слишком далеко или вне границ)";
                }
            }
            else if (isBuildingLine)
            {
                var position = e.GetPosition(GameCanvas);
                var snappedPosition = SnapToGrid(position);
                UpdateLinePreview(snappedPosition);
            }
            else if (isSelectingConveyorForBuilding)
            {
                var position = e.GetPosition(GameCanvas);
                var conveyorAtCursor = FindConveyorAtPoint(position);

                foreach (var conveyor in conveyors)
                    conveyor.Sprite.Opacity = 1.0;

                if (conveyorAtCursor != null)
                {
                    BuildHintText.Text = $"Конвейер выбран: ({conveyorAtCursor.X}, {conveyorAtCursor.Y})\n" +
                                       $"Направление: {conveyorAtCursor.Direction}\n" +
                                       "Кликните, чтобы соединить с зданием";
                    conveyorAtCursor.Sprite.Opacity = 0.9;
                }
                else
                {
                    BuildHintText.Text = "Выберите конвейер для соединения с зданием";
                }
            }
        }

        private void HandleBuildingModeClick(Point clickPoint)
        {
            var offset = GetBuildingCenterOffset(buildingToPlace);
            double buildingX = clickPoint.X - offset.X;
            double buildingY = clickPoint.Y - offset.Y;
            buildingX = Math.Floor(buildingX / GridSize) * GridSize;
            buildingY = Math.Floor(buildingY / GridSize) * GridSize;

            if (IsBuildingPlacementValid(buildingX, buildingY, buildingToPlace))
            {
                switch (buildingToPlace)
                {
                    case "smelter": BuildSmelter(buildingX, buildingY); break;
                    case "miner": BuildMiner(buildingX, buildingY); break;
                    case "conveyor": ShowDirectionSelectionMenu(buildingX, buildingY); break;
                    case "arms_factory": BuildArmsFactory(buildingX, buildingY); break;
                    case "cannon": BuildCannon(buildingX, buildingY); break;
                }
            }
            else
            {
                ShowMessage("Нельзя построить здесь!");
            }
        }


        private void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Tuple<object, bool> connectionInfo)
            {
                var building = connectionInfo.Item1;
                var isInput = connectionInfo.Item2;
                StartSelectConveyorForBuilding(building, isInput);
            }
        }

        //Постройка
        private bool IsBuildingPlacementValid(double x, double y, string buildingType, bool checkDistance = true)
        {
            var size = GetBuildingSize(buildingType);

            Rect playerRect = new Rect(player.X, player.Y, player.Width, player.Height);
            Rect buildingRect = new Rect(x, y, size.Width, size.Height);

            if (playerRect.IntersectsWith(buildingRect))
            {
                return false;
            }

            if (checkDistance && buildingType != "conveyor")
            {
                double playerCenterX = player.X + player.Width / 2;
                double playerCenterY = player.Y + player.Height / 2;
                double buildingCenterX = x + size.Width / 2;
                double buildingCenterY = y + size.Height / 2;

                double distance = Math.Sqrt(
                    Math.Pow(playerCenterX - buildingCenterX, 2) +
                    Math.Pow(playerCenterY - buildingCenterY, 2));

                if (distance > 200)
                    return false;
            }

            if (x < 0 || y < 0 || x + size.Width > GameCanvas.ActualWidth || y + size.Height > GameCanvas.ActualHeight)
                return false;

            Rect newBuildingRect = new Rect(x, y, size.Width, size.Height);

            if (buildingType == "conveyor")
                return IsConveyorPlacementValid(x, y);
            else
                return IsRegularBuildingPlacementValid(newBuildingRect, buildingType);
        }

        private bool IsConveyorPlacementValid(double x, double y)
        {
            Point conveyorCenter = new Point(x + 15, y + 15);

            Rect playerRect = new Rect(player.X, player.Y, player.Width, player.Height);
            Rect conveyorRect = new Rect(x, y, 30, 30);

            if (playerRect.IntersectsWith(conveyorRect))
            {
                return false; 
            }

            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt && PointsAreEqual(conveyorCenter, new Point(conveyor.X + 15, conveyor.Y + 15)))
                    return false;
            }

            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt && IsPointTooCloseToRect(conveyorCenter, smelter.X, smelter.Y, smelter.Width, smelter.Height, 5))
                    return false;
            }

            foreach (var miner in miners)
            {
                if (miner.IsBuilt && IsPointTooCloseToRect(conveyorCenter, miner.X, miner.Y, miner.Width, miner.Height, 5))
                    return false;
            }

            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt && IsPointTooCloseToRect(conveyorCenter, armsFactory.X, armsFactory.Y, armsFactory.Width, armsFactory.Height, 5))
                    return false;
            }

            return true;
        }

        private bool IsRegularBuildingPlacementValid(Rect newBuildingRect, string buildingType)
        {
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt && RectanglesIntersectWithMargin(newBuildingRect, new Rect(smelter.X, smelter.Y, smelter.Width, smelter.Height), 5))
                    return false;
            }

            foreach (var miner in miners)
            {
                if (miner.IsBuilt && RectanglesIntersectWithMargin(newBuildingRect, new Rect(miner.X, miner.Y, miner.Width, miner.Height), 5))
                    return false;
            }

            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt && buildingType != "conveyor" && RectanglesIntersectWithMargin(newBuildingRect, new Rect(conveyor.X, conveyor.Y, conveyor.Width, conveyor.Height), 5))
                    return false;
            }

            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt && RectanglesIntersectWithMargin(newBuildingRect, new Rect(armsFactory.X, armsFactory.Y, armsFactory.Width, armsFactory.Height), 5))
                    return false;
            }

            if (buildingType != "miner")
            {
                foreach (var resource in resources)
                {
                    if (RectanglesIntersectWithMargin(newBuildingRect, new Rect(resource.X, resource.Y, resource.Width, resource.Height), 5))
                        return false;
                }
            }

            return true;
        }

        private bool PointsAreEqual(Point p1, Point p2, double tolerance = 0.1)
        {
            return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
        }

        private bool IsPointTooCloseToRect(Point point, double rectX, double rectY, double rectWidth, double rectHeight, double margin = 0)
        {
            return point.X >= rectX - margin && point.X <= rectX + rectWidth + margin &&
                   point.Y >= rectY - margin && point.Y <= rectY + rectHeight + margin;
        }

        private bool RectanglesIntersectWithMargin(Rect rect1, Rect rect2, double margin = 0)
        {
            Rect expandedRect1 = new Rect(rect1.X - margin, rect1.Y - margin, rect1.Width + 2 * margin, rect1.Height + 2 * margin);
            Rect expandedRect2 = new Rect(rect2.X - margin, rect2.Y - margin, rect2.Width + 2 * margin, rect2.Height + 2 * margin);
            return expandedRect1.IntersectsWith(expandedRect2);
        }

        private bool RectanglesOverlapWithMargin(Rect rect1, Rect rect2)
        {
            if (rect1.Width == 30 && rect1.Height == 30 && rect2.Width == 30 && rect2.Height == 30)
            {
                return rect1.IntersectsWith(rect2) &&
                       !(rect1.X + rect1.Width <= rect2.X ||
                         rect1.X >= rect2.X + rect2.Width ||
                         rect1.Y + rect1.Height <= rect2.Y ||
                         rect1.Y >= rect2.Y + rect2.Height);
            }

            Rect expandedRect1 = new Rect(rect1.X - 5, rect1.Y - 5, rect1.Width + 10, rect1.Height + 10);
            Rect expandedRect2 = new Rect(rect2.X - 5, rect2.Y - 5, rect2.Width + 10, rect2.Height + 10);
            return expandedRect1.IntersectsWith(expandedRect2);
        }



        //Игрок
        private void InitializePlayer()
        {
            double startX = this.ActualWidth / 2 - 25;
            double startY = this.ActualHeight / 2 - 25;
            player = new Player(startX, startY, 50, 50);
            player.AddToCanvas(GameCanvas);
        }

        private void UpdatePlayerMovement()
        {
            double deltaX = 0;
            double deltaY = 0;
            Direction direction = Direction.Down;

            if (isUpPressed && !isDownPressed) { deltaY = -1; direction = Direction.Up; }
            else if (isDownPressed && !isUpPressed) { deltaY = 1; direction = Direction.Down; }

            if (isLeftPressed && !isRightPressed) { deltaX = -1; direction = Direction.Left; }
            else if (isRightPressed && !isLeftPressed) { deltaX = 1; direction = Direction.Right; }

            if (Math.Abs(deltaX) > 0 && Math.Abs(deltaY) > 0)
            {
                double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                deltaX /= length; deltaY /= length;
                if (deltaX < 0) direction = Direction.Left;
                else if (deltaX > 0) direction = Direction.Right;
            }

            if (deltaX != 0 || deltaY != 0)
                player.Move(deltaX, deltaY, direction, miners, smelters, conveyors, armsFactories, cannons);
            else
                player.Stop();
        }

        private void UpdatePlayerHealth()
        {
            if (player == null) return;

            HealthBar.Value = player.Health;
            HealthBar.Maximum = player.MaxHealth;
            HealthText.Text = $"{player.Health}/{player.MaxHealth}";

            if (player.Health <= 2)
                HealthBar.Foreground = Brushes.Red;
            else if (player.Health <= 4)
                HealthBar.Foreground = Brushes.Yellow;
            else
                HealthBar.Foreground = Brushes.LimeGreen;

            if (player.IsDead)
            {
                gameLoopTimer.Stop();
                MessageBox.Show("Игрок умер! Игра окончена.", "Game Over", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }

        //Здания
        private bool HasBuildingResources(string buildingType)
        {
            return buildingType switch
            {
                "smelter" => player.CanBuildSmelter(),
                "miner" => player.CanBuildMiner(),
                "conveyor" => player.HasResources(ResourceType.IronIngot, 2),
                "arms_factory" => player.CanBuildArmsFactory(),
                "cannon" => player.HasResources(ResourceType.IronIngot, 5) &&
                           player.HasResources(ResourceType.Gears, 6) &&
                           player.HasResources(ResourceType.Ammo, 3),
                _ => false
            };
        }

        private void BuildSmelter(double x, double y)
        {
            if (!HasBuildingResources("smelter"))
            {
                ShowMessage("Недостаточно ресурсов для постройки плавильни!\nНужно: 10 камня + 5 угля");
                return;
            }

            if (!player.RemoveBuildingResources())
            {
                ShowMessage("Ошибка при удалении ресурсов!");
                return;
            }

            Smelter smelter = new Smelter(x, y, player);
            smelter.Build();
            smelter.AddToCanvas(GameCanvas);
            smelters.Add(smelter);
            player.SetSmelters(smelters);
            ShowMessage("Плавильня построена!");
            CancelBuildingMode();
        }

        private void BuildMiner(double x, double y)
        {
            Resource targetResource = null;
            var size = GetBuildingSize("miner");
            Rect minerRect = new Rect(x, y, size.Width, size.Height);

            foreach (var resource in resources)
            {
                Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                if (RectanglesOverlapWithMargin(minerRect, resourceRect))
                {
                    targetResource = resource;
                    break;
                }
            }

            if (targetResource == null)
            {
                ShowMessage("Добытчик должен быть построен НА РЕСУРСЕ!");
                return;
            }

            if (!HasBuildingResources("miner"))
            {
                ShowMessage("Недостаточно ресурсов для постройки добытчика!\nНужно: 5 железных слитков + 5 медных слитков");
                return;
            }

            if (!player.RemoveMinerResources())
            {
                ShowMessage("Ошибка при удалении ресурсов!");
                return;
            }

            Miner miner = new Miner(x, y, player);
            miner.SetTargetResource(targetResource);
            miner.Build();
            miner.AddToCanvas(GameCanvas);
            miners.Add(miner);
            ShowMessage($"Добытчик построен на {GetResourceName(targetResource.Type)}!");
            CancelBuildingMode();
        }

        private void BuildSingleConveyor(double x, double y, Direction direction)
        {
            if (!HasBuildingResources("conveyor"))
            {
                ShowMessage("Недостаточно ресурсов для постройки конвейера!\nНужно: 2 железных слитка");
                return;
            }

            if (!player.RemoveResources(ResourceType.IronIngot, 2))
            {
                ShowMessage("Ошибка при удалении ресурсов!");
                return;
            }

            Conveyor conveyor = new Conveyor(x, y, direction);

            if (IsBuildingPlacementValid(x, y, "conveyor", false))
            {
                conveyor.Build();
                conveyor.AddToCanvas(GameCanvas);
                conveyors.Add(conveyor);
                GameCanvasHelper.AllConveyors = conveyors;
                ShowMessage("Конвейер построен!");
                CancelBuildingMode();
            }
            else
            {
                ShowMessage("Нельзя построить конвейер здесь!");
                player.AddResource(ResourceType.IronIngot, 2);
            }
        }

        private void BuildArmsFactory(double x, double y)
        {
            if (!HasBuildingResources("arms_factory"))
            {
                ShowMessage("Недостаточно ресурсов для постройки оружейного завода!\nНужно: 15 камня + 10 жел.слитков + 10 мед.слитков");
                return;
            }

            if (!player.RemoveArmsFactoryResources())
            {
                ShowMessage("Ошибка при удалении ресурсов!");
                return;
            }

            ArmsFactory armsFactory = new ArmsFactory(x, y, player);
            armsFactory.Build();
            armsFactory.AddToCanvas(GameCanvas);
            armsFactories.Add(armsFactory);
            ShowMessage("Оружейный завод построен!");
            CancelBuildingMode();
        }

        private void BuildCannon(double x, double y)
        {
            if (!HasBuildingResources("cannon"))
            {
                ShowMessage("Недостаточно ресурсов для постройки пушки!\nНужно: 5 железных слитков + 6 деталей + 3 патрона");
                return;
            }

            if (!player.RemoveResources(ResourceType.IronIngot, 5) ||
                !player.RemoveResources(ResourceType.Gears, 6) ||
                !player.RemoveResources(ResourceType.Ammo, 3))
            {
                ShowMessage("Ошибка при удалении ресурсов!");
                return;
            }

            Cannon cannon = new Cannon(x, y);
            cannon.SetTargetInsects(insects);
            cannon.SetGameCanvas(GameCanvas);
            cannon.Build();
            cannon.AddToCanvas(GameCanvas);
            cannons.Add(cannon);
            ShowMessage("Пушка построена!");
            CancelBuildingMode();
        }

        //Удаление
        private void StartDeletingMode()
        {
            isDeletingMode = true;
            CancelBuildingMode();
            CancelLineMode();
            CancelSelectConveyorForBuilding();

            DeleteButton.Background = Brushes.Red;
            DeleteButton.Content = "Отмена";
            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "РЕЖИМ УДАЛЕНИЯ:\nКликните на постройку для удаления\n(Esc или правый клик для отмены)";
            HighlightAllBuildings(true);
        }

        private void CancelDeletingMode()
        {
            isDeletingMode = false;
            DeleteButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF555555"));
            DeleteButton.Content = "Удалить";
            BuildHint.Visibility = Visibility.Collapsed;
            HighlightAllBuildings(false);
        }

        private void HighlightAllBuildings(bool highlight)
        {
            foreach (var miner in miners)
                if (miner.IsBuilt) miner.Sprite.Opacity = highlight ? 0.5 : 1.0;

            foreach (var smelter in smelters)
                if (smelter.IsBuilt) smelter.Sprite.Opacity = highlight ? 0.5 : 1.0;

            foreach (var conveyor in conveyors)
                if (conveyor.IsBuilt) conveyor.Sprite.Opacity = highlight ? 0.5 : 1.0;

            foreach (var armsFactory in armsFactories)
                if (armsFactory.IsBuilt) armsFactory.Sprite.Opacity = highlight ? 0.5 : 1.0;

            foreach (var cannon in cannons)
                if (cannon.IsBuilt) cannon.Sprite.Opacity = highlight ? 0.5 : 1.0;
        }

        private void DeleteBuildingAtPoint(Point point)
        {
            object buildingToDelete = FindBuildingAtPoint(point);

            if (buildingToDelete != null)
            {
                string buildingType = GetBuildingName(buildingToDelete);
                MessageBoxResult result = MessageBox.Show(
                    $"Удалить {buildingType}?\nВозвращается 50% ресурсов.",
                    "Удаление постройки",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    DeleteBuildingAndReturnResources(buildingToDelete);
            }
            else
                ShowMessage("На этой клетке нет построек!");

            CancelDeletingMode();
        }

        private void DeleteBuildingAndReturnResources(object building)
        {
            if (building is Miner miner)
            {
                int ironToReturn = (int)Math.Ceiling(5 * 0.5);
                int copperToReturn = (int)Math.Ceiling(5 * 0.5);
                player.AddResource(ResourceType.IronIngot, ironToReturn);
                player.AddResource(ResourceType.CopperIngot, copperToReturn);
                miner.RemoveFromCanvas(GameCanvas);
                miners.Remove(miner);
                foreach (var conveyor in conveyors)
                {
                    if (conveyor.LinkedBuilding == miner)
                    {
                        conveyor.LinkedBuilding = null;
                        conveyor.IsInputConveyor = false;
                        conveyor.IsOutputConveyor = false;
                    }
                }
                ShowMessage($"Добытчик удален! Возвращено {ironToReturn} жел.слитков и {copperToReturn} мед.слитков");
            }
            else if (building is Smelter smelter)
            {
                int stoneToReturn = (int)Math.Ceiling(10 * 0.5);
                int coalToReturn = (int)Math.Ceiling(5 * 0.5);
                player.AddResource(ResourceType.Stone, stoneToReturn);
                player.AddResource(ResourceType.Coal, coalToReturn);
                smelter.RemoveFromCanvas(GameCanvas);
                smelters.Remove(smelter);
                foreach (var conveyor in conveyors)
                {
                    if (conveyor.LinkedBuilding == smelter)
                    {
                        conveyor.LinkedBuilding = null;
                        conveyor.IsInputConveyor = false;
                        conveyor.IsOutputConveyor = false;
                    }
                }
                ShowMessage($"Плавильня удалена! Возвращено {stoneToReturn} камня и {coalToReturn} угля");
            }
            else if (building is Conveyor conveyor)
            {
                int ironToReturn = (int)Math.Ceiling(2 * 0.5);
                player.AddResource(ResourceType.IronIngot, ironToReturn);
                conveyor.RemoveFromCanvas(GameCanvas);
                conveyors.Remove(conveyor);
                ShowMessage($"Конвейер удален! Возвращено {ironToReturn} жел.слиток");
            }
            else if (building is ArmsFactory armsFactory)
            {
                int stoneToReturn = (int)Math.Ceiling(15 * 0.5);
                int ironToReturn = (int)Math.Ceiling(10 * 0.5);
                int copperToReturn = (int)Math.Ceiling(10 * 0.5);
                player.AddResource(ResourceType.Stone, stoneToReturn);
                player.AddResource(ResourceType.IronIngot, ironToReturn);
                player.AddResource(ResourceType.CopperIngot, copperToReturn);
                armsFactory.RemoveFromCanvas(GameCanvas);
                armsFactories.Remove(armsFactory);
                foreach (var conv in conveyors)
                {
                    if (conv.LinkedBuilding == armsFactory)
                    {
                        conv.LinkedBuilding = null;
                        conv.IsInputConveyor = false;
                        conv.IsOutputConveyor = false;
                    }
                }
                ShowMessage($"Оружейный завод удален! Возвращено {stoneToReturn} камня, {ironToReturn} жел.слитков и {copperToReturn} мед.слитков");
            }
            else if (building is Cannon cannon)
            {
                int ironToReturn = (int)Math.Ceiling(5 * 0.5);
                int gearsToReturn = (int)Math.Ceiling(6 * 0.5);
                int ammoToReturn = (int)Math.Ceiling(3 * 0.5);
                player.AddResource(ResourceType.IronIngot, ironToReturn);
                player.AddResource(ResourceType.Gears, gearsToReturn);
                player.AddResource(ResourceType.Ammo, ammoToReturn);
                cannon.RemoveFromCanvas(GameCanvas);
                cannons.Remove(cannon);
                ShowMessage($"Пушка удалена! Возвращено {ironToReturn} жел.слитков, {gearsToReturn} деталей и {ammoToReturn} патронов");
            }
        }

        //Конвейеры
        private void ShowConnectionContextMenu(object building, Point position)
        {
            var contextMenu = new ContextMenu();
            var connectInputItem = new MenuItem
            {
                Header = "Соединить с ВХОДНЫМ конвейером",
                Tag = new Tuple<object, bool>(building, true)
            };
            connectInputItem.Click += ConnectMenuItem_Click;

            var connectOutputItem = new MenuItem
            {
                Header = "Соединить с ВЫХОДНЫМ конвейером",
                Tag = new Tuple<object, bool>(building, false)
            };
            connectOutputItem.Click += ConnectMenuItem_Click;

            contextMenu.Items.Add(connectInputItem);
            contextMenu.Items.Add(connectOutputItem);
            contextMenu.IsOpen = true;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            contextMenu.HorizontalOffset = position.X;
            contextMenu.VerticalOffset = position.Y;
        }


        private void StartSelectConveyorForBuilding(object building, bool isInput)
        {
            if (building == null)
            {
                ShowMessage("Ошибка: здание не выбрано!");
                return;
            }

            isSelectingConveyorForBuilding = true;
            selectedBuildingForConnection = building;
            isConnectingInput = isInput;
            CancelBuildingMode();
            CancelLineMode();
            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = isInput ?
                "Выберите ВХОДНОЙ конвейер (должен быть направлен К зданию)" :
                "Выберите ВЫХОДНОЙ конвейер (должен быть направлен ОТ здания)";
        }

        private void CompleteConveyorConnection(Conveyor conveyor)
        {
            if (selectedBuildingForConnection == null || conveyor == null)
            {
                ShowMessage("Ошибка: не выбрано здание или конвейер!");
                return;
            }

            if (!IsConveyorDirectionValid(conveyor, selectedBuildingForConnection, isConnectingInput))
            {
                string directionError = isConnectingInput ?
                    "Конвейер должен быть направлен К зданию!" :
                    "Конвейер должен быть направлен ОТ здания!";
                ShowMessage($"Неправильное направление! {directionError}");
                return;
            }

            conveyor.LinkedBuilding = selectedBuildingForConnection;
            conveyor.IsInputConveyor = isConnectingInput;
            conveyor.IsOutputConveyor = !isConnectingInput;
            ShowMessage($"{(isConnectingInput ? "Входной" : "Выходной")} конвейер соединён с {GetBuildingName(selectedBuildingForConnection)}");
            CancelSelectConveyorForBuilding();
        }

        private bool IsConveyorDirectionValid(Conveyor conveyor, object building, bool isInput)
        {
            Point buildingCenter = GetBuildingCenter(building);
            Point conveyorCenter = new Point(conveyor.X + conveyor.Width / 2, conveyor.Y + conveyor.Height / 2);

            if (isInput)
                return IsDirectionTowardBuilding(conveyor.Direction, conveyorCenter, buildingCenter);
            else
                return IsDirectionAwayFromBuilding(conveyor.Direction, conveyorCenter, buildingCenter);
        }

        private bool IsDirectionTowardBuilding(Direction direction, Point conveyorCenter, Point buildingCenter)
        {
            switch (direction)
            {
                case Direction.Right: return buildingCenter.X > conveyorCenter.X;
                case Direction.Left: return buildingCenter.X < conveyorCenter.X;
                case Direction.Down: return buildingCenter.Y > conveyorCenter.Y;
                case Direction.Up: return buildingCenter.Y < conveyorCenter.Y;
                default: return false;
            }
        }

        private bool IsDirectionAwayFromBuilding(Direction direction, Point conveyorCenter, Point buildingCenter)
        {
            switch (direction)
            {
                case Direction.Right: return buildingCenter.X < conveyorCenter.X;
                case Direction.Left: return buildingCenter.X > conveyorCenter.X;
                case Direction.Down: return buildingCenter.Y < conveyorCenter.Y;
                case Direction.Up: return buildingCenter.Y > conveyorCenter.Y;
                default: return false;
            }
        }

        private void CancelSelectConveyorForBuilding()
        {
            isSelectingConveyorForBuilding = false;
            selectedBuildingForConnection = null;
            foreach (var conveyor in conveyors)
                conveyor.Sprite.Opacity = 1.0;
            BuildHint.Visibility = Visibility.Collapsed;
        }

        //Режим постройки 
        private void StartBuildingMode(string buildingType)
        {
            isBuildingMode = true;
            buildingToPlace = buildingType;
            CloseBuildMenu();

            var size = GetBuildingSize(buildingType);
            buildingPreview = new Image
            {
                Width = size.Width,
                Height = size.Height,
                Opacity = 0.7,
                Source = LoadBuildingPreview(buildingType)
            };
            GameCanvas.Children.Add(buildingPreview);
            Canvas.SetZIndex(buildingPreview, 99);
            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = buildingType switch
            {
                "smelter" => "Кликните на место для постройки плавильни (10 камня + 5 угля)",
                "miner" => "Кликните НА РЕСУРС для постройки добытчика (5 жел.слитков + 5 мед.слитков)",
                "conveyor" => "Кликните для постройки конвейера (2 железных слитка)",
                "arms_factory" => "Кликните на место для постройки оружейного завода (15 камня + 10 жел.слитков + 10 мед.слитков)",
                "cannon" => "Кликните на место для постройки пушки (5 жел.слитков + 6 деталей + 3 патрона)",
                _ => "Кликните на место для постройки"
            };
        }

        private void CancelBuildingMode()
        {
            isBuildingMode = false;
            buildingToPlace = "";

            if (buildingPreview != null && GameCanvas.Children.Contains(buildingPreview))
            {
                GameCanvas.Children.Remove(buildingPreview);
                buildingPreview = null;
            }

            BuildHint.Visibility = Visibility.Collapsed;
        }

        private void ShowDirectionSelectionMenu(double x, double y)
        {
            var directionMenu = new Window
            {
                Title = "Выберите направление конвейера",
                Width = 300,
                Height = 200,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            string[] directionNames = { "Вверх", "Вниз", "Влево", "Вправо" };

            for (int i = 0; i < 4; i++)
            {
                var button = new Button
                {
                    Content = directionNames[i],
                    Margin = new Thickness(5),
                    Tag = directions[i]
                };

                button.Click += (s, args) =>
                {
                    Direction selectedDirection = (Direction)((Button)s).Tag;
                    BuildSingleConveyor(x, y, selectedDirection);
                    directionMenu.Close();
                };

                Grid.SetRow(button, i < 2 ? 0 : 1);
                Grid.SetColumn(button, i % 2);
                grid.Children.Add(button);
            }

            directionMenu.Content = grid;
            directionMenu.ShowDialog();
        }


        private void UpdateLinePreview(Point currentPoint)
        {
            if (!isBuildingLine || isLineFirstClick) return;

            foreach (var conveyor in linePreviewConveyors)
                conveyor.RemoveFromCanvas(GameCanvas);
            linePreviewConveyors.Clear();

            List<Point> pathPoints = CalculateLinePath(lineStartPoint, currentPoint);

            foreach (var point in pathPoints)
            {
                Direction direction = CalculateDirectionBetweenPoints(lineStartPoint, point);
                Conveyor previewConveyor = new Conveyor(point.X, point.Y, direction) { Sprite = { Opacity = 0.5 } };
                previewConveyor.AddToCanvas(GameCanvas);
                linePreviewConveyors.Add(previewConveyor);
            }
        }

        private List<Point> CalculateLinePath(Point start, Point end)
        {
            List<Point> path = new List<Point>();

            if (Math.Abs(start.X - end.X) < 5 && Math.Abs(start.Y - end.Y) < 5)
            {
                path.Add(start);
                return path;
            }

            if (Math.Abs(start.Y - end.Y) < 5)
            {
                int step = start.X < end.X ? GridSize : -GridSize;
                for (double x = start.X; Math.Abs(x - end.X) > GridSize / 2; x += step)
                    path.Add(new Point(x, start.Y));
                path.Add(end);
            }
            else if (Math.Abs(start.X - end.X) < 5)
            {
                int step = start.Y < end.Y ? GridSize : -GridSize;
                for (double y = start.Y; Math.Abs(y - end.Y) > GridSize / 2; y += step)
                    path.Add(new Point(start.X, y));
                path.Add(end);
            }
            else
            {
                int stepX = start.X < end.X ? GridSize : -GridSize;
                int stepY = start.Y < end.Y ? GridSize : -GridSize;

                for (double x = start.X; Math.Abs(x - end.X) > GridSize / 2; x += stepX)
                    path.Add(new Point(x, start.Y));

                Point lastPoint = path.Last();
                for (double y = lastPoint.Y + stepY; Math.Abs(y - end.Y) > GridSize / 2; y += stepY)
                    path.Add(new Point(end.X, y));
                path.Add(end);
            }

            return path.Distinct().ToList();
        }

        private Direction CalculateDirectionBetweenPoints(Point from, Point to)
        {
            if (Math.Abs(to.Y - from.Y) < GridSize)
                return to.X > from.X ? Direction.Right : Direction.Left;
            else
                return to.Y > from.Y ? Direction.Down : Direction.Up;
        }

        private void HandleLineModeClick(Point clickPoint)
        {
            if (isLineFirstClick)
            {
                lineStartPoint = clickPoint;
                isLineFirstClick = false;
                BuildHintText.Text = "Начальная точка выбрана. Кликните на конечную точку.";
            }
            else
            {
                CancelLineMode();
            }
        }

        private void CancelLineMode()
        {
            isBuildingLine = false;
            isLineFirstClick = true;
            foreach (var conveyor in linePreviewConveyors)
                conveyor.RemoveFromCanvas(GameCanvas);
            linePreviewConveyors.Clear();
            BuildHint.Visibility = Visibility.Collapsed;
        }

        private Conveyor FindConveyorAtPoint(Point point)
        {
            foreach (var conveyor in conveyors)
                if (conveyor.IsBuilt && conveyor.IsPointInside(point)) return conveyor;
            return null;
        }

        private object FindBuildingAtPoint(Point point)
        {
            foreach (var miner in miners)
                if (miner.IsBuilt && miner.IsPointInside(point)) return miner;
            foreach (var smelter in smelters)
                if (smelter.IsBuilt && smelter.IsPointInside(point)) return smelter;
            foreach (var conveyor in conveyors)
                if (conveyor.IsBuilt && conveyor.IsPointInside(point)) return conveyor;
            foreach (var armsFactory in armsFactories)
                if (armsFactory.IsBuilt && armsFactory.IsPointInside(point)) return armsFactory;
            foreach (var cannon in cannons)
                if (cannon.IsBuilt && cannon.IsPointInside(point)) return cannon;
            return null;
        }

        private string GetBuildingName(object building)
        {
            if (building is Miner) return "Добытчик";
            if (building is Smelter) return "Плавильня";
            if (building is ArmsFactory) return "Оружейный завод";
            return "Неизвестное здание";
        }

        private Point GetBuildingCenter(object building)
        {
            if (building is Miner miner)
                return new Point(miner.X + miner.Width / 2, miner.Y + miner.Height / 2);
            else if (building is Smelter smelter)
                return new Point(smelter.X + smelter.Width / 2, smelter.Y + smelter.Height / 2);
            else if (building is ArmsFactory armsFactory)
                return new Point(armsFactory.X + armsFactory.Width / 2, armsFactory.Y + armsFactory.Height / 2);
            return new Point(0, 0);
        }

        //Ресурсы
        private void SpawnInitialResources()
        {
            for (int i = 0; i < 25; i++)
                SpawnRandomResource();
        }

        private void SpawnRandomResource()
        {
            int attempts = 0;
            int gridX = 0, gridY = 0;
            int maxGridX = (int)(this.ActualWidth / GridSize) - 1;
            int maxGridY = (int)(this.ActualHeight / GridSize) - 1;
            int minBorder = 2;

            do
            {
                gridX = random.Next(minBorder, maxGridX - minBorder);
                gridY = random.Next(minBorder, maxGridY - minBorder);
                double x = gridX * GridSize;
                double y = gridY * GridSize;

                ResourceType type = (ResourceType)random.Next(4);
                double width = 30, height = 30;
                x += (GridSize - width) / 2;
                y += (GridSize - height) / 2;

                attempts++;
                if (attempts > 100) break;

                if (IsGridCellValid(gridX, gridY, 2))
                {
                    Resource resource = new Resource(x, y, type);
                    resource.AddToCanvas(GameCanvas);
                    resources.Add(resource);
                    resource.Tag = $"{gridX},{gridY}";
                    return;
                }

            } while (true);
        }

        private bool IsGridCellValid(int gridX, int gridY, int minDistanceInCells = 2)
        {
            foreach (var resource in resources)
            {
                if (resource.Tag is string tag && tag.Contains(","))
                {
                    var parts = tag.Split(',');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int existingGridX) &&
                        int.TryParse(parts[1], out int existingGridY))
                    {
                        int cellDistanceX = Math.Abs(gridX - existingGridX);
                        int cellDistanceY = Math.Abs(gridY - existingGridY);
                        if (cellDistanceX < minDistanceInCells && cellDistanceY < minDistanceInCells)
                            return false;
                    }
                }
                else
                {
                    int existingGridX = (int)(resource.X / GridSize);
                    int existingGridY = (int)(resource.Y / GridSize);
                    int cellDistanceX = Math.Abs(gridX - existingGridX);
                    int cellDistanceY = Math.Abs(gridY - existingGridY);
                    if (cellDistanceX < minDistanceInCells && cellDistanceY < minDistanceInCells)
                        return false;
                }
            }

            if (player != null)
            {
                int playerGridX = (int)(player.X / GridSize);
                int playerGridY = (int)(player.Y / GridSize);
                int distanceToPlayerX = Math.Abs(gridX - playerGridX);
                int distanceToPlayerY = Math.Abs(gridY - playerGridY);
                if (distanceToPlayerX < 4 && distanceToPlayerY < 4)
                    return false;
            }

            return true;
        }

        private string GetResourceName(ResourceType type)
        {
            return type switch
            {
                ResourceType.Iron => "железе",
                ResourceType.Copper => "меди",
                ResourceType.Coal => "угле",
                ResourceType.Stone => "камне",
                _ => "ресурсе"
            };
        }

        //Доп
        private void CreateTileMapToFillWindow()
        {
            GameCanvas.Children.Clear();
            double canvasWidth = this.ActualWidth;
            double canvasHeight = this.ActualHeight;
            double tileWidth = canvasWidth / mapWidth;
            double tileHeight = canvasHeight / mapHeight;

            for (int row = 0; row < mapHeight; row++)
            {
                for (int col = 0; col < mapWidth; col++)
                {
                    int textureIndex = random.Next(texturePaths.Length);
                    Image tile = new Image
                    {
                        Width = tileWidth,
                        Height = tileHeight,
                        Source = new BitmapImage(new Uri(texturePaths[textureIndex])),
                        Stretch = Stretch.UniformToFill
                    };
                    Canvas.SetLeft(tile, col * tileWidth);
                    Canvas.SetTop(tile, row * tileHeight);
                    GameCanvas.Children.Add(tile);
                }
            }
        }

        private void ShowGrid()
        {
            var gridElements = GameCanvas.Children.OfType<Rectangle>().Where(r => r.Name == "GridLine").ToList();
            foreach (var element in gridElements)
                GameCanvas.Children.Remove(element);

            if (!isGridVisible) return;

            for (int x = 0; x < this.ActualWidth; x += GridSize)
            {
                var verticalLine = new Rectangle
                {
                    Name = "GridLine",
                    Width = 1,
                    Height = this.ActualHeight,
                    Fill = Brushes.Red,
                    Opacity = 0.5
                };
                Canvas.SetLeft(verticalLine, x);
                Canvas.SetTop(verticalLine, 0);
                Canvas.SetZIndex(verticalLine, 5);
                GameCanvas.Children.Add(verticalLine);
            }

            for (int y = 0; y < this.ActualHeight; y += GridSize)
            {
                var horizontalLine = new Rectangle
                {
                    Name = "GridLine",
                    Width = this.ActualWidth,
                    Height = 1,
                    Fill = Brushes.Red,
                    Opacity = 0.5
                };
                Canvas.SetLeft(horizontalLine, 0);
                Canvas.SetTop(horizontalLine, y);
                Canvas.SetZIndex(horizontalLine, 5);
                GameCanvas.Children.Add(horizontalLine);
            }
        }

        private BitmapImage LoadBuildingPreview(string buildingType)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            if (buildingType == "cannon")
                basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\npc\";

            string fileName = buildingType switch
            {
                "smelter" => "Smelter.png",
                "miner" => "Mining.png",
                "conveyor" => "conveyor\\down_1.png",
                "arms_factory" => "arms_factory.png",
                "cannon" => "cannon.png",
                _ => "default.png"
            };

            string filePath = System.IO.Path.Combine(basePath, fileName);
            return File.Exists(filePath) ? new BitmapImage(new Uri(filePath)) : CreatePlaceholderBuildingPreview(buildingType);
        }

        private BitmapImage CreatePlaceholderBuildingPreview(string buildingType)
        {
            var size = GetBuildingSize(buildingType);
            int width = (int)size.Width, height = (int)size.Height;

            string text = buildingType switch
            {
                "smelter" => "SM",
                "miner" => "MI",
                "conveyor" => "CV",
                "arms_factory" => "AF",
                "cannon" => "CA",
                _ => "??"
            };

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                Brush color = buildingType switch
                {
                    "smelter" => Brushes.DarkGray,
                    "miner" => Brushes.DarkBlue,
                    "conveyor" => Brushes.DarkGreen,
                    "arms_factory" => Brushes.DarkBlue,
                    "cannon" => Brushes.DarkRed,
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, width, height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, width, height));

                var formattedText = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Arial"), width < 50 ? 14 : 20, Brushes.White, 1.0);
                drawingContext.DrawText(formattedText, new Point(width / 2 - 15, height / 2 - 10));
            }

            renderTarget.Render(drawingVisual);
            var bitmap = new BitmapImage();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }

            return bitmap;
        }

        private void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        private void ShowInventoryInfo()
        {
            string info = "Инвентарь: ";
            bool hasItems = false;

            for (int i = 0; i < player.Inventory.Length; i++)
            {
                var slot = player.Inventory[i];
                if (slot.Type != ResourceType.None)
                {
                    info += $"[{slot.Type}: {slot.Count}] ";
                    hasItems = true;
                }
            }

            if (!hasItems) info += "Пусто";
            ShowMessage(info);
        }

        private void SpawnInsects()
        {
            Random random = new Random();
            int insectCount = random.Next(2, 6);
            for (int i = 0; i < insectCount; i++)
                SpawnInsect();
            ShowMessage($"Появилось {insectCount} жуков!");
        }

        private void SpawnInsect()
        {
            Random random = new Random();
            int side = random.Next(4);
            double x, y;

            switch (side)
            {
                case 0: x = random.Next((int)this.ActualWidth); y = -50; break;
                case 1: x = random.Next((int)this.ActualWidth); y = this.ActualHeight + 50; break;
                case 2: x = -50; y = random.Next((int)this.ActualHeight); break;
                default: x = this.ActualWidth + 50; y = random.Next((int)this.ActualHeight); break;
            }

            Insect insect = new Insect(x, y, player);
            insect.AddToCanvas(GameCanvas);
            insects.Add(insect);
        }

        private void CreateTestSetup()
        {
            player.AddResource(ResourceType.CopperIngot, 10);
            player.AddResource(ResourceType.IronIngot, 10);
            player.AddResource(ResourceType.Coal, 5);
            player.AddResource(ResourceType.Stone, 10);

        }

        private void EmergencyCleanup()
        {
            foreach (var conveyor in conveyors.ToList())
                conveyor.RemoveFromCanvas(GameCanvas);
            conveyors.Clear();
            ShowMessage("Все конвейеры удалены!");
        }

        private void ToggleGrid()
        {
            isGridVisible = !isGridVisible;
            ShowGrid();
            ToggleGridButton.Content = $"Сетка: {(isGridVisible ? "Вкл" : "Выкл")}";
        }

        private void OpenBuildMenu()
        {
            BuildMenu.Visibility = Visibility.Visible;
            BuildHint.Visibility = Visibility.Collapsed;
        }

        private void CloseBuildMenu()
        {
            BuildMenu.Visibility = Visibility.Collapsed;
        }

        private void BuildCannonButton_Click(object sender, RoutedEventArgs e) => StartBuildingMode("cannon");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void BuildSmelterButton_Click(object sender, RoutedEventArgs e) => StartBuildingMode("smelter");
        private void BuildMinerButton_Click(object sender, RoutedEventArgs e) => StartBuildingMode("miner");
        private void BuildConveyorButton_Click(object sender, RoutedEventArgs e) => StartBuildingMode("conveyor");
        private void BuildArmsFactoryButton_Click(object sender, RoutedEventArgs e) => StartBuildingMode("arms_factory");
        private void CancelBuildButton_Click(object sender, RoutedEventArgs e) => CloseBuildMenu();
        private void ToggleGridButton_Click(object sender, RoutedEventArgs e) => ToggleGrid();
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDeletingMode)
                StartDeletingMode();
            else
                CancelDeletingMode();
        }
    }
}