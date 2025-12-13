using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Windows;
using System.IO;

namespace Factorio
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public class Player
    {
        // Свойства персонажа
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Speed { get; set; }
        public Direction CurrentDirection { get; private set; }

        // Анимации
        private Dictionary<Direction, List<BitmapImage>> animations;
        private int currentFrame = 0;
        private int animationSpeed = 150; // мс между кадрами
        private DateTime lastFrameTime;

        // Состояние движения
        public bool IsMoving { get; private set; }

        public Player(double startX, double startY, double width, double height)
        {
            X = startX;
            Y = startY;
            Width = width;
            Height = height;
            Speed = 3.0;
            CurrentDirection = Direction.Down;
            lastFrameTime = DateTime.Now;

            InitializeSprite();
            LoadAnimations();
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = System.Windows.Media.Stretch.Uniform
            };

            UpdatePosition();
        }

        private void LoadAnimations()
        {
            animations = new Dictionary<Direction, List<BitmapImage>>();
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Player\";

            // Создаем списки для каждого направления
            animations[Direction.Up] = new List<BitmapImage>();
            animations[Direction.Down] = new List<BitmapImage>();
            animations[Direction.Left] = new List<BitmapImage>();
            animations[Direction.Right] = new List<BitmapImage>();

            // Загружаем спрайты для каждого направления
            LoadDirectionSprites(Direction.Up, basePath);
            LoadDirectionSprites(Direction.Down, basePath);
            LoadDirectionSprites(Direction.Left, basePath);
            LoadDirectionSprites(Direction.Right, basePath);
        }

        private void LoadDirectionSprites(Direction direction, string basePath)
        {
            string directionName = direction.ToString();

            // Сначала пытаемся загрузить спрайт стояния
            string standingPath = Path.Combine(basePath, $"{directionName}.png");
            if (File.Exists(standingPath))
            {
                animations[direction].Add(new BitmapImage(new Uri(standingPath)));
            }
            else
            {
                // Создаем заглушку для стояния
                animations[direction].Add(CreatePlaceholderSprite(direction, "S"));
            }

            // Затем загружаем спрайты движения
            for (int i = 1; i <= 2; i++)
            {
                string movePath = Path.Combine(basePath, $"{directionName}_move({i}).png");
                if (File.Exists(movePath))
                {
                    animations[direction].Add(new BitmapImage(new Uri(movePath)));
                }
                else
                {
                    // Создаем заглушку для движения
                    animations[direction].Add(CreatePlaceholderSprite(direction, $"M{i}"));
                }
            }
        }

        private BitmapImage CreatePlaceholderSprite(Direction direction, string label = "S")
        {
            // Создание простого спрайта-заглушки
            var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Width, (int)Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            var drawingVisual = new System.Windows.Media.DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Разные цвета для разных направлений
                System.Windows.Media.Brush color = direction switch
                {
                    Direction.Up => System.Windows.Media.Brushes.Blue,
                    Direction.Down => System.Windows.Media.Brushes.Green,
                    Direction.Left => System.Windows.Media.Brushes.Yellow,
                    Direction.Right => System.Windows.Media.Brushes.Red,
                    _ => System.Windows.Media.Brushes.White
                };

                // Фон
                drawingContext.DrawRectangle(color, null,
                    new Rect(0, 0, Width, Height));

                // Черная рамка
                drawingContext.DrawRectangle(
                    System.Windows.Media.Brushes.Black,
                    new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 2),
                    new Rect(0, 0, Width, Height));

                // Текст с направлением и типом спрайта
                string text = $"{direction.ToString()[0]}{label}";
                drawingContext.DrawText(
                    new System.Windows.Media.FormattedText(
                        text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        14,
                        System.Windows.Media.Brushes.Black),
                    new Point(Width / 2 - 15, Height / 2 - 7));
            }

            renderTarget.Render(drawingVisual);
            var bitmap = new BitmapImage();
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTarget));

            using (var stream = new System.IO.MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, System.IO.SeekOrigin.Begin);
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
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

            // Ограничиваем перемещение в пределах экрана
            X = Math.Max(0, Math.Min(X, SystemParameters.PrimaryScreenWidth - Width));
            Y = Math.Max(0, Math.Min(Y, SystemParameters.PrimaryScreenHeight - Height));

            UpdatePosition();
        }

        public void Stop()
        {
            IsMoving = false;
            currentFrame = 0; // Возвращаемся к первому кадру (стояние)
        }

        public void UpdateAnimation()
        {
            if (IsMoving && animations.ContainsKey(CurrentDirection) && animations[CurrentDirection].Count > 0)
            {
                // Проверяем время для смены кадра
                if ((DateTime.Now - lastFrameTime).TotalMilliseconds >= animationSpeed)
                {
                    // При движении переключаемся между кадрами 1 и 2 (движение)
                    // Кадр 0 - стояние
                    if (currentFrame == 0)
                        currentFrame = 1;
                    else
                        currentFrame = (currentFrame == 1) ? 2 : 1;

                    lastFrameTime = DateTime.Now;
                }

                // Устанавливаем текущий спрайт
                Sprite.Source = animations[CurrentDirection][currentFrame];
            }
            else if (animations.ContainsKey(CurrentDirection) && animations[CurrentDirection].Count > 0)
            {
                // Если персонаж не двигается, показываем первый кадр (стояние)
                currentFrame = 0;
                Sprite.Source = animations[CurrentDirection][0];
            }
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
    }
}