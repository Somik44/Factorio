using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        private int mapWidth = 6;
        private int mapHeight = 6;
        private Player player;
        private DispatcherTimer gameLoopTimer;
        private List<Resource> resources = new List<Resource>();
        private List<Smelter> smelters = new List<Smelter>();
        private List<Miner> miners = new List<Miner>();
        private List<Conveyor> conveyors = new List<Conveyor>();
        private Random random = new Random();
        private bool isUpPressed = false;
        private bool isDownPressed = false;
        private bool isLeftPressed = false;
        private bool isRightPressed = false;
        private bool isMiningPressed = false;

        // Состояние постройки
        private bool isBuildingMode = false;
        private string buildingToPlace = "";
        private Image buildingPreview;

        // Состояние соединения конвейера
        private bool isConnectingConveyor = false;
        private Conveyor currentConveyor = null;
        private Miner conveyorSourceMiner = null;
        private Smelter conveyorTargetSmelter = null;

        // Состояние рисования линии конвейеров
        private bool isDrawingConveyorLine = false;
        private Point lineStartPoint;
        private Point lineEndPoint;
        private Direction? conveyorLineDirection = null; // Изменено на nullable
        private List<Conveyor> tempConveyors = new List<Conveyor>();

        private bool hasLineStart = false;

        public MainWindow()
        {
            InitializeComponent();

            // Добавляем обработчик клика по канвасу
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
            this.Focus();
        }

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

        private void InitializeGameLoop()
        {
            gameLoopTimer = new DispatcherTimer();
            gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameLoopTimer.Tick += GameLoop_Tick;
            gameLoopTimer.Start();
        }

        private void GameLoop_Tick(object sender, EventArgs e)
        {
            UpdatePlayerMovement();
            player.UpdateAnimation();
            player.UpdateMining(resources, isMiningPressed);

            // Обновляем все плавильни и добытчики
            foreach (var smelter in smelters)
            {
                // Прогресс переплавки обновляется через таймер в классе Smelter
            }

            // Проверяем состояние добытчиков
            foreach (var miner in miners)
            {
                if (miner.IsBuilt)
                {
                    miner.CheckPlacementOnResource(resources);
                }
            }

            // Отладочная информация о конвейерах
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsActive && conveyor.SourceMiner != null)
                {
                    Console.WriteLine($"Конвейер: Активен, источник={conveyor.SourceMiner.GetOutputType()}, ресурсов={conveyor.SourceMiner.GetOutputCount()}");
                }
            }
        }

        private void UpdatePlayerMovement()
        {
            double deltaX = 0;
            double deltaY = 0;
            Direction direction = Direction.Down;

            if (isUpPressed && !isDownPressed)
            {
                deltaY = -1;
                direction = Direction.Up;
            }
            else if (isDownPressed && !isUpPressed)
            {
                deltaY = 1;
                direction = Direction.Down;
            }

            if (isLeftPressed && !isRightPressed)
            {
                deltaX = -1;
                direction = Direction.Left;
            }
            else if (isRightPressed && !isLeftPressed)
            {
                deltaX = 1;
                direction = Direction.Right;
            }

            if (Math.Abs(deltaX) > 0 && Math.Abs(deltaY) > 0)
            {
                double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                deltaX /= length;
                deltaY /= length;
                if (deltaX < 0) direction = Direction.Left;
                else if (deltaX > 0) direction = Direction.Right;
            }

            if (deltaX != 0 || deltaY != 0)
            {
                player.Move(deltaX, deltaY, direction, miners);
            }
            else
            {
                player.Stop();
            }
        }

        private void SpawnInitialResources()
        {
            int resourceCount = 20;
            for (int i = 0; i < resourceCount; i++)
            {
                SpawnRandomResource();
            }
        }

        private void SpawnRandomResource()
        {
            int attempts = 0;
            double x = 0;
            double y = 0;

            // Пытаемся найти валидную позицию
            do
            {
                x = random.Next(50, (int)this.ActualWidth - 50);
                y = random.Next(50, (int)this.ActualHeight - 150);
                attempts++;

                if (attempts > 100)
                {
                    // Если не можем найти позицию, пробуем еще раз с меньшим расстоянием
                    break;
                }

            } while (!IsResourcePositionValid(x, y, 100));

            double distanceToPlayer = Math.Sqrt(Math.Pow(x - player.X, 2) + Math.Pow(y - player.Y, 2));
            if (distanceToPlayer < 100)
            {
                x = (x + 150) % (this.ActualWidth - 100);
                y = (y + 150) % (this.ActualHeight - 200);
            }

            ResourceType type = (ResourceType)random.Next(4);
            // Бесконечные ресурсы - не указываем количество
            Resource resource = new Resource(x, y, type);
            resource.AddToCanvas(GameCanvas);
            resources.Add(resource);
        }

        private bool IsResourcePositionValid(double x, double y, double minDistance = 100)
        {
            // Проверяем расстояние до других ресурсов
            foreach (var resource in resources)
            {
                double distance = Math.Sqrt(Math.Pow(resource.X - x, 2) + Math.Pow(resource.Y - y, 2));
                if (distance < minDistance)
                    return false;
            }

            // Проверяем расстояние до плавилен
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt)
                {
                    double distance = Math.Sqrt(Math.Pow(smelter.X - x, 2) + Math.Pow(smelter.Y - y, 2));
                    if (distance < minDistance)
                        return false;
                }
            }

            // Проверяем расстояние до добытчиков
            foreach (var miner in miners)
            {
                if (miner.IsBuilt)
                {
                    double distance = Math.Sqrt(Math.Pow(miner.X - x, 2) + Math.Pow(miner.Y - y, 2));
                    if (distance < minDistance)
                        return false;
                }
            }

            // Проверяем расстояние до конвейеров
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt)
                {
                    double distance = Math.Sqrt(Math.Pow(conveyor.X - x, 2) + Math.Pow(conveyor.Y - y, 2));
                    if (distance < minDistance)
                        return false;
                }
            }

            return true;
        }

        private void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (isBuildingMode)
                    {
                        CancelBuildingMode();
                    }
                    else if (isConnectingConveyor)
                    {
                        CancelConveyorConnection();
                    }
                    else if (isDrawingConveyorLine)
                    {
                        CancelConveyorLineMode();
                    }
                    else
                    {
                        this.Close();
                    }
                    break;
                case Key.W:
                case Key.Up:
                    isUpPressed = true;
                    break;
                case Key.S:
                case Key.Down:
                    isDownPressed = true;
                    break;
                case Key.A:
                case Key.Left:
                    isLeftPressed = true;
                    break;
                case Key.D:
                case Key.Right:
                    isRightPressed = true;
                    break;
                case Key.Space:
                    isMiningPressed = true;
                    break;
                case Key.R:
                    SpawnRandomResource();
                    break;
                case Key.I:
                    ShowInventoryInfo();
                    break;
                case Key.Tab:
                    // ИСПРАВЛЕНИЕ: Проверяем оба режима
                    if (!isBuildingMode && !isConnectingConveyor && !isDrawingConveyorLine)
                    {
                        OpenBuildMenu();
                    }
                    else
                    {
                        // Отменяем оба режима, если они активны
                        if (isBuildingMode)
                            CancelBuildingMode();
                        if (isConnectingConveyor)
                            CancelConveyorConnection();
                        if (isDrawingConveyorLine)
                            CancelConveyorLineMode();
                    }
                    e.Handled = true;
                    break;
                case Key.L: // Линия конвейеров
                    if (!isBuildingMode && !isConnectingConveyor)
                    {
                        StartConveyorLineMode();
                    }
                    break;
                case Key.C: // Connect - запустить соединение
                    if (isBuildingMode)
                    {
                        CancelBuildingMode();
                    }
                    else if (isDrawingConveyorLine)
                    {
                        CancelConveyorLineMode();
                    }

                    // Если уже в режиме соединения - отменяем
                    if (isConnectingConveyor)
                    {
                        CancelConveyorConnection();
                    }
                    else
                    {
                        StartConveyorConnectionMode();
                    }
                    break;
                case Key.T: // Test - тестовая расстановка
                    CreateTestSetup();
                    break;
            }
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

        private void StartBuildingMode(string buildingType)
        {
            isBuildingMode = true;
            buildingToPlace = buildingType;
            CloseBuildMenu();

            // Создаем превью здания
            buildingPreview = new Image
            {
                Width = buildingType == "smelter" ? 150 :
                       buildingType == "miner" ? 80 : 80,
                Height = buildingType == "smelter" ? 150 :
                        buildingType == "miner" ? 80 : 80,
                Opacity = 0.7,
                Source = LoadBuildingPreview(buildingType)
            };
            GameCanvas.Children.Add(buildingPreview);
            Canvas.SetZIndex(buildingPreview, 99);

            BuildHint.Visibility = Visibility.Visible;
            if (buildingType == "smelter")
            {
                BuildHintText.Text = "Кликните на место для постройки плавильни (не дальше 100px от игрока)";
            }
            else if (buildingType == "miner")
            {
                BuildHintText.Text = "Кликните НА РЕСУРС для постройки добытчика (не дальше 100px от игрока)";
            }
            else if (buildingType == "conveyor")
            {
                BuildHintText.Text = "Кликните для постройки конвейера (выберите направление)";
            }
        }

        private BitmapImage LoadBuildingPreview(string buildingType)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string fileName = buildingType switch
            {
                "smelter" => "Smelter.png",
                "miner" => "Mining.png",
                "conveyor" => "conveyor\\down_1.png", // Исправленный путь
                _ => "default.png"
            };

            string filePath = Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            // Заглушка
            return CreatePlaceholderBuildingPreview(buildingType);
        }

        private BitmapImage CreatePlaceholderBuildingPreview(string buildingType)
        {
            int size = buildingType == "smelter" ? 150 : 80;
            string text = buildingType switch
            {
                "smelter" => "SM",
                "miner" => "MI",
                "conveyor" => "CV",
                _ => "??"
            };

            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                Brush color = buildingType switch
                {
                    "smelter" => Brushes.DarkGray,
                    "miner" => Brushes.DarkBlue,
                    "conveyor" => Brushes.DarkGreen,
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, size, size));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, size, size));

                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(size / 2 - 15, size / 2 - 10));
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

            // Отменяем также режим линии, если он активен
            if (isDrawingConveyorLine)
            {
                CancelConveyorLineMode();
            }
        }

        private void CancelConveyorConnection()
        {
            isConnectingConveyor = false;
            currentConveyor = null;
            conveyorSourceMiner = null;
            conveyorTargetSmelter = null;
            BuildHint.Visibility = Visibility.Collapsed;
        }

        private bool HasBuildingResources(string buildingType)
        {
            if (buildingType == "smelter")
            {
                int stoneCount = 0;
                int coalCount = 0;

                foreach (var slot in player.Inventory)
                {
                    if (slot.Type == ResourceType.Stone)
                        stoneCount += slot.Count;
                    if (slot.Type == ResourceType.Coal)
                        coalCount += slot.Count;
                }

                return stoneCount >= 10 && coalCount >= 5;
            }
            else if (buildingType == "miner")
            {
                int ironIngotCount = 0;
                int copperIngotCount = 0;

                foreach (var slot in player.Inventory)
                {
                    if (slot.Type == ResourceType.IronIngot)
                        ironIngotCount += slot.Count;
                    if (slot.Type == ResourceType.CopperIngot)
                        copperIngotCount += slot.Count;
                }

                return ironIngotCount >= 5 && copperIngotCount >= 5;
            }
            else if (buildingType == "conveyor")
            {
                int ironIngotCount = 0;
                foreach (var slot in player.Inventory)
                {
                    if (slot.Type == ResourceType.IronIngot)
                        ironIngotCount += slot.Count;
                }
                return ironIngotCount >= 2;
            }

            return false;
        }

        private bool RemoveBuildingResources(string buildingType)
        {
            if (buildingType == "smelter")
            {
                return player.RemoveBuildingResources();
            }
            else if (buildingType == "miner")
            {
                return player.RemoveMinerResources();
            }
            else if (buildingType == "conveyor")
            {
                return player.RemoveResources(ResourceType.IronIngot, 2);
            }

            return false;
        }

        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isBuildingMode && buildingPreview != null)
            {
                var position = e.GetPosition(GameCanvas);
                double offsetX = buildingToPlace == "smelter" ? 75 : 40;
                double offsetY = buildingToPlace == "smelter" ? 75 : 40;

                Canvas.SetLeft(buildingPreview, position.X - offsetX);
                Canvas.SetTop(buildingPreview, position.Y - offsetY);

                // Проверяем расстояние до игрока
                double distance = Math.Sqrt(
                    Math.Pow(position.X - (player.X + player.Width / 2), 2) +
                    Math.Pow(position.Y - (player.Y + player.Height / 2), 2));

                bool isValidPosition = distance <= 100;

                // Для добытчика дополнительно проверяем, что он на ресурсе
                if (buildingToPlace == "miner")
                {
                    // Проверяем, есть ли ресурс под курсором
                    bool isOnResource = false;
                    foreach (var resource in resources)
                    {
                        if (resource.IsPointInside(new Point(position.X, position.Y)))
                        {
                            isOnResource = true;
                            break;
                        }
                    }

                    isValidPosition = isValidPosition && isOnResource;

                    if (isOnResource)
                    {
                        buildingPreview.Opacity = 0.7;
                        BuildHintText.Text = "Кликните для постройки на этом ресурсе";
                    }
                    else
                    {
                        buildingPreview.Opacity = 0.3;
                        BuildHintText.Text = "Добытчик должен быть построен НА РЕСУРСЕ!";
                    }
                }
                else
                {
                    if (distance <= 100)
                    {
                        buildingPreview.Opacity = 0.7;
                        if (buildingToPlace == "smelter")
                            BuildHintText.Text = "Кликните для постройки";
                        else if (buildingToPlace == "conveyor")
                            BuildHintText.Text = "Кликните для постройки конвейера";
                    }
                    else
                    {
                        buildingPreview.Opacity = 0.3;
                        BuildHintText.Text = "Слишком далеко от игрока (макс. 100px)";
                    }
                }
            }
        }

        private void ShowInventoryInfo()
        {
            string info = "Инвентарь: ";
            for (int i = 0; i < player.Inventory.Length; i++)
            {
                var slot = player.Inventory[i];
                if (slot.Type != ResourceType.None)
                {
                    info += $"[{slot.Type}: {slot.Count}] ";
                }
            }
            ShowMessage(info);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                case Key.Up:
                    isUpPressed = false;
                    break;
                case Key.S:
                case Key.Down:
                    isDownPressed = false;
                    break;
                case Key.A:
                case Key.Left:
                    isLeftPressed = false;
                    break;
                case Key.D:
                case Key.Right:
                    isRightPressed = false;
                    break;
                case Key.Space:
                    isMiningPressed = false;
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BuildSmelterButton_Click(object sender, RoutedEventArgs e)
        {
            StartBuildingMode("smelter");
        }

        private void BuildMinerButton_Click(object sender, RoutedEventArgs e)
        {
            StartBuildingMode("miner");
        }

        private void BuildConveyorButton_Click(object sender, RoutedEventArgs e)
        {
            StartBuildingMode("conveyor");
        }

        private void BuildConveyorLineButton_Click(object sender, RoutedEventArgs e)
        {
            CloseBuildMenu();
            StartConveyorLineMode();
        }

        private void CancelBuildButton_Click(object sender, RoutedEventArgs e)
        {
            CloseBuildMenu();
        }

        private void InitializePlayer()
        {
            double startX = this.ActualWidth / 2 - 25;
            double startY = this.ActualHeight / 2 - 25;

            player = new Player(startX, startY, 50, 50);
            player.AddToCanvas(GameCanvas);
        }

        private void GameCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(GameCanvas);
            Point clickPoint = new Point(position.X, position.Y);

            // ===== РЕЖИМ РИСОВАНИЯ ЛИНИИ КОНВЕЙЕРОВ =====
            if (isDrawingConveyorLine && e.LeftButton == MouseButtonState.Pressed)
            {
                if (!hasLineStart)
                {
                    lineStartPoint = clickPoint;
                    hasLineStart = true;

                    BuildHintText.Text = "Начальная точка выбрана. Выберите направление.";
                    ShowDirectionSelectionForLine(clickPoint);
                }
                else if (conveyorLineDirection.HasValue)
                {
                    lineEndPoint = clickPoint;
                    CreateConveyorLine();
                }

                return;
            }


            // Обработка кликов для соединения конвейера
            if (isConnectingConveyor && e.LeftButton == MouseButtonState.Pressed)
            {
                HandleConveyorConnectionClick(clickPoint);
                return;
            }

            // Обработка кликов для постройки зданий
            if (isBuildingMode && e.LeftButton == MouseButtonState.Pressed)
            {
                double distance = Math.Sqrt(
                    Math.Pow(position.X - (player.X + player.Width / 2), 2) +
                    Math.Pow(position.Y - (player.Y + player.Height / 2), 2));

                if (distance <= 100)
                {
                    if (buildingToPlace == "smelter")
                    {
                        if (HasBuildingResources("smelter"))
                        {
                            Smelter smelter = new Smelter(position.X - 75, position.Y - 75, player);

                            if (RemoveBuildingResources("smelter"))
                            {
                                smelter.Build();
                                smelter.AddToCanvas(GameCanvas);
                                smelters.Add(smelter);

                                ShowMessage("Плавильня построена!");
                                CancelBuildingMode();
                            }
                        }
                        else
                        {
                            ShowMessage("Недостаточно ресурсов для постройки плавильни!\nНужно: 10 камня + 5 угля");
                        }
                    }
                    else if (buildingToPlace == "miner")
                    {
                        // Находим ресурс под курсором
                        Resource targetResource = null;
                        foreach (var resource in resources)
                        {
                            if (resource.IsPointInside(clickPoint))
                            {
                                targetResource = resource;
                                break;
                            }
                        }

                        if (targetResource != null)
                        {
                            if (HasBuildingResources("miner"))
                            {
                                Miner miner = new Miner(position.X - 40, position.Y - 40, player);
                                miner.SetTargetResource(targetResource);

                                if (RemoveBuildingResources("miner"))
                                {
                                    miner.Build();
                                    miner.AddToCanvas(GameCanvas);
                                    miners.Add(miner);

                                    ShowMessage($"Добытчик построен на {GetResourceName(targetResource.Type)}!");
                                    CancelBuildingMode();
                                }
                            }
                            else
                            {
                                ShowMessage("Недостаточно ресурсов для постройки добытчика!\nНужно: 5 железных слитков + 5 медных слитков");
                            }
                        }
                        else
                        {
                            ShowMessage("Добытчик должен быть построен НА РЕСУРСЕ!");
                        }
                    }
                    else if (buildingToPlace == "conveyor")
                    {
                        // Для конвейера нужно выбрать направление
                        ShowDirectionSelectionMenu(clickPoint);
                    }
                }
            }
        }

        private void ShowDirectionSelectionMenu(Point position)
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

            // Кнопки направлений
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
                    BuildConveyorAtPosition(position, selectedDirection);
                    directionMenu.Close();
                };

                Grid.SetRow(button, i < 2 ? 0 : 1);
                Grid.SetColumn(button, i % 2);
                grid.Children.Add(button);
            }

            directionMenu.Content = grid;
            directionMenu.ShowDialog();
        }

        private void ShowDirectionSelectionForLine(Point position)
        {
            var directionMenu = new Window
            {
                Title = "Выберите направление линии конвейеров",
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
                int index = i; // АЖНО: фикс замыкания

                var button = new Button
                {
                    Content = directionNames[index],
                    Margin = new Thickness(5),
                    Tag = directions[index]
                };

                button.Click += (s, args) =>
                {
                    conveyorLineDirection = (Direction)((Button)s).Tag;
                    directionMenu.Close();

                    BuildHintText.Text =
                        $"Направление: {directionNames[index]}. Теперь кликните конечную точку линии.";
                };

                Grid.SetRow(button, index < 2 ? 0 : 1);
                Grid.SetColumn(button, index % 2);
                grid.Children.Add(button);
            }

            directionMenu.Content = grid;
            directionMenu.ShowDialog();
        }


        private void BuildConveyorAtPosition(Point position, Direction direction)
        {
            if (HasBuildingResources("conveyor"))
            {
                Conveyor conveyor = new Conveyor(position.X - 20, position.Y - 20, direction); // Центрируем по размеру 40x40

                if (RemoveBuildingResources("conveyor"))
                {
                    conveyor.Build();
                    conveyor.AddToCanvas(GameCanvas);
                    conveyors.Add(conveyor);

                    // Начинаем процесс соединения конвейера
                    StartConveyorConnection(conveyor);

                    ShowMessage("Конвейер построен! Теперь соедините его с добытчиком и плавильней.");
                    CancelBuildingMode();
                }
            }
            else
            {
                ShowMessage("Недостаточно ресурсов для постройки конвейера!\nНужно: 2 железных слитка");
            }
        }

        private void StartConveyorConnection(Conveyor conveyor)
        {
            isConnectingConveyor = true;
            currentConveyor = conveyor;
            conveyorSourceMiner = null;
            conveyorTargetSmelter = null;

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "Кликните на добытчик (источник), затем на плавильню (приемник)";
        }

        private void StartConveyorConnectionMode()
        {
            if (conveyors.Count == 0)
            {
                ShowMessage("Нет конвейеров для соединения!");
                return;
            }

            isConnectingConveyor = true;
            currentConveyor = null;
            conveyorSourceMiner = null;
            conveyorTargetSmelter = null;

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "РЕЖИМ СОЕДИНЕНИЯ: 1. Выберите ПЕРВЫЙ конвейер в линии, 2. Выберите ПОСЛЕДНИЙ конвейер, 3. Выберите майнер, 4. Выберите плавильню";
        }

        private void StartConveyorLineMode()
        {
            isDrawingConveyorLine = true;

            lineStartPoint = new Point();
            lineEndPoint = new Point();
            conveyorLineDirection = null;
            hasLineStart = false;

            // Удаляем временные конвейеры (если были)
            foreach (var conveyor in tempConveyors)
            {
                conveyor.RemoveFromCanvas(GameCanvas);
            }
            tempConveyors.Clear();

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text =
                "РЕЖИМ ЛИНИИ:\n1. Кликните начальную точку\n2. Выберите направление\n3. Кликните конечную точку";
        }


        private void CancelConveyorLineMode()
        {
            isDrawingConveyorLine = false;

            lineStartPoint = new Point();
            lineEndPoint = new Point();
            conveyorLineDirection = null;
            hasLineStart = false;

            foreach (var conveyor in tempConveyors)
            {
                conveyor.RemoveFromCanvas(GameCanvas);
            }
            tempConveyors.Clear();

            BuildHint.Visibility = Visibility.Collapsed;
        }


        // В MainWindow добавьте:
        private Conveyor lastConveyorInLine = null;

        private void HandleConveyorConnectionClick(Point position)
        {
            // Шаг 1: Выбор конвейера
            if (currentConveyor == null)
            {
                foreach (var conveyor in conveyors)
                {
                    if (conveyor.IsBuilt && conveyor.IsPointInside(position))
                    {
                        currentConveyor = conveyor;
                        BuildHintText.Text = "Конвейер выбран. Теперь выберите майнер.";
                        return;
                    }
                }
            }

            // Шаг 2: Выбор майнера
            else if (conveyorSourceMiner == null)
            {
                foreach (var miner in miners)
                {
                    if (miner.IsBuilt && miner.IsPointInside(position))
                    {
                        conveyorSourceMiner = miner;
                        BuildHintText.Text = "Майнер выбран. Теперь выберите плавильню.";
                        return;
                    }
                }
            }

            // Шаг 3: Выбор плавильни
            else if (conveyorTargetSmelter == null)
            {
                foreach (var smelter in smelters)
                {
                    if (smelter.IsBuilt && smelter.IsPointInside(position))
                    {
                        conveyorTargetSmelter = smelter;
                        // Соединяем!
                        currentConveyor.ConnectBuildings(conveyorSourceMiner, conveyorTargetSmelter);
                        ShowMessage("Конвейер соединен!");
                        CompleteConveyorConnection();
                        return;
                    }
                }
            }
        }

        private void ConnectConveyorLine(Conveyor firstConveyor, Conveyor lastConveyor, Miner miner, Smelter smelter)
        {
            // Находим все конвейеры, которые образуют непрерывную линию от firstConveyor до lastConveyor
            var connectedConveyors = FindConveyorPath(firstConveyor, lastConveyor);

            if (connectedConveyors.Count == 0)
            {
                ShowMessage("Не удалось найти непрерывную линию между выбранными конвейерами!");
                return;
            }

            // Соединяем каждый конвейер
            foreach (var conveyor in connectedConveyors)
            {
                conveyor.ConnectBuildings(miner, smelter);
            }

            ShowMessage($"Соединена линия из {connectedConveyors.Count} конвейеров!");
        }

        private List<Conveyor> FindConveyorPath(Conveyor start, Conveyor end)
        {
            var path = new List<Conveyor>();
            var visited = new HashSet<Conveyor>();
            var queue = new Queue<Conveyor>();
            var parent = new Dictionary<Conveyor, Conveyor>();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = null;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end)
                {
                    // Восстанавливаем путь
                    var node = end;
                    while (node != null)
                    {
                        path.Insert(0, node);
                        node = parent[node];
                    }
                    return path;
                }

                // Находим соседние конвейеры
                foreach (var conveyor in conveyors)
                {
                    if (!visited.Contains(conveyor) && IsConveyorsAdjacent(current, conveyor))
                    {
                        queue.Enqueue(conveyor);
                        visited.Add(conveyor);
                        parent[conveyor] = current;
                    }
                }
            }

            return path; // Путь не найден
        }

        private bool IsConveyorsAdjacent(Conveyor c1, Conveyor c2)
        {
            double centerX1 = c1.X + c1.Width / 2;
            double centerY1 = c1.Y + c1.Height / 2;
            double centerX2 = c2.X + c2.Width / 2;
            double centerY2 = c2.Y + c2.Height / 2;

            // Максимальное расстояние между центрами для соединения
            const double maxDistance = 50; // 40 + 10 допуск

            double distance = Math.Sqrt(
                Math.Pow(centerX2 - centerX1, 2) +
                Math.Pow(centerY2 - centerY1, 2));

            return distance <= maxDistance;
        }

        private Miner FindNearestMinerToConveyorLine(List<Conveyor> conveyorLine)
        {
            if (miners.Count == 0) return null;

            Miner nearest = null;
            double minDistance = double.MaxValue;

            foreach (var conveyor in conveyorLine)
            {
                foreach (var miner in miners)
                {
                    if (miner.IsBuilt && miner.IsPlacedOnResource)
                    {
                        double distance = Math.Sqrt(
                            Math.Pow(conveyor.X - miner.X, 2) +
                            Math.Pow(conveyor.Y - miner.Y, 2));

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearest = miner;
                        }
                    }
                }
            }

            return nearest;
        }

        private void CreateConveyorLine()
        {
            if (!hasLineStart || !conveyorLineDirection.HasValue)
            {
                ShowMessage("Не выбрана начальная точка или направление");
                CancelConveyorLineMode();
                return;
            }

            if (lineStartPoint == lineEndPoint)
            {
                ShowMessage("Начальная и конечная точки совпадают");
                CancelConveyorLineMode();
                return;
            }

            double distance = CalculateLineDistance();
            int conveyorCount = (int)(distance / 40);

            if (conveyorCount <= 0)
            {
                ShowMessage("Слишком короткая линия");
                CancelConveyorLineMode();
                return;
            }

            int requiredIron = conveyorCount * 2;
            if (!player.HasResources(ResourceType.IronIngot, requiredIron))
            {
                ShowMessage($"Недостаточно ресурсов! Нужно {requiredIron} железных слитков");
                CancelConveyorLineMode();
                return;
            }

            double stepX = 0;
            double stepY = 0;

            double currentX = lineStartPoint.X - 20;
            double currentY = lineStartPoint.Y - 20;

            switch (conveyorLineDirection.Value)
            {
                case Direction.Right: stepX = 40; break;
                case Direction.Left: stepX = -40; break;
                case Direction.Down: stepY = 40; break;
                case Direction.Up: stepY = -40; break;
            }

            for (int i = 0; i < conveyorCount; i++)
            {
                Conveyor conveyor = new Conveyor(currentX, currentY, conveyorLineDirection.Value);
                conveyor.Build();
                conveyor.AddToCanvas(GameCanvas);
                conveyors.Add(conveyor);

                currentX += stepX;
                currentY += stepY;
            }

            player.RemoveResources(ResourceType.IronIngot, requiredIron);

            ShowMessage($"Линия из {conveyorCount} конвейеров построена! Нажмите C для соединения.");
            CancelConveyorLineMode();
        }

        private double CalculateLineDistance()
        {
            if (!conveyorLineDirection.HasValue)
                return 0;

            switch (conveyorLineDirection.Value)
            {
                case Direction.Left:
                case Direction.Right:
                    return Math.Abs(lineEndPoint.X - lineStartPoint.X);
                case Direction.Up:
                case Direction.Down:
                    return Math.Abs(lineEndPoint.Y - lineStartPoint.Y);
                default:
                    return 0;
            }
        }

        private bool HasBuildingResourcesForConveyorLine()
        {
            double distance = CalculateLineDistance();
            int conveyorCount = (int)(distance / 40);
            return player.HasResources(ResourceType.IronIngot, conveyorCount * 2);
        }

        private void CompleteConveyorConnection()
        {
            isConnectingConveyor = false;
            currentConveyor = null;
            conveyorSourceMiner = null;
            conveyorTargetSmelter = null;
            BuildHint.Visibility = Visibility.Collapsed;
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

        private void CreateTestSetup()
        {
            // 1. Создаем ресурс
            Resource testResource = new Resource(100, 100, ResourceType.Iron);
            testResource.AddToCanvas(GameCanvas);
            resources.Add(testResource);

            // 2. Создаем майнер НА ресурсе
            Miner miner = new Miner(100, 100, player);
            miner.SetTargetResource(testResource);
            miner.Build();
            miner.AddToCanvas(GameCanvas);
            miners.Add(miner);

            // 3. Создаем плавильню СПРАВА от майнера
            Smelter smelter = new Smelter(180, 90, player); // 100 + 80 (майнер) = 180
            smelter.Build();
            smelter.AddToCanvas(GameCanvas);
            smelters.Add(smelter);

            // 4. Создаем конвейер МЕЖДУ ними (направление ВПРАВО)
            Conveyor conveyor = new Conveyor(140, 100, Direction.Right); // X=100+40=140
            conveyor.Build();
            conveyor.AddToCanvas(GameCanvas);
            conveyors.Add(conveyor);

            // 5. СРАЗУ соединяем (не ждем кликов)
            conveyor.ConnectBuildings(miner, smelter);

            ShowMessage("ТЕСТОВАЯ СХЕМА СОЗДАНА: Майнер → Конвейер(вправо) → Плавильня");
        }
    }
}