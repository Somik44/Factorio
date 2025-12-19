// MainWindow.xaml.cs
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

        // Размер сетки
        private const int GridSize = 30;

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
        private bool isGridVisible = true;

        // Состояние соединения конвейера
        private bool isConnectingConveyor = false;
        private Conveyor currentConveyor = null;
        private Miner conveyorSourceMiner = null;
        private Smelter conveyorTargetSmelter = null;

        // Состояние рисования линии конвейеров
        private bool isDrawingConveyorLine = false;
        private Point lineStartPoint;
        private Point lineEndPoint;
        private Direction? conveyorLineDirection = null;
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

        private Point SnapToGrid(Point point)
        {
            double snappedX = Math.Floor(point.X / GridSize) * GridSize;
            double snappedY = Math.Floor(point.Y / GridSize) * GridSize;
            return new Point(snappedX, snappedY);
        }

        private Point GetBuildingCenterOffset(string buildingType)
        {
            return buildingType switch
            {
                "smelter" => new Point(0, 0),    // Плавильня (180x150) - 6x5 клеток
                "miner" => new Point(0, 0),      // Добытчик (90x90) - 3x3 клетки
                "conveyor" => new Point(0, 0),   // Конвейер (30x30) - 1x1 клетка
                _ => new Point(0, 0)
            };
        }

        private Size GetBuildingSize(string buildingType)
        {
            return buildingType switch
            {
                "smelter" => new Size(180, 150),   // 6x5 клеток
                "miner" => new Size(90, 90),       // 3x3 клетки
                "conveyor" => new Size(30, 30),    // 1x1 клетка
                _ => new Size(0, 0)
            };
        }

        private bool IsBuildingPlacementValid(double x, double y, string buildingType, bool checkDistance = true)
        {
            var size = GetBuildingSize(buildingType);

            // Для конвейеров не проверяем расстояние до игрока
            if (checkDistance && buildingType != "conveyor")
            {
                double playerCenterX = player.X + player.Width / 2;
                double playerCenterY = player.Y + player.Height / 2;
                double buildingCenterX = x + size.Width / 2;
                double buildingCenterY = y + size.Height / 2;

                double distance = Math.Sqrt(
                    Math.Pow(playerCenterX - buildingCenterX, 2) +
                    Math.Pow(playerCenterY - buildingCenterY, 2));

                if (distance > 300) // Максимальное расстояние в пикселях
                {
                    return false;
                }
            }

            // Проверяем, не выходит ли за границы
            if (x < 0 || y < 0 || x + size.Width > GameCanvas.ActualWidth || y + size.Height > GameCanvas.ActualHeight)
            {
                return false;
            }

            // Проверяем столкновение с другими зданиями
            Rect newBuildingRect = new Rect(x, y, size.Width, size.Height);

            // Проверяем плавильни
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt)
                {
                    Rect smelterRect = new Rect(smelter.X, smelter.Y, smelter.Width, smelter.Height);
                    if (RectanglesOverlapWithMargin(newBuildingRect, smelterRect))
                        return false;
                }
            }

            // Проверяем добытчики
            foreach (var miner in miners)
            {
                if (miner.IsBuilt)
                {
                    Rect minerRect = new Rect(miner.X, miner.Y, miner.Width, miner.Height);
                    if (RectanglesOverlapWithMargin(newBuildingRect, minerRect))
                        return false;
                }
            }

            // Проверяем конвейеры
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt)
                {
                    Rect conveyorRect = new Rect(conveyor.X, conveyor.Y, conveyor.Width, conveyor.Height);
                    if (RectanglesOverlapWithMargin(newBuildingRect, conveyorRect))
                        return false;
                }
            }

            // Проверяем ресурсы (для добытчика это нужно, для других - нет)
            if (buildingType != "miner")
            {
                foreach (var resource in resources)
                {
                    Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                    if (RectanglesOverlapWithMargin(newBuildingRect, resourceRect))
                        return false;
                }
            }

            return true;
        }

        // Проверяет, действительно ли прямоугольники ПЕРЕКРЫВАЮТСЯ, а не просто касаются
        private bool RectanglesOverlapWithMargin(Rect rect1, Rect rect2)
        {
            // Проверяем, пересекаются ли прямоугольники
            if (!rect1.IntersectsWith(rect2))
                return false;

            // Если пересекаются, то получаем прямоугольник пересечения
            Rect intersection = Rect.Intersect(rect1, rect2);

            // Если площадь пересечения больше 0, то считаем, что есть перекрытие
            return intersection.Width > 0 && intersection.Height > 0;
        }

        private void ShowGrid()
        {
            // Очищаем предыдущую сетку
            var gridElements = GameCanvas.Children.OfType<Rectangle>().Where(r => r.Name == "GridLine").ToList();
            foreach (var element in gridElements)
            {
                GameCanvas.Children.Remove(element);
            }

            if (!isGridVisible) return;

            // Создаем новую сетку с БОЛЬШИМ ZIndex
            for (int x = 0; x < this.ActualWidth; x += GridSize)
            {
                var verticalLine = new Rectangle
                {
                    Name = "GridLine",
                    Width = 1,
                    Height = this.ActualHeight,
                    Fill = Brushes.Red,
                    Opacity = 1  // Увеличим прозрачность
                };
                Canvas.SetLeft(verticalLine, x);
                Canvas.SetTop(verticalLine, 0);
                Canvas.SetZIndex(verticalLine, 10);  // Увеличиваем ZIndex
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
                    Opacity = 1  // Увеличим прозрачность
                };
                Canvas.SetLeft(horizontalLine, 0);
                Canvas.SetTop(horizontalLine, y);
                Canvas.SetZIndex(horizontalLine, 10);
                GameCanvas.Children.Add(horizontalLine);
            }
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
                    if (!isBuildingMode && !isConnectingConveyor && !isDrawingConveyorLine)
                    {
                        OpenBuildMenu();
                    }
                    else
                    {
                        if (isBuildingMode)
                            CancelBuildingMode();
                        if (isConnectingConveyor)
                            CancelConveyorConnection();
                        if (isDrawingConveyorLine)
                            CancelConveyorLineMode();
                    }
                    e.Handled = true;
                    break;
                case Key.L:
                    if (!isBuildingMode && !isConnectingConveyor)
                    {
                        StartConveyorLineMode();
                    }
                    break;
                case Key.C:
                    if (isBuildingMode)
                    {
                        CancelBuildingMode();
                    }
                    else if (isDrawingConveyorLine)
                    {
                        CancelConveyorLineMode();
                    }

                    if (isConnectingConveyor)
                    {
                        CancelConveyorConnection();
                    }
                    else
                    {
                        StartConveyorConnectionMode();
                    }
                    break;
                case Key.T:
                    CreateTestSetup();
                    break;
                case Key.G:
                    ToggleGrid();
                    break;
            }
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

        private void StartBuildingMode(string buildingType)
        {
            isBuildingMode = true;
            buildingToPlace = buildingType;
            CloseBuildMenu();

            // Создаем превью здания
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
            if (buildingType == "smelter")
            {
                BuildHintText.Text = "Кликните на место для постройки плавильни";
            }
            else if (buildingType == "miner")
            {
                BuildHintText.Text = "Кликните НА РЕСУРС для постройки добытчика";
            }
            else if (buildingType == "conveyor")
            {
                BuildHintText.Text = "Кликните для постройки конвейера (1x1 клетка)";
            }
        }

        private BitmapImage LoadBuildingPreview(string buildingType)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string fileName = buildingType switch
            {
                "smelter" => "Smelter.png",
                "miner" => "Mining.png",
                "conveyor" => "conveyor\\down_1.png",
                _ => "default.png"
            };

            string filePath = System.IO.Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreatePlaceholderBuildingPreview(buildingType);
        }

        private BitmapImage CreatePlaceholderBuildingPreview(string buildingType)
        {
            var size = GetBuildingSize(buildingType);
            int width = (int)size.Width;
            int height = (int)size.Height;

            string text = buildingType switch
            {
                "smelter" => "SM",
                "miner" => "MI",
                "conveyor" => "CV",
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
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, width, height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, width, height));

                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White,
                    1.0);

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
                var snappedPosition = SnapToGrid(position);
                var offset = GetBuildingCenterOffset(buildingToPlace);
                var size = GetBuildingSize(buildingToPlace);

                // Устанавливаем позицию превью с учетом смещения
                Canvas.SetLeft(buildingPreview, snappedPosition.X - offset.X);
                Canvas.SetTop(buildingPreview, snappedPosition.Y - offset.Y);

                // Проверяем валидность позиции
                bool isValidPosition = IsBuildingPlacementValid(
                    snappedPosition.X - offset.X,
                    snappedPosition.Y - offset.Y,
                    buildingToPlace);

                // Для добытчика дополнительно проверяем, что он на ресурсе
                if (buildingToPlace == "miner" && isValidPosition)
                {
                    bool isOnResource = false;
                    Rect minerRect = new Rect(
                        snappedPosition.X - offset.X,
                        snappedPosition.Y - offset.Y,
                        size.Width,
                        size.Height);

                    foreach (var resource in resources)
                    {
                        Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                        if (RectanglesOverlapWithMargin(minerRect, resourceRect))
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
                    buildingPreview.Opacity = isValidPosition ? 0.7 : 0.3;
                    BuildHintText.Text = isValidPosition ?
                        "Кликните для постройки" :
                        "Нельзя построить здесь (занято, слишком далеко или вне границ)";
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
            var snappedPosition = SnapToGrid(position);
            Point clickPoint = new Point(snappedPosition.X, snappedPosition.Y);

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
                var offset = GetBuildingCenterOffset(buildingToPlace);
                double buildingX = clickPoint.X - offset.X;
                double buildingY = clickPoint.Y - offset.Y;

                if (IsBuildingPlacementValid(buildingX, buildingY, buildingToPlace))
                {
                    if (buildingToPlace == "smelter")
                    {
                        if (HasBuildingResources("smelter"))
                        {
                            Smelter smelter = new Smelter(buildingX, buildingY, player);

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
                        var size = GetBuildingSize("miner");
                        Rect minerRect = new Rect(buildingX, buildingY, size.Width, size.Height);

                        foreach (var resource in resources)
                        {
                            Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                            if (RectanglesOverlapWithMargin(minerRect, resourceRect))
                            {
                                targetResource = resource;
                                break;
                            }
                        }

                        if (targetResource != null)
                        {
                            if (HasBuildingResources("miner"))
                            {
                                Miner miner = new Miner(buildingX, buildingY, player);
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
                int index = i;

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
                // Конвейер занимает 1 клетку, БЕЗ центрирования
                double conveyorX = position.X;
                double conveyorY = position.Y;

                if (IsBuildingPlacementValid(conveyorX, conveyorY, "conveyor"))
                {
                    Conveyor conveyor = new Conveyor(conveyorX, conveyorY, direction);

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
                    ShowMessage("Нельзя построить конвейер здесь!");
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
            var connectedConveyors = FindConveyorPath(firstConveyor, lastConveyor);

            if (connectedConveyors.Count == 0)
            {
                ShowMessage("Не удалось найти непрерывную линию между выбранными конвейерами!");
                return;
            }

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
                    var node = end;
                    while (node != null)
                    {
                        path.Insert(0, node);
                        node = parent[node];
                    }
                    return path;
                }

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

            return path;
        }

        private bool IsConveyorsAdjacent(Conveyor c1, Conveyor c2)
        {
            double centerX1 = c1.X + c1.Width / 2;
            double centerY1 = c1.Y + c1.Height / 2;
            double centerX2 = c2.X + c2.Width / 2;
            double centerY2 = c2.Y + c2.Height / 2;

            const double maxDistance = 50;

            double distance = Math.Sqrt(
                Math.Pow(centerX2 - centerX1, 2) +
                Math.Pow(centerY2 - centerY1, 2));

            return distance <= maxDistance;
        }

        private void CreateConveyorLine()
        {
            if (!hasLineStart || !conveyorLineDirection.HasValue)
            {
                ShowMessage("Не выбрана начальная точка или направление");
                CancelConveyorLineMode();
                return;
            }

            // Выравниваем точки по сетке
            lineStartPoint = SnapToGrid(lineStartPoint);
            lineEndPoint = SnapToGrid(lineEndPoint);

            if (lineStartPoint == lineEndPoint)
            {
                ShowMessage("Начальная и конечная точки совпадают");
                CancelConveyorLineMode();
                return;
            }

            double distance = CalculateLineDistance();
            int conveyorCount = (int)(distance / GridSize) + 1; // +1 чтобы включить начальную точку

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
            double startX = lineStartPoint.X;
            double startY = lineStartPoint.Y;

            switch (conveyorLineDirection.Value)
            {
                case Direction.Right: stepX = GridSize; break;
                case Direction.Left: stepX = -GridSize; break;
                case Direction.Down: stepY = GridSize; break;
                case Direction.Up: stepY = -GridSize; break;
            }

            int builtCount = 0;
            for (int i = 0; i < conveyorCount; i++)
            {
                double conveyorX = startX + stepX * i;
                double conveyorY = startY + stepY * i;

                if (IsBuildingPlacementValid(conveyorX, conveyorY, "conveyor", false))
                {
                    Conveyor conveyor = new Conveyor(conveyorX, conveyorY, conveyorLineDirection.Value);
                    conveyor.Build();
                    conveyor.AddToCanvas(GameCanvas);
                    conveyors.Add(conveyor);
                    builtCount++;
                }
            }

            player.RemoveResources(ResourceType.IronIngot, requiredIron);

            ShowMessage($"Линия из {builtCount} конвейеров построена! Нажмите C для соединения.");
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
            // Добавляем ресурсы в инвентарь игрока
            player.AddResource(ResourceType.CopperIngot, 11);
            player.AddResource(ResourceType.IronIngot, 11);
            player.AddResource(ResourceType.Coal, 27);
            player.AddResource(ResourceType.Stone, 10);


            ShowMessage("Добавлено в инвентарь: 11 медных слитков, 11 железных слитков, 27 угля и 10 камня");
        }

        private void ToggleGridButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleGrid();
        }
    }
}