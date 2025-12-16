using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;

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
        private Random random = new Random();
        private bool isUpPressed = false;
        private bool isDownPressed = false;
        private bool isLeftPressed = false;
        private bool isRightPressed = false;
        private bool isMiningPressed = false;

        public MainWindow()
        {
            InitializeComponent();
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

        private void InitializePlayer()
        {
            double startX = this.ActualWidth / 2 - 25;
            double startY = this.ActualHeight / 2 - 25;

            player = new Player(startX, startY, 50, 50);
            player.AddToCanvas(GameCanvas);
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
            //RemoveDepletedResources();
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
            int resourceCount = 15;
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

            // Генерируем случайный тип ресурса (железо, медь или уголь)
            ResourceType type = (ResourceType)random.Next(3); // 0=Iron, 1=Copper, 2=Coal
            int amount = random.Next(5, 15);

            Resource resource = new Resource(x, y, type, amount);
            resource.AddToCanvas(GameCanvas);
            resources.Add(resource);
        }

        private void RemoveDepletedResources()
        {
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                if (resources[i].Amount <= 0)
                {
                    resources[i].RemoveFromCanvas(GameCanvas);
                    resources.RemoveAt(i);

                    // С некоторой вероятностью спавним новый ресурс
                    if (random.Next(3) == 0)
                    {
                        SpawnRandomResource();
                    }
                }
            }
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
                    this.Close();
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
    }
}