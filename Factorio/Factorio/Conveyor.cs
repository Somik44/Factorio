using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Factorio
{
    public class Conveyor
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public Direction Direction { get; private set; }
        public bool IsBuilt { get; private set; }
        public bool IsActive { get; private set; }

        // Связанные здания
        public Miner SourceMiner { get; private set; }
        public Smelter TargetSmelter { get; private set; }

        // Текущий переносимый ресурс
        private ResourceType currentResource = ResourceType.None;
        private double transportProgress = 0;
        private const double transportTime = 2.0; // Время передачи ресурса
        private bool isTransporting = false;

        // Анимация
        private DispatcherTimer animationTimer;
        private int currentFrame = 0;
        private List<BitmapImage> animationFrames;
        private DispatcherTimer transportTimer;

        // Для отображения ресурса на конвейере
        private Image resourceSprite;

        public Conveyor(double x, double y, Direction direction)
        {
            X = x;
            Y = y;
            Direction = direction;
            Width = 30;
            Height = 30;
            IsBuilt = false;
            IsActive = false;

            InitializeSprite();
            InitializeAnimation();
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform,
                Source = LoadConveyorTexture(Direction, 0),
                Opacity = 0.7
            };

            UpdatePosition();
        }

        private void InitializeAnimation()
        {
            animationFrames = new List<BitmapImage>();
            LoadAnimationFrames();

            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(200);
            animationTimer.Tick += AnimationTimer_Tick;

            transportTimer = new DispatcherTimer();
            transportTimer.Interval = TimeSpan.FromMilliseconds(100);
            transportTimer.Tick += TransportTimer_Tick;

            // Спрайт для отображения ресурса
            resourceSprite = new Image
            {
                Width = 30,
                Height = 30,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Collapsed
            };
        }

        private BitmapImage LoadConveyorTexture(Direction direction, int frame)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\conveyor\";
            string directionName = direction.ToString().ToLower();

            // Для frame=0 используем первый кадр, для frame=1 - второй
            int fileFrame = frame == 0 ? 1 : 2;
            string fileName = $"{directionName}_{fileFrame}.png";

            string filePath = Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreatePlaceholderFrame(directionName, frame);
        }

        private void LoadAnimationFrames()
        {
            animationFrames = new List<BitmapImage>();
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\conveyor\";
            string directionName = Direction.ToString().ToLower();

            // Загружаем оба кадра: _1.png и _2.png
            for (int i = 1; i <= 2; i++)
            {
                string fileName = $"{directionName}_{i}.png";
                string filePath = Path.Combine(basePath, fileName);

                if (File.Exists(filePath))
                {
                    animationFrames.Add(new BitmapImage(new Uri(filePath)));
                }
                else
                {
                    animationFrames.Add(CreatePlaceholderFrame(directionName, i));
                }
            }
        }

        private BitmapImage CreatePlaceholderFrame(string direction, int frame)
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                Brush color = frame switch
                {
                    0 => Brushes.DarkGray,
                    1 => Brushes.Gray,
                    2 => Brushes.LightGray,
                    _ => Brushes.White
                };

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                string text = $"{direction.Substring(0, 1).ToUpper()}{frame}";
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    14,
                    Brushes.White,
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

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (IsActive && animationFrames.Count > 0)
            {
                currentFrame = (currentFrame + 1) % animationFrames.Count;
                Sprite.Source = animationFrames[currentFrame];
            }
        }

        private void TransportTimer_Tick(object sender, EventArgs e)
        {
            UpdateTransport();
        }

        public void ConnectBuildings(Miner miner, Smelter smelter)
        {
            SourceMiner = miner;
            TargetSmelter = smelter;
            IsActive = true;

            if (IsBuilt)
            {
                animationTimer.Start();
                transportTimer.Start();
            }
        }

        public void Build()
        {
            IsBuilt = true;
            Sprite.Opacity = 1.0;

            if (IsActive)
            {
                animationTimer.Start();
                transportTimer.Start();
            }
        }

        private void UpdateTransport()
        {
            // Если уже транспортируем ресурс
            if (isTransporting)
            {
                transportProgress += 0.1;

                // Обновляем позицию ресурса на конвейере
                UpdateResourcePosition();

                if (transportProgress >= transportTime)
                {
                    // Завершаем транспортировку
                    DeliverResource();
                    transportProgress = 0;
                    isTransporting = false;
                    resourceSprite.Visibility = Visibility.Collapsed;
                }
                return;
            }

            // Если нет активной транспортировки, проверяем можно ли взять ресурс у майнера
            if (SourceMiner != null && SourceMiner.IsBuilt && SourceMiner.IsPlacedOnResource)
            {
                if (SourceMiner.GetOutputCount() > 0)
                {
                    // Берем ресурс у майнера
                    currentResource = SourceMiner.GetOutputType();
                    if (TakeResourceFromMiner())
                    {
                        // Начинаем транспортировку
                        isTransporting = true;
                        transportProgress = 0;

                        // Настраиваем спрайт ресурса
                        resourceSprite.Source = LoadResourceIcon(currentResource);
                        resourceSprite.Visibility = Visibility.Visible;
                        UpdateResourcePosition();
                    }
                }
            }
        }

        private bool TakeResourceFromMiner()
        {
            if (SourceMiner == null || currentResource == ResourceType.None)
                return false;

            // Пытаемся взять ресурс из выходного слота майнера
            var outputSlot = SourceMiner.GetOutputSlot();
            if (outputSlot.Type == currentResource && outputSlot.Count > 0)
            {
                outputSlot.Count--;
                if (outputSlot.Count <= 0)
                {
                    outputSlot.Type = ResourceType.None;
                    outputSlot.Count = 0;
                }

                // Обновляем интерфейс майнера
                SourceMiner.UpdateInterface();
                return true;
            }

            return false;
        }

        private void UpdateResourcePosition()
        {
            if (!isTransporting) return;

            double progress = transportProgress / transportTime;
            double posX = X;
            double posY = Y;

            // Вычисляем позицию ресурса на конвейере в зависимости от направления
            switch (Direction)
            {
                case Direction.Down:
                    posX = X + Width / 2 - 15;
                    posY = Y + (Height * progress);
                    break;
                case Direction.Up:
                    posX = X + Width / 2 - 15;
                    posY = Y + Height - (Height * progress);
                    break;
                case Direction.Left:
                    posX = X + Width - (Width * progress);
                    posY = Y + Height / 2 - 15;
                    break;
                case Direction.Right:
                    posX = X + (Width * progress);
                    posY = Y + Height / 2 - 15;
                    break;
                default:
                    // Для Direction.None ничего не делаем
                    return;
            }

            Canvas.SetLeft(resourceSprite, posX);
            Canvas.SetTop(resourceSprite, posY);
        }

        private void DeliverResource()
        {
            if (TargetSmelter == null || !TargetSmelter.IsBuilt) return;

            // Определяем в какой слот плавильни отдать ресурс
            string slotType = "input";
            if (currentResource == ResourceType.Coal)
            {
                slotType = "fuel";
            }

            // Пытаемся добавить ресурс в плавильню
            if (!TargetSmelter.AddItem(currentResource, 1, slotType))
            {
                // Если не получилось (например, слот полон или не тот тип),
                // возвращаем ресурс майнеру
                ReturnResourceToMiner();
            }

            currentResource = ResourceType.None;
        }

        private void ReturnResourceToMiner()
        {
            if (SourceMiner == null || currentResource == ResourceType.None)
                return;

            // Возвращаем ресурс в майнер
            var outputSlot = SourceMiner.GetOutputSlot();
            if (outputSlot.Type == ResourceType.None)
            {
                outputSlot.Type = currentResource;
                outputSlot.Count = 1;
            }
            else if (outputSlot.Type == currentResource && outputSlot.Count < 99)
            {
                outputSlot.Count++;
            }

            SourceMiner.UpdateInterface();
        }

        private BitmapImage LoadResourceIcon(ResourceType type)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Resources\";
            string fileName = type switch
            {
                ResourceType.Iron => "iron.png",
                ResourceType.Copper => "copper.png",
                ResourceType.Coal => "coal.png",
                ResourceType.Stone => "stone.png",
                ResourceType.IronIngot => "iron_ingot.png",
                ResourceType.CopperIngot => "copper_ingot.png",
                _ => "default.png"
            };

            string filePath = Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreateSimpleResourceIcon(type);
        }

        private BitmapImage CreateSimpleResourceIcon(ResourceType type)
        {
            var renderTarget = new RenderTargetBitmap(30, 30, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, 30, 30));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 1), new Rect(0, 0, 30, 30));

                string text = type.ToString().Substring(0, 2);
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    10,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(8, 8));
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
                Canvas.SetZIndex(Sprite, 30);
                UpdatePosition();
            }

            if (!canvas.Children.Contains(resourceSprite))
            {
                canvas.Children.Add(resourceSprite);
                Canvas.SetZIndex(resourceSprite, 31);
            }
        }


        public void RemoveFromCanvas(Canvas canvas)
        {
            if (canvas.Children.Contains(Sprite))
                canvas.Children.Remove(Sprite);
            if (canvas.Children.Contains(resourceSprite))
                canvas.Children.Remove(resourceSprite);
        }

        public bool IsPointInside(Point point)
        {
            return point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
        }

        public void Disconnect()
        {
            IsActive = false;
            animationTimer.Stop();
            transportTimer.Stop();

            if (isTransporting)
            {
                // Возвращаем ресурс майнеру, если он был в процессе транспортировки
                ReturnResourceToMiner();
                isTransporting = false;
                resourceSprite.Visibility = Visibility.Collapsed;
            }

            SourceMiner = null;
            TargetSmelter = null;
        }
    }
}