using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.IO;

namespace Factorio
{
    public class InventorySlot
    {
        public ResourceType Type { get; set; }
        public int Count { get; set; }
        public Image Icon { get; set; }
        public TextBlock CountText { get; set; }
    }

    public class Player
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Speed { get; set; }
        public Direction CurrentDirection { get; private set; }
        public InventorySlot[] Inventory { get; private set; }
        private StackPanel inventoryPanel;
        private Dictionary<Direction, List<BitmapImage>> animations;
        private int currentFrame = 0;
        private int animationSpeed = 150;
        private DateTime lastFrameTime;
        public bool IsMoving { get; private set; }
        private Resource currentMiningResource = null;
        private double miningProgress = 0;
        private const double miningTime = 1.0;
        private bool isMining = false;

        public Player(double startX, double startY, double width, double height)
        {
            X = startX;
            Y = startY;
            Width = width;
            Height = height;
            Speed = 3.0;
            CurrentDirection = Direction.Down;
            lastFrameTime = DateTime.Now;

            InitializeInventory();
            InitializeSprite();
            LoadAnimations();
        }

        private void InitializeInventory()
        {
            Inventory = new InventorySlot[5];
            for (int i = 0; i < 5; i++)
            {
                Inventory[i] = new InventorySlot
                {
                    Type = ResourceType.None,
                    Count = 0,
                    Icon = null,
                    CountText = null
                };
            }
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform
            };
            UpdatePosition();
        }

        private void LoadAnimations()
        {
            animations = new Dictionary<Direction, List<BitmapImage>>();
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Player\";

            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                animations[direction] = new List<BitmapImage>();
                LoadDirectionSprites(direction, basePath);
            }
        }

        private void LoadDirectionSprites(Direction direction, string basePath)
        {
            string directionName = direction.ToString();

            string standingPath = Path.Combine(basePath, $"{directionName}.png");
            if (File.Exists(standingPath))
            {
                animations[direction].Add(new BitmapImage(new Uri(standingPath)));
            }
            else
            {
                animations[direction].Add(CreatePlaceholderSprite(direction, "S"));
            }

            for (int i = 1; i <= 2; i++)
            {
                string movePath = Path.Combine(basePath, $"{directionName}_move({i}).png");
                if (File.Exists(movePath))
                {
                    animations[direction].Add(new BitmapImage(new Uri(movePath)));
                }
                else
                {
                    animations[direction].Add(CreatePlaceholderSprite(direction, $"M{i}"));
                }
            }
        }

        private BitmapImage CreatePlaceholderSprite(Direction direction, string label = "S")
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                Brush color = direction switch
                {
                    Direction.Up => Brushes.Blue,
                    Direction.Down => Brushes.Green,
                    Direction.Left => Brushes.Yellow,
                    Direction.Right => Brushes.Red,
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                string text = $"{direction.ToString()[0]}{label}";
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    14,
                    Brushes.Black,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(Width / 2 - 15, Height / 2 - 7));
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

        public void Move(double deltaX, double deltaY, Direction direction)
        {
            IsMoving = true;
            CurrentDirection = direction;

            X += deltaX * Speed;
            Y += deltaY * Speed;

            X = Math.Max(0, Math.Min(X, SystemParameters.PrimaryScreenWidth - Width));
            Y = Math.Max(0, Math.Min(Y, SystemParameters.PrimaryScreenHeight - Height));

            UpdatePosition();
        }

        public void Stop()
        {
            IsMoving = false;
            currentFrame = 0;
        }

        public void UpdateAnimation()
        {
            if (IsMoving && animations.ContainsKey(CurrentDirection) && animations[CurrentDirection].Count > 0)
            {
                if ((DateTime.Now - lastFrameTime).TotalMilliseconds >= animationSpeed)
                {
                    currentFrame = (currentFrame == 0 || currentFrame == 1) ? 2 : 1;
                    lastFrameTime = DateTime.Now;
                }
                Sprite.Source = animations[CurrentDirection][currentFrame];
            }
            else if (animations.ContainsKey(CurrentDirection) && animations[CurrentDirection].Count > 0)
            {
                currentFrame = 0;
                Sprite.Source = animations[CurrentDirection][0];
            }
        }

        public void UpdateMining(List<Resource> resources, bool isMiningButtonPressed)
        {
            if (!isMiningButtonPressed)
            {
                isMining = false;
                currentMiningResource = null;
                miningProgress = 0;
                return;
            }

            Resource nearestResource = FindNearestResourceInRange(resources, 50);

            if (nearestResource != null)
            {
                if (currentMiningResource != nearestResource)
                {
                    currentMiningResource = nearestResource;
                    miningProgress = 0;
                    isMining = true;
                }

                if (isMining)
                {
                    miningProgress += 0.016;

                    if (miningProgress >= miningTime)
                    {
                        if (AddToInventory(currentMiningResource.Type))
                        {
                            currentMiningResource.DecreaseAmount(1);
                            if (currentMiningResource.Amount <= 0)
                            {
                                currentMiningResource = null;
                            }
                        }
                        miningProgress = 0;
                    }
                }
            }
            else
            {
                isMining = false;
                currentMiningResource = null;
                miningProgress = 0;
            }
        }

        private Resource FindNearestResourceInRange(List<Resource> resources, double range)
        {
            Resource nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (var resource in resources)
            {
                if (resource.Amount > 0)
                {
                    double distance = Math.Sqrt(Math.Pow(resource.X - X, 2) + Math.Pow(resource.Y - Y, 2));
                    if (distance < range && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = resource;
                    }
                }
            }

            return nearest;
        }

        public double GetMiningProgress()
        {
            return miningProgress / miningTime;
        }

        private void UpdatePosition()
        {
            Canvas.SetLeft(Sprite, X);
            Canvas.SetTop(Sprite, Y);
        }

        public void AddToCanvas(Canvas canvas)
        {
            if (!canvas.Children.Contains(Sprite))
            {
                canvas.Children.Add(Sprite);
                Canvas.SetZIndex(Sprite, 100);
                UpdatePosition();
            }
        }

        public void RemoveFromCanvas(Canvas canvas)
        {
            if (canvas.Children.Contains(Sprite))
            {
                canvas.Children.Remove(Sprite);
            }
        }

        public bool AddToInventory(ResourceType type)
        {
            for (int i = 0; i < Inventory.Length; i++)
            {
                if (Inventory[i].Type == type && Inventory[i].Count < 99)
                {
                    Inventory[i].Count++;
                    UpdateInventorySlot(i);
                    return true;
                }
            }

            for (int i = 0; i < Inventory.Length; i++)
            {
                if (Inventory[i].Type == ResourceType.None)
                {
                    Inventory[i].Type = type;
                    Inventory[i].Count = 1;
                    UpdateInventorySlot(i);
                    return true;
                }
            }

            return false;
        }

        private void UpdateInventorySlot(int slotIndex)
        {
            if (inventoryPanel == null || slotIndex < 0 || slotIndex >= 5) return;

            var slotBorder = inventoryPanel.Children[slotIndex] as Border;
            if (slotBorder == null) return;

            var slot = Inventory[slotIndex];
            slotBorder.Child = null;

            if (slot.Type != ResourceType.None)
            {
                var stackPanel = new StackPanel();
                string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Resources\";

                // Определяем путь к файлу в зависимости от типа ресурса
                string filePath;
                switch (slot.Type)
                {
                    case ResourceType.Iron:
                        filePath = Path.Combine(basePath, "iron.png");
                        break;
                    case ResourceType.Copper:
                        filePath = Path.Combine(basePath, "copper.png");
                        break;
                    case ResourceType.Coal:
                        filePath = Path.Combine(basePath, "coal.png");
                        break;
                    default:
                        filePath = Path.Combine(basePath, "default.png");
                        break;
                }

                Image icon = new Image
                {
                    Width = 40,
                    Height = 40,
                    Stretch = Stretch.Uniform
                };

                if (File.Exists(filePath))
                {
                    icon.Source = new BitmapImage(new Uri(filePath));
                }
                else
                {
                    // Если файл не найден, создаем заглушку
                    icon.Source = CreatePlaceholderResourceIcon(slot.Type);
                }

                TextBlock countText = new TextBlock
                {
                    Text = slot.Count.ToString(),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };

                stackPanel.Children.Add(icon);
                stackPanel.Children.Add(countText);
                slotBorder.Child = stackPanel;
            }
        }

        private BitmapImage CreatePlaceholderResourceIcon(ResourceType type)
        {
            var renderTarget = new RenderTargetBitmap(50, 50, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Разные цвета для разных типов ресурсов
                Brush color = type switch
                {
                    ResourceType.Iron => Brushes.Gray,
                    ResourceType.Copper => Brushes.Orange,
                    ResourceType.Coal => Brushes.Black,
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, 50, 50));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, 50, 50));

                // Текст с названием ресурса
                string text = type.ToString().Substring(0, 1);
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(15, 12));
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

        public void SetInventoryPanel(StackPanel panel)
        {
            inventoryPanel = panel;
            for (int i = 0; i < 5; i++)
            {
                UpdateInventorySlot(i);
            }
        }
    }
}