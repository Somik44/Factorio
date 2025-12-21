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
        private List<ArmsFactory> armsFactories = new List<ArmsFactory>();
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

        // Состояние построения линии конвейеров
        private bool isBuildingLine = false;
        private Point lineStartPoint;
        private bool isLineFirstClick = true;
        private List<Conveyor> linePreviewConveyors = new List<Conveyor>();

        // Состояние соединения конвейера с зданиями
        private bool isConnectingMode = false;
        private object connectionSource = null;
        private object connectionTarget = null;

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
            player.SetSmelters(smelters); // Устанавливаем список плавилен для игрока
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
            // Для конвейера не центрируем, потому что он занимает ровно одну клетку
            if (buildingType == "conveyor")
                return new Point(0, 0);

            // Возвращаем половину размера для центрирования курсора
            var size = GetBuildingSize(buildingType);
            return new Point(size.Width / 2, size.Height / 2);
        }


        private Size GetBuildingSize(string buildingType)
        {
            return buildingType switch
            {
                "smelter" => new Size(180, 150),     // 6x5 клеток
                "miner" => new Size(90, 90),         // 3x3 клетки
                "conveyor" => new Size(30, 30),      // 1x1 клетка
                "arms_factory" => new Size(90, 120), // 3x4 клетки
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

            Rect newBuildingRect = new Rect(x, y, size.Width, size.Height);

            // Для конвейеров используем менее строгие проверки
            if (buildingType == "conveyor")
            {
                // Для конвейера проверяем только пересечение с центрами других объектов
                return IsConveyorPlacementValid(x, y);
            }
            else
            {
                // Для других зданий проверяем столкновения
                return IsRegularBuildingPlacementValid(newBuildingRect, buildingType);
            }
        }

        private bool IsConveyorPlacementValid(double x, double y)
        {
            // Для конвейера проверяем, чтобы центр не совпадал с центрами других зданий/конвейеров
            Point conveyorCenter = new Point(x + 15, y + 15); // 30/2 = 15

            // Проверяем конвейеры
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt)
                {
                    Point existingCenter = new Point(conveyor.X + 15, conveyor.Y + 15);
                    if (PointsAreEqual(conveyorCenter, existingCenter))
                        return false;
                }
            }

            // Проверяем плавильни
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt)
                {
                    // Конвейер может касаться плавильни, но не перекрывать её существенную часть
                    if (IsPointTooCloseToRect(conveyorCenter, smelter.X, smelter.Y, smelter.Width, smelter.Height, 5))
                        return false;
                }
            }

            // Проверяем добытчики
            foreach (var miner in miners)
            {
                if (miner.IsBuilt)
                {
                    if (IsPointTooCloseToRect(conveyorCenter, miner.X, miner.Y, miner.Width, miner.Height, 5))
                        return false;
                }
            }

            // Проверяем оружейные заводы
            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt)
                {
                    if (IsPointTooCloseToRect(conveyorCenter, armsFactory.X, armsFactory.Y, armsFactory.Width, armsFactory.Height, 5))
                        return false;
                }
            }

            return true;
        }

        private bool IsRegularBuildingPlacementValid(Rect newBuildingRect, string buildingType)
        {
            // Проверяем плавильни
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt)
                {
                    Rect smelterRect = new Rect(smelter.X, smelter.Y, smelter.Width, smelter.Height);
                    if (RectanglesIntersectWithMargin(newBuildingRect, smelterRect, 5))
                        return false;
                }
            }

            // Проверяем добытчики
            foreach (var miner in miners)
            {
                if (miner.IsBuilt)
                {
                    Rect minerRect = new Rect(miner.X, miner.Y, miner.Width, miner.Height);
                    if (RectanglesIntersectWithMargin(newBuildingRect, minerRect, 5))
                        return false;
                }
            }

            // Проверяем конвейеры (для обычных зданий проверяем с отступом)
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt)
                {
                    Rect conveyorRect = new Rect(conveyor.X, conveyor.Y, conveyor.Width, conveyor.Height);
                    if (buildingType != "conveyor" && RectanglesIntersectWithMargin(newBuildingRect, conveyorRect, 5))
                        return false;
                }
            }

            // Проверяем оружейные заводы
            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt)
                {
                    Rect armsFactoryRect = new Rect(armsFactory.X, armsFactory.Y, armsFactory.Width, armsFactory.Height);
                    if (RectanglesIntersectWithMargin(newBuildingRect, armsFactoryRect, 5))
                        return false;
                }
            }

            // Проверяем ресурсы (для добытчика это нужно, для других - нет)
            if (buildingType != "miner")
            {
                foreach (var resource in resources)
                {
                    Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);
                    if (RectanglesIntersectWithMargin(newBuildingRect, resourceRect, 5))
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
            // Проверяем, находится ли точка слишком близко к прямоугольнику
            double centerX = rectX + rectWidth / 2;
            double centerY = rectY + rectHeight / 2;

            // Если точка находится внутри прямоугольника (с учетом отступа)
            if (point.X >= rectX - margin && point.X <= rectX + rectWidth + margin &&
                point.Y >= rectY - margin && point.Y <= rectY + rectHeight + margin)
            {
                return true;
            }

            return false;
        }

        private bool RectanglesIntersectWithMargin(Rect rect1, Rect rect2, double margin = 0)
        {
            // Проверяем пересечение с учетом отступа
            Rect expandedRect1 = new Rect(rect1.X - margin, rect1.Y - margin, rect1.Width + 2 * margin, rect1.Height + 2 * margin);
            Rect expandedRect2 = new Rect(rect2.X - margin, rect2.Y - margin, rect2.Width + 2 * margin, rect2.Height + 2 * margin);

            return expandedRect1.IntersectsWith(expandedRect2);
        }


        // Новая функция для проверки пересечения без отступов
        private bool RectanglesOverlap(Rect rect1, Rect rect2)
        {
            return rect1.IntersectsWith(rect2);
        }

        // Функция для проверки пересечения (более строгая, чем касание)
        private bool RectanglesIntersect(Rect rect1, Rect rect2)
        {
            // Разрешаем касание (когда границы совпадают), но запрещаем пересечение
            return rect1.X < rect2.X + rect2.Width &&
                   rect1.X + rect1.Width > rect2.X &&
                   rect1.Y < rect2.Y + rect2.Height &&
                   rect1.Y + rect1.Height > rect2.Y;
        }

        private bool RectanglesOverlapWithMargin(Rect rect1, Rect rect2)
        {
            // Для конвейеров (30x30) разрешаем касание
            if (rect1.Width == 30 && rect1.Height == 30 && rect2.Width == 30 && rect2.Height == 30)
            {
                // Конвейеры могут касаться, но не пересекаться
                return rect1.IntersectsWith(rect2) &&
                       !(rect1.X + rect1.Width <= rect2.X || // справа
                         rect1.X >= rect2.X + rect2.Width || // слева
                         rect1.Y + rect1.Height <= rect2.Y || // снизу
                         rect1.Y >= rect2.Y + rect2.Height);  // сверху
            }

            // Для других зданий добавляем отступ 5 пикселей
            Rect expandedRect1 = new Rect(rect1.X - 5, rect1.Y - 5, rect1.Width + 10, rect1.Height + 10);
            Rect expandedRect2 = new Rect(rect2.X - 5, rect2.Y - 5, rect2.Width + 10, rect2.Height + 10);

            return expandedRect1.IntersectsWith(expandedRect2);
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

            // Создаем новую сетку
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

            // Обновляем все добытчики
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
                player.Move(deltaX, deltaY, direction);
            }
            else
            {
                player.Stop();
            }
        }

        private void SpawnInitialResources()
        {
            int resourceCount = 25;
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
                x = random.Next(50, (int)this.ActualWidth - 100);
                y = random.Next(50, (int)this.ActualHeight - 200);
                attempts++;

                if (attempts > 100)
                {
                    x = random.Next(50, (int)this.ActualWidth - 100);
                    y = random.Next(50, (int)this.ActualHeight - 200);
                    break;
                }

            } while (!IsResourcePositionValid(x, y, 80));

            double distanceToPlayer = Math.Sqrt(Math.Pow(x - player.X, 2) + Math.Pow(y - player.Y, 2));
            if (distanceToPlayer < 100)
            {
                x = (x + 200) % (this.ActualWidth - 150);
                y = (y + 200) % (this.ActualHeight - 250);
            }

            ResourceType type = (ResourceType)random.Next(4);
            Resource resource = new Resource(x, y, type);
            resource.AddToCanvas(GameCanvas);
            resources.Add(resource);
        }

        private bool IsResourcePositionValid(double x, double y, double minDistance = 80)
        {
            // Проверяем расстояние до других ресурсов
            foreach (var resource in resources)
            {
                double distance = Math.Sqrt(Math.Pow(resource.X - x, 2) + Math.Pow(resource.Y - y, 2));
                if (distance < minDistance)
                    return false;
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
                    else if (isConnectingMode)
                    {
                        CancelConnectionMode();
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
                    if (!isBuildingMode && !isBuildingLine && !isConnectingMode)
                    {
                        OpenBuildMenu();
                    }
                    else
                    {
                        CancelBuildingMode();
                        CancelLineMode();
                        CancelConnectionMode();
                    }
                    e.Handled = true;
                    break;
                case Key.L:
                    if (!isBuildingMode && !isConnectingMode)
                    {
                        StartLineMode();
                    }
                    break;
                case Key.C:
                    if (isBuildingMode)
                    {
                        CancelBuildingMode();
                    }

                    if (isConnectingMode)
                    {
                        CancelConnectionMode();
                    }
                    else
                    {
                        StartConnectionMode();
                    }
                    break;
                case Key.T:
                    CreateTestSetup();
                    break;
                case Key.G:
                    ToggleGrid();
                    break;
                case Key.E:
                    // Экстренное удаление - для отладки
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        EmergencyCleanup();
                    }
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
            BuildHintText.Text = buildingType switch
            {
                "smelter" => "Кликните на место для постройки плавильни (10 камня + 5 угля)",
                "miner" => "Кликните НА РЕСУРС для постройки добытчика (5 жел.слитков + 5 мед.слитков)",
                "conveyor" => "Кликните для постройки конвейера (2 железных слитка)",
                "arms_factory" => "Кликните на место для постройки оружейного завода (15 камня + 10 жел.слитков + 10 мед.слитков)",
                _ => "Кликните на место для постройки"
            };
        }

        private BitmapImage LoadBuildingPreview(string buildingType)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string fileName = buildingType switch
            {
                "smelter" => "Smelter.png",
                "miner" => "Mining.png",
                "conveyor" => "conveyor\\down_1.png",
                "arms_factory" => "arms_factory.png",
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
                "arms_factory" => "AF",
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
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, width, height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, width, height));

                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    width < 50 ? 14 : 20,
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
        }

        private void CancelLineMode()
        {
            isBuildingLine = false;
            isLineFirstClick = true;

            // Удаляем превью конвейеров
            foreach (var conveyor in linePreviewConveyors)
            {
                conveyor.RemoveFromCanvas(GameCanvas);
            }
            linePreviewConveyors.Clear();

            BuildHint.Visibility = Visibility.Collapsed;
        }

        private void CancelConnectionMode()
        {
            isConnectingMode = false;
            connectionSource = null;
            connectionTarget = null;
            BuildHint.Visibility = Visibility.Collapsed;
        }

        private bool HasBuildingResources(string buildingType)
        {
            if (buildingType == "smelter")
            {
                return player.CanBuildSmelter();
            }
            else if (buildingType == "miner")
            {
                return player.CanBuildMiner();
            }
            else if (buildingType == "conveyor")
            {
                return player.HasResources(ResourceType.IronIngot, 2);
            }
            else if (buildingType == "arms_factory")
            {
                return player.CanBuildArmsFactory();
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

                // Правильное центрирование: отнимаем половину размера здания
                double buildingX = snappedPosition.X - offset.X;
                double buildingY = snappedPosition.Y - offset.Y;

                // Убедимся, что позиция соответствует сетке
                buildingX = Math.Floor(buildingX / GridSize) * GridSize;
                buildingY = Math.Floor(buildingY / GridSize) * GridSize;

                Canvas.SetLeft(buildingPreview, buildingX);
                Canvas.SetTop(buildingPreview, buildingY);

                // Проверяем валидность позиции
                bool isValidPosition = IsBuildingPlacementValid(buildingX, buildingY, buildingToPlace);

                // Для добытчика дополнительно проверяем, что он на ресурсе
                if (buildingToPlace == "miner" && isValidPosition)
                {
                    bool isOnResource = false;
                    Rect minerRect = new Rect(buildingX, buildingY, size.Width, size.Height);

                    foreach (var resource in resources)
                    {
                        Rect resourceRect = new Rect(resource.X, resource.Y, resource.Width, resource.Height);

                        // Проверяем, что добытчик полностью или частично находится на ресурсе
                        if (minerRect.IntersectsWith(resourceRect))
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

                    if (isValidPosition)
                    {
                        BuildHintText.Text = buildingToPlace switch
                        {
                            "smelter" => "Кликните для постройки плавильни (10 камня + 5 угля)",
                            "conveyor" => "Кликните для постройки конвейера (2 железных слитка)",
                            "arms_factory" => "Кликните для постройки оружейного завода (15 камня + 10 жел.слитков + 10 мед.слитков)",
                            _ => "Кликните для постройки"
                        };
                    }
                    else
                    {
                        BuildHintText.Text = "Нельзя построить здесь (занято, слишком далеко или вне границ)";
                    }
                }
            }
            else if (isBuildingLine)
            {
                var position = e.GetPosition(GameCanvas);
                var snappedPosition = SnapToGrid(position);

                // Обновляем превью линии
                UpdateLinePreview(snappedPosition);
            }
            else if (isConnectingMode)
            {
                var position = e.GetPosition(GameCanvas);

                // Проверяем, находится ли курсор над зданием
                var buildingAtCursor = FindBuildingAtPoint(position);

                if (buildingAtCursor != null)
                {
                    string buildingName = GetBuildingName(buildingAtCursor);
                    BuildHintText.Text = $"Наведено на: {buildingName}\n" +
                                       (connectionSource == null ?
                                        "Кликните, чтобы выбрать как ИСТОЧНИК" :
                                        "Кликните, чтобы выбрать как ЦЕЛЬ");
                }
                else
                {
                    BuildHintText.Text = "СОЕДИНЕНИЕ КОНВЕЙЕРОВ:\n" +
                                        "1. Выберите ИСТОЧНИК (майнер/плавильня)\n" +
                                        "2. Выберите ЦЕЛЬ (плавильня/оружейный завод)";
                }
            }
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

            if (!hasItems)
            {
                info += "Пусто";
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
            StartLineMode();
        }

        private void BuildArmsFactoryButton_Click(object sender, RoutedEventArgs e)
        {
            StartBuildingMode("arms_factory");
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

            // Обработка правого клика по зданиям для открытия интерфейса
            if (e.RightButton == MouseButtonState.Pressed)
            {
                HandleRightClick(clickPoint);
                return;
            }

            // Обработка левого клика для строительства
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (isBuildingLine)
                {
                    HandleLineModeClick(clickPoint);
                    return;
                }

                if (isConnectingMode)
                {
                    HandleConnectionModeClick(clickPoint);
                    return;
                }

                if (isBuildingMode)
                {
                    HandleBuildingModeClick(clickPoint);
                    return;
                }
            }
        }

        private void HandleRightClick(Point clickPoint)
        {
            // Проверяем плавильни
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt && smelter.IsPointInside(clickPoint))
                {
                    smelter.OpenInterface();
                    return;
                }
            }

            // Проверяем добытчики
            foreach (var miner in miners)
            {
                if (miner.IsBuilt && miner.IsPointInside(clickPoint))
                {
                    miner.OpenInterface();
                    return;
                }
            }

            // Проверяем оружейные заводы
            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt && armsFactory.IsPointInside(clickPoint))
                {
                    armsFactory.OpenInterface();
                    return;
                }
            }
        }

        private void HandleBuildingModeClick(Point clickPoint)
        {
            var offset = GetBuildingCenterOffset(buildingToPlace);
            double buildingX = clickPoint.X - offset.X;
            double buildingY = clickPoint.Y - offset.Y;

            // Привязываем к сетке (как в превью)
            buildingX = Math.Floor(buildingX / GridSize) * GridSize;
            buildingY = Math.Floor(buildingY / GridSize) * GridSize;

            if (IsBuildingPlacementValid(buildingX, buildingY, buildingToPlace))
            {
                if (buildingToPlace == "smelter")
                {
                    BuildSmelter(buildingX, buildingY);
                }
                else if (buildingToPlace == "miner")
                {
                    BuildMiner(buildingX, buildingY);
                }
                else if (buildingToPlace == "conveyor")
                {
                    // Показываем меню выбора направления
                    ShowDirectionSelectionMenu(buildingX, buildingY);
                }
                else if (buildingToPlace == "arms_factory")
                {
                    BuildArmsFactory(buildingX, buildingY);
                }
            }
            else
            {
                ShowMessage("Нельзя построить здесь!");
            }
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

            // Обновляем список плавилен у игрока
            player.SetSmelters(smelters);

            ShowMessage("Плавильня построена!");
            CancelBuildingMode();
        }

        private void BuildMiner(double x, double y)
        {
            // Находим ресурс под курсором
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

                // Автоматически соединяем с соседними конвейерами
                AutoConnectConveyor(conveyor);

                ShowMessage("Конвейер построен!");
                CancelBuildingMode();
            }
            else
            {
                ShowMessage("Нельзя построить конвейер здесь!");
                // Возвращаем ресурсы
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

        private void AutoConnectConveyor(Conveyor newConveyor)
        {
            List<Conveyor> adjacent = newConveyor.GetAdjacentConveyors(conveyors);

            foreach (var adjacentConveyor in adjacent)
            {
                // Проверяем расстояние - должны быть точно рядом
                double distanceX = Math.Abs(newConveyor.X - adjacentConveyor.X);
                double distanceY = Math.Abs(newConveyor.Y - adjacentConveyor.Y);

                // Должны быть в соседних клетках (расстояние равно размеру клетки)
                if ((distanceX == GridSize && distanceY == 0) || // горизонтальные соседи
                    (distanceY == GridSize && distanceX == 0))   // вертикальные соседи
                {
                    // Проверяем, смотрит ли новый конвейер на соседний
                    if (newConveyor.IsNextInDirection(adjacentConveyor))
                    {
                        if (newConveyor.NextConveyor == null && adjacentConveyor.PreviousConveyor == null)
                        {
                            newConveyor.SetNextConveyor(adjacentConveyor);
                        }
                    }
                    // Проверяем, смотрит ли соседний конвейер на новый
                    else if (adjacentConveyor.IsNextInDirection(newConveyor))
                    {
                        if (adjacentConveyor.NextConveyor == null && newConveyor.PreviousConveyor == null)
                        {
                            adjacentConveyor.SetNextConveyor(newConveyor);
                        }
                    }
                }
            }
        }

        // РЕЖИМ ПОСТРОЕНИЯ ЛИНИИ КОНВЕЙЕРОВ
        private void StartLineMode()
        {
            if (!player.HasResources(ResourceType.IronIngot, 2))
            {
                ShowMessage("Недостаточно железных слитков для постройки конвейеров!");
                return;
            }

            isBuildingLine = true;
            isLineFirstClick = true;
            linePreviewConveyors.Clear();

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "ПОСТРОЕНИЕ ЛИНИИ КОНВЕЙЕРОВ:\n" +
                                "1. Кликните на начальную клетку\n" +
                                "2. Кликните на конечную клетку\n" +
                                "Система автоматически построит линию между точками.";
        }

        private void UpdateLinePreview(Point currentPoint)
        {
            if (!isBuildingLine || isLineFirstClick) return;

            // Очищаем превью
            foreach (var conveyor in linePreviewConveyors)
            {
                conveyor.RemoveFromCanvas(GameCanvas);
            }
            linePreviewConveyors.Clear();

            // Вычисляем путь между точками
            List<Point> pathPoints = CalculateLinePath(lineStartPoint, currentPoint);

            // Создаем превью конвейеров
            foreach (var point in pathPoints)
            {
                Direction direction = CalculateDirectionBetweenPoints(lineStartPoint, point);
                Conveyor previewConveyor = new Conveyor(point.X, point.Y, direction)
                {
                    Sprite = { Opacity = 0.5 }
                };
                previewConveyor.AddToCanvas(GameCanvas);
                linePreviewConveyors.Add(previewConveyor);
            }
        }

        private List<Point> CalculateLinePath(Point start, Point end)
        {
            List<Point> path = new List<Point>();

            // Если точки совпадают
            if (Math.Abs(start.X - end.X) < 5 && Math.Abs(start.Y - end.Y) < 5)
            {
                path.Add(start);
                return path;
            }

            // Горизонтальная линия
            if (Math.Abs(start.Y - end.Y) < 5)
            {
                int step = start.X < end.X ? GridSize : -GridSize;
                for (double x = start.X; Math.Abs(x - end.X) > GridSize / 2; x += step)
                {
                    path.Add(new Point(x, start.Y));
                }
                path.Add(end);
            }
            // Вертикальная линия
            else if (Math.Abs(start.X - end.X) < 5)
            {
                int step = start.Y < end.Y ? GridSize : -GridSize;
                for (double y = start.Y; Math.Abs(y - end.Y) > GridSize / 2; y += step)
                {
                    path.Add(new Point(start.X, y));
                }
                path.Add(end);
            }
            else
            {
                // L-образный путь: сначала горизонталь, потом вертикаль
                int stepX = start.X < end.X ? GridSize : -GridSize;
                int stepY = start.Y < end.Y ? GridSize : -GridSize;

                // Горизонтальная часть
                for (double x = start.X; Math.Abs(x - end.X) > GridSize / 2; x += stepX)
                {
                    path.Add(new Point(x, start.Y));
                }

                // Вертикальная часть (начинаем от последней точки + шаг)
                Point lastPoint = path.Last();
                for (double y = lastPoint.Y + stepY; Math.Abs(y - end.Y) > GridSize / 2; y += stepY)
                {
                    path.Add(new Point(end.X, y));
                }
                path.Add(end);
            }

            return path.Distinct().ToList(); // Удаляем дубликаты
        }

        private Direction CalculateDirectionBetweenPoints(Point from, Point to)
        {
            if (Math.Abs(to.Y - from.Y) < GridSize) // Горизонтальное движение
            {
                return to.X > from.X ? Direction.Right : Direction.Left;
            }
            else // Вертикальное движение
            {
                return to.Y > from.Y ? Direction.Down : Direction.Up;
            }
        }

        private void HandleLineModeClick(Point clickPoint)
        {
            if (isLineFirstClick)
            {
                // Первый клик - запоминаем начальную точку
                lineStartPoint = clickPoint;
                isLineFirstClick = false;
                BuildHintText.Text = "Начальная точка выбрана. Кликните на конечную точку.";
            }
            else
            {
                // Второй клик - строим линию
                BuildLineBetweenPoints(lineStartPoint, clickPoint);
                CancelLineMode();
            }
        }

        private void BuildLineBetweenPoints(Point start, Point end)
        {
            // Вычисляем путь
            List<Point> pathPoints = CalculateLinePath(start, end);

            if (pathPoints.Count == 0)
            {
                ShowMessage("Линия слишком короткая!");
                return;
            }

            // Проверяем достаточно ли ресурсов
            int requiredIron = pathPoints.Count * 2;
            if (!player.HasResources(ResourceType.IronIngot, requiredIron))
            {
                ShowMessage($"Недостаточно железных слитков! Нужно: {requiredIron}");
                return;
            }

            List<Conveyor> newConveyors = new List<Conveyor>();

            // Строим конвейеры
            for (int i = 0; i < pathPoints.Count; i++)
            {
                Point point = pathPoints[i];

                // Определяем направление
                Direction direction;
                if (i < pathPoints.Count - 1)
                {
                    direction = CalculateDirectionBetweenPoints(point, pathPoints[i + 1]);
                }
                else if (i > 0)
                {
                    direction = CalculateDirectionBetweenPoints(pathPoints[i - 1], point);
                }
                else
                {
                    direction = Direction.Right; // По умолчанию
                }

                // Проверяем, можно ли построить здесь
                if (!IsBuildingPlacementValid(point.X, point.Y, "conveyor", false))
                {
                    ShowMessage($"Нельзя построить конвейер на позиции ({point.X}, {point.Y})!");
                    // Удаляем уже построенные
                    foreach (var conv in newConveyors)
                    {
                        conv.RemoveFromCanvas(GameCanvas);
                        conveyors.Remove(conv);
                    }
                    return;
                }

                // Платим ресурсы
                if (!player.RemoveResources(ResourceType.IronIngot, 2))
                {
                    ShowMessage("Ошибка при оплате ресурсов!");
                    return;
                }

                // Создаем конвейер
                Conveyor newConveyor = new Conveyor(point.X, point.Y, direction); // Изменено имя переменной
                newConveyor.Build();
                newConveyor.AddToCanvas(GameCanvas);
                conveyors.Add(newConveyor);
                newConveyors.Add(newConveyor);
            }

            // Соединяем конвейеры в цепочку
            for (int i = 0; i < newConveyors.Count - 1; i++)
            {
                newConveyors[i].SetNextConveyor(newConveyors[i + 1]);
            }

            // Автоматически соединяем каждый конвейер с соседями
            foreach (var conv in newConveyors)
            {
                AutoConnectConveyor(conv);
            }

            ShowMessage($"Построена линия из {newConveyors.Count} конвейеров!");
        }

        // РЕЖИМ СОЕДИНЕНИЯ КОНВЕЙЕРОВ С ЗДАНИЯМИ
        private void StartConnectionMode()
        {
            if (conveyors.Count == 0)
            {
                ShowMessage("Нет конвейеров для соединения!");
                return;
            }

            if (miners.Count == 0 && smelters.Count == 0 && armsFactories.Count == 0)
            {
                ShowMessage("Нет зданий для соединения!");
                return;
            }

            isConnectingMode = true;
            connectionSource = null;
            connectionTarget = null;

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "СОЕДИНЕНИЕ КОНВЕЙЕРОВ:\n" +
                                "1. Выберите ИСТОЧНИК (майнер/плавильня)\n" +
                                "2. Выберите ЦЕЛЬ (плавильня/оружейный завод)\n" +
                                "Система найдет ближайший конвейер к каждому зданию.";
        }

        private void HandleConnectionModeClick(Point clickPoint)
        {
            if (!isConnectingMode) return;

            // Ищем здание под кликом
            object clickedBuilding = FindBuildingAtPoint(clickPoint);

            if (clickedBuilding == null)
            {
                ShowMessage("Кликните на здание (майнер, плавильню или оружейный завод)!");
                return;
            }

            if (connectionSource == null)
            {
                // Выбор источника
                if (clickedBuilding is Miner || clickedBuilding is Smelter || clickedBuilding is ArmsFactory)
                {
                    connectionSource = clickedBuilding;
                    BuildHintText.Text = $"Источник выбран: {GetBuildingName(clickedBuilding)}\nТеперь выберите ЦЕЛЬ.";
                }
                else
                {
                    ShowMessage("Источником может быть майнер, плавильня или оружейный завод!");
                }
            }
            else if (connectionTarget == null)
            {
                // Выбор цели
                if (clickedBuilding != connectionSource && (clickedBuilding is Smelter || clickedBuilding is ArmsFactory))
                {
                    connectionTarget = clickedBuilding;
                    BuildHintText.Text = $"Цель выбрана: {GetBuildingName(clickedBuilding)}\nСоединяем...";

                    // Соединяем здания через конвейеры
                    ConnectBuildings(connectionSource, connectionTarget);

                    // Сбрасываем режим
                    CancelConnectionMode();
                }
                else
                {
                    ShowMessage("Целью может быть плавильня или оружейный завод (и не то же самое, что источник)!");
                }
            }
        }

        private object FindBuildingAtPoint(Point point)
        {
            // Проверяем майнеры
            foreach (var miner in miners)
            {
                if (miner.IsBuilt && miner.IsPointInside(point))
                    return miner;
            }

            // Проверяем плавильни
            foreach (var smelter in smelters)
            {
                if (smelter.IsBuilt && smelter.IsPointInside(point))
                    return smelter;
            }

            // Проверяем оружейные заводы
            foreach (var armsFactory in armsFactories)
            {
                if (armsFactory.IsBuilt && armsFactory.IsPointInside(point))
                    return armsFactory;
            }

            return null;
        }

        private string GetBuildingName(object building)
        {
            if (building is Miner) return "Добытчик";
            if (building is Smelter) return "Плавильня";
            if (building is ArmsFactory) return "Оружейный завод";
            return "Неизвестное здание";
        }

        private void ConnectBuildings(object source, object target)
        {
            // Находим ближайший конвейер к источнику
            Conveyor sourceConveyor = FindNearestConveyorToBuilding(source);
            // Находим ближайший конвейер к цели
            Conveyor targetConveyor = FindNearestConveyorToBuilding(target);

            // Если нет конвейеров рядом, строим их
            if (sourceConveyor == null || targetConveyor == null)
            {
                // Попробуем построить конвейеры между зданиями
                if (TryBuildConveyorsBetweenBuildings(source, target))
                {
                    ShowMessage("Построены конвейеры и соединены!");
                }
                else
                {
                    ShowMessage("Не удалось построить соединение! Убедитесь, что есть конвейеры рядом с обоими зданиями.");
                }
                return;
            }

            // Находим путь между конвейерами
            List<Conveyor> path = FindPathBetweenConveyors(sourceConveyor, targetConveyor);

            if (path.Count == 0)
            {
                // Если не нашли путь, попробуем построить прямую линию
                if (TryBuildDirectLine(sourceConveyor, targetConveyor, out path))
                {
                    ShowMessage($"Построена прямая линия через {path.Count} конвейеров!");
                }
                else
                {
                    ShowMessage("Не удалось найти путь между конвейерами!");
                    return;
                }
            }

            // Устанавливаем источник и цель для цепочки
            if (path.Count > 0)
            {
                // Первый конвейер в цепочке получает источник
                path[0].SourceBuilding = source;
                // Последний конвейер в цепочке получает цель
                path[path.Count - 1].TargetBuilding = target;
            }

            // Соединяем конвейеры в цепочку
            for (int i = 0; i < path.Count - 1; i++)
            {
                path[i].SetNextConveyor(path[i + 1]);
            }

            ShowMessage($"Соединено! Путь через {path.Count} конвейеров.");
        }

        private bool TryBuildConveyorsBetweenBuildings(object source, object target)
        {
            Point sourcePoint = GetBuildingCenter(source);
            Point targetPoint = GetBuildingCenter(target);

            // Вычисляем расстояние между зданиями
            double distance = Math.Sqrt(Math.Pow(targetPoint.X - sourcePoint.X, 2) +
                                        Math.Pow(targetPoint.Y - sourcePoint.Y, 2));

            // Если слишком далеко, не строим
            if (distance > 500) return false;

            // Строим линию конвейеров между точками
            List<Point> pathPoints = CalculateLinePath(SnapToGrid(sourcePoint), SnapToGrid(targetPoint));

            if (pathPoints.Count == 0) return false;

            List<Conveyor> newConveyors = new List<Conveyor>();

            // Строим конвейеры
            for (int i = 0; i < pathPoints.Count; i++)
            {
                Point point = pathPoints[i];

                // Определяем направление
                Direction direction;
                if (i < pathPoints.Count - 1)
                {
                    direction = CalculateDirectionBetweenPoints(point, pathPoints[i + 1]);
                }
                else if (i > 0)
                {
                    direction = CalculateDirectionBetweenPoints(pathPoints[i - 1], point);
                }
                else
                {
                    direction = Direction.Right; // По умолчанию
                }

                // Проверяем, можно ли построить здесь
                if (!IsBuildingPlacementValid(point.X, point.Y, "conveyor", false))
                    continue; // Пропускаем эту клетку

                // Создаем конвейер
                Conveyor newConveyor = new Conveyor(point.X, point.Y, direction);
                newConveyor.Build();
                newConveyor.AddToCanvas(GameCanvas);
                conveyors.Add(newConveyor);
                newConveyors.Add(newConveyor);
            }

            if (newConveyors.Count == 0) return false;

            // Соединяем конвейеры в цепочку
            for (int i = 0; i < newConveyors.Count - 1; i++)
            {
                newConveyors[i].SetNextConveyor(newConveyors[i + 1]);
            }

            // Устанавливаем источник и цель
            newConveyors[0].SourceBuilding = source;
            newConveyors[newConveyors.Count - 1].TargetBuilding = target;

            return true;
        }

        private bool TryBuildDirectLine(Conveyor start, Conveyor end, out List<Conveyor> path)
        {
            path = new List<Conveyor>();

            // Простой алгоритм: идем по прямой
            double currentX = start.X;
            double currentY = start.Y;
            double targetX = end.X;
            double targetY = end.Y;

            // Находим все конвейеры по пути
            while (Math.Abs(currentX - targetX) > GridSize || Math.Abs(currentY - targetY) > GridSize)
            {
                // Находим конвейер в текущей позиции
                Conveyor conveyorAtPos = FindConveyorAtPosition(currentX, currentY);
                if (conveyorAtPos != null && !path.Contains(conveyorAtPos))
                {
                    path.Add(conveyorAtPos);
                }

                // Двигаемся к цели
                if (Math.Abs(currentX - targetX) > GridSize)
                {
                    currentX += (targetX > currentX) ? GridSize : -GridSize;
                }
                else if (Math.Abs(currentY - targetY) > GridSize)
                {
                    currentY += (targetY > currentY) ? GridSize : -GridSize;
                }
            }

            // Добавляем конечный конвейер
            if (!path.Contains(end))
            {
                path.Add(end);
            }

            return path.Count > 0;
        }

        private Conveyor FindConveyorAtPosition(double x, double y)
        {
            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt &&
                    Math.Abs(conveyor.X - x) < 5 &&
                    Math.Abs(conveyor.Y - y) < 5)
                {
                    return conveyor;
                }
            }
            return null;
        }

        private Conveyor FindNearestConveyorToBuilding(object building)
        {
            Point buildingCenter = GetBuildingCenter(building);
            Conveyor nearest = null;
            double minDistance = double.MaxValue;

            foreach (var conveyor in conveyors)
            {
                if (conveyor.IsBuilt)
                {
                    Point conveyorCenter = new Point(conveyor.X + conveyor.Width / 2,
                                                   conveyor.Y + conveyor.Height / 2);
                    double distance = Math.Sqrt(
                        Math.Pow(conveyorCenter.X - buildingCenter.X, 2) +
                        Math.Pow(conveyorCenter.Y - buildingCenter.Y, 2));

                    // Увеличиваем максимальное расстояние до 250 пикселей
                    if (distance < minDistance && distance < 250)
                    {
                        minDistance = distance;
                        nearest = conveyor;
                    }
                }
            }

            // Если не нашли, ищем самый ближайший независимо от расстояния
            if (nearest == null && conveyors.Count > 0)
            {
                minDistance = double.MaxValue;
                foreach (var conveyor in conveyors)
                {
                    if (conveyor.IsBuilt)
                    {
                        Point conveyorCenter = new Point(conveyor.X + conveyor.Width / 2,
                                                       conveyor.Y + conveyor.Height / 2);
                        double distance = Math.Sqrt(
                            Math.Pow(conveyorCenter.X - buildingCenter.X, 2) +
                            Math.Pow(conveyorCenter.Y - buildingCenter.Y, 2));

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearest = conveyor;
                        }
                    }
                }
            }

            return nearest;
        }

        private Point GetBuildingCenter(object building)
        {
            if (building is Miner miner)
            {
                return new Point(miner.X + miner.Width / 2, miner.Y + miner.Height / 2);
            }
            else if (building is Smelter smelter)
            {
                return new Point(smelter.X + smelter.Width / 2, smelter.Y + smelter.Height / 2);
            }
            else if (building is ArmsFactory armsFactory)
            {
                return new Point(armsFactory.X + armsFactory.Width / 2, armsFactory.Y + armsFactory.Height / 2);
            }
            return new Point(0, 0);
        }

        private List<Conveyor> FindPathBetweenConveyors(Conveyor start, Conveyor end)
        {
            // Простая реализация: используем поиск в ширину
            var visited = new HashSet<Conveyor>();
            var queue = new Queue<List<Conveyor>>();

            queue.Enqueue(new List<Conveyor> { start });
            visited.Add(start);

            int maxSteps = 50; // Ограничиваем количество шагов

            while (queue.Count > 0 && maxSteps-- > 0)
            {
                var currentPath = queue.Dequeue();
                var current = currentPath.Last();

                if (current == end)
                {
                    return currentPath;
                }

                // Получаем соседей (включая следующий и предыдущий)
                var neighbors = new List<Conveyor>();

                if (current.NextConveyor != null)
                    neighbors.Add(current.NextConveyor);
                if (current.PreviousConveyor != null)
                    neighbors.Add(current.PreviousConveyor);

                // Также ищем конвейеры рядом по позиции
                foreach (var conveyor in conveyors)
                {
                    if (conveyor != current && conveyor.IsBuilt && !visited.Contains(conveyor))
                    {
                        double distance = Math.Sqrt(
                            Math.Pow(conveyor.X - current.X, 2) +
                            Math.Pow(conveyor.Y - current.Y, 2));

                        // Если конвейер рядом (в пределах 60 пикселей)
                        if (distance < 60)
                        {
                            neighbors.Add(conveyor);
                        }
                    }
                }

                foreach (var neighbor in neighbors.Distinct())
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        var newPath = new List<Conveyor>(currentPath) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return new List<Conveyor>(); // Путь не найден
        }

        private bool AreConveyorsConnected(Conveyor start, Conveyor end, out List<Conveyor> path)
        {
            path = new List<Conveyor>();
            var current = start;

            while (current != null)
            {
                path.Add(current);
                if (current == end) return true;
                current = current.NextConveyor;
            }

            return false;
        }

        private List<Conveyor> GetConnectedNeighbors(Conveyor conveyor)
        {
            List<Conveyor> neighbors = new List<Conveyor>();

            if (conveyor.NextConveyor != null)
                neighbors.Add(conveyor.NextConveyor);
            if (conveyor.PreviousConveyor != null)
                neighbors.Add(conveyor.PreviousConveyor);

            return neighbors;
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
            player.AddResource(ResourceType.Stone, 15);
            player.AddResource(ResourceType.Iron, 10);
            player.AddResource(ResourceType.Copper, 10);

            ShowMessage("Добавлено в инвентарь: 10 железа, 10 меди, 11 медных слитков, 11 железных слитков, 27 угля, 15 камня");
        }

        private void EmergencyCleanup()
        {
            // Удаляем все конвейеры (для отладки)
            foreach (var conveyor in conveyors.ToList())
            {
                conveyor.RemoveFromCanvas(GameCanvas);
            }
            conveyors.Clear();
            ShowMessage("Все конвейеры удалены!");
        }

        private void ToggleGridButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleGrid();
        }
    }
}