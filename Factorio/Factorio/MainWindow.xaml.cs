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
        private List<Smelter> smelters = new List<Smelter>(); // Добавлено
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

            // Обновляем все плавильни
            foreach (var smelter in smelters)
            {
                // Прогресс переплавки обновляется через таймер в классе Smelter
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
            int resourceCount = 20;
            for (int i = 0; i < resourceCount; i++)
            {
                SpawnRandomResource();
            }
        }

        private void SpawnRandomResource()
        {
            double x = random.Next(50, (int)this.ActualWidth - 50);
            double y = random.Next(50, (int)this.ActualHeight - 150);

            double distanceToPlayer = Math.Sqrt(Math.Pow(x - player.X, 2) + Math.Pow(y - player.Y, 2));
            if (distanceToPlayer < 100)
            {
                x = (x + 150) % (this.ActualWidth - 100);
                y = (y + 150) % (this.ActualHeight - 200);
            }

            ResourceType type = (ResourceType)random.Next(4);
            int amount = type switch
            {
                ResourceType.Coal => random.Next(10, 20),
                ResourceType.Stone => random.Next(8, 15),
                _ => random.Next(5, 12)
            };

            Resource resource = new Resource(x, y, type, amount);
            resource.AddToCanvas(GameCanvas);
            resources.Add(resource);
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
                    if (!isBuildingMode)
                    {
                        OpenBuildMenu();
                    }
                    else
                    {
                        CancelBuildingMode();
                    }
                    e.Handled = true; // Предотвращаем стандартную обработку Tab
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
                Width = 60,
                Height = 60,
                Opacity = 0.7,
                Source = LoadBuildingPreview(buildingType)
            };
            GameCanvas.Children.Add(buildingPreview);
            Canvas.SetZIndex(buildingPreview, 99);

            BuildHint.Visibility = Visibility.Visible;
            BuildHintText.Text = "Кликните на место для постройки плавильни (не дальше 100px от игрока)";
        }

        private BitmapImage LoadBuildingPreview(string buildingType)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string filePath = Path.Combine(basePath, "Smelter.png");

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            // Заглушка
            return CreatePlaceholderBuildingPreview();
        }

        private BitmapImage CreatePlaceholderBuildingPreview()
        {
            var renderTarget = new RenderTargetBitmap(60, 60, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkGray, null, new Rect(0, 0, 60, 60));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, 60, 60));

                var formattedText = new FormattedText(
                    "SM",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(15, 15));
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

        private bool HasBuildingResources()
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

        private bool RemoveBuildingResources()
        {
            int stoneNeeded = 10;
            int coalNeeded = 5;

            // Удаляем камень
            for (int i = 0; i < player.Inventory.Length && stoneNeeded > 0; i++)
            {
                if (player.Inventory[i].Type == ResourceType.Stone)
                {
                    int removeAmount = Math.Min(player.Inventory[i].Count, stoneNeeded);
                    player.Inventory[i].Count -= removeAmount;
                    stoneNeeded -= removeAmount;

                    if (player.Inventory[i].Count <= 0)
                    {
                        player.Inventory[i].Type = ResourceType.None;
                        player.Inventory[i].Count = 0;
                    }

                    player.UpdateInventorySlot(i);
                }
            }

            // Удаляем уголь
            for (int i = 0; i < player.Inventory.Length && coalNeeded > 0; i++)
            {
                if (player.Inventory[i].Type == ResourceType.Coal)
                {
                    int removeAmount = Math.Min(player.Inventory[i].Count, coalNeeded);
                    player.Inventory[i].Count -= removeAmount;
                    coalNeeded -= removeAmount;

                    if (player.Inventory[i].Count <= 0)
                    {
                        player.Inventory[i].Type = ResourceType.None;
                        player.Inventory[i].Count = 0;
                    }

                    player.UpdateInventorySlot(i);
                }
            }

            return stoneNeeded == 0 && coalNeeded == 0;
        }

        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isBuildingMode && buildingPreview != null)
            {
                var position = e.GetPosition(GameCanvas);
                Canvas.SetLeft(buildingPreview, position.X - 30);
                Canvas.SetTop(buildingPreview, position.Y - 30);

                // Проверяем расстояние до игрока
                double distance = Math.Sqrt(
                    Math.Pow(position.X - (player.X + player.Width / 2), 2) +
                    Math.Pow(position.Y - (player.Y + player.Height / 2), 2));

                if (distance <= 100)
                {
                    buildingPreview.Opacity = 0.7;
                    BuildHintText.Text = "Кликните для постройки";
                }
                else
                {
                    buildingPreview.Opacity = 0.3;
                    BuildHintText.Text = "Слишком далеко от игрока (макс. 100px)";
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

            // Передаем список плавилен игроку для проверки коллизий
            player.SetSmelters(smelters);
        }

        // При постройке новой плавильни обновляем ссылку у игрока
        private void GameCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isBuildingMode && e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(GameCanvas);

                double distance = Math.Sqrt(
                    Math.Pow(position.X - (player.X + player.Width / 2), 2) +
                    Math.Pow(position.Y - (player.Y + player.Height / 2), 2));

                if (distance <= 100)
                {
                    if (HasBuildingResources())
                    {
                        Smelter smelter = new Smelter(position.X - 30, position.Y - 30, player);

                        if (RemoveBuildingResources())
                        {
                            smelter.Build();
                            smelter.AddToCanvas(GameCanvas);
                            smelters.Add(smelter);

                            // Обновляем список плавилен у игрока
                            player.SetSmelters(smelters);

                            ShowMessage("Плавильня построена!");
                            CancelBuildingMode();
                        }
                    }
                }
            }
        }
    }
}