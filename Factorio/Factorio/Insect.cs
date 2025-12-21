using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;

namespace Factorio
{
    public class Insect
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Speed { get; private set; }
        public int Health { get; private set; }
        public bool IsDead { get; private set; }
        public Player TargetPlayer { get; private set; }

        private DispatcherTimer moveTimer;
        private DispatcherTimer animationTimer;
        private int currentFrame = 1;
        private bool movingRight = false;
        private int damageToPlayer = 0;
        private const int PlayerMaxHealth = 4;
        private Random random = new Random();

        public Insect(double x, double y, Player targetPlayer)
        {
            X = x;
            Y = y;
            Width = 40;
            Height = 40;
            Speed = 3;
            Health = 2; // Нужно 2 попадания для уничтожения
            IsDead = false;
            TargetPlayer = targetPlayer;

            InitializeSprite();
            InitializeTimers();
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
            UpdateAnimation();
        }

        private void InitializeTimers()
        {
            // Таймер для движения
            moveTimer = new DispatcherTimer();
            moveTimer.Interval = TimeSpan.FromMilliseconds(50);
            moveTimer.Tick += (s, e) => Move();
            moveTimer.Start();

            // Таймер для анимации
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(200);
            animationTimer.Tick += (s, e) => UpdateAnimation();
            animationTimer.Start();
        }

        private void Move()
        {
            if (IsDead || TargetPlayer == null) return;

            // Вычисляем направление к игроку
            double playerCenterX = TargetPlayer.X + TargetPlayer.Width / 2;
            double playerCenterY = TargetPlayer.Y + TargetPlayer.Height / 2;
            double insectCenterX = X + Width / 2;
            double insectCenterY = Y + Height / 2;

            double deltaX = playerCenterX - insectCenterX;
            double deltaY = playerCenterY - insectCenterY;

            // Нормализуем вектор
            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length > 0)
            {
                deltaX /= length;
                deltaY /= length;
            }

            // Обновляем направление анимации
            movingRight = deltaX > 0;

            // Двигаемся к игроку
            X += deltaX * Speed;
            Y += deltaY * Speed;

            UpdatePosition();

            // Проверяем столкновение с игроком
            CheckCollisionWithPlayer();
        }

        private void CheckCollisionWithPlayer()
        {
            if (IsDead || TargetPlayer == null) return;

            // Простая проверка столкновения (пересечение прямоугольников)
            Rect insectRect = new Rect(X, Y, Width, Height);
            Rect playerRect = new Rect(TargetPlayer.X, TargetPlayer.Y, TargetPlayer.Width, TargetPlayer.Height);

            if (insectRect.IntersectsWith(playerRect))
            {
                // Наносим урон игроку
                damageToPlayer++;
                // Здесь можно добавить визуальную индикацию урона

                // Проверяем, умер ли игрок
                if (damageToPlayer >= PlayerMaxHealth)
                {
                    // Игрок умер - можно добавить обработку
                    // Например, показать сообщение или перезапустить игру
                    Console.WriteLine("Игрок убит жуком!");
                }
            }
        }

        private void UpdateAnimation()
        {
            if (IsDead) return;

            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\npc\";
            string direction = movingRight ? "right" : "left";

            // Переключаем кадры анимации (1 или 2)
            currentFrame = currentFrame == 1 ? 2 : 1;
            string fileName = $"{direction}_{currentFrame}.png";
            string filePath = Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                Sprite.Source = new BitmapImage(new Uri(filePath));
            }
            else
            {
                // Заглушка, если файл не найден
                Sprite.Source = CreatePlaceholderSprite();
            }
        }

        private BitmapImage CreatePlaceholderSprite()
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkGreen, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                var formattedText = new FormattedText(
                    "BUG",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    10,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(5, 10));
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

        public void TakeDamage(int damage = 1)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            IsDead = true;
            moveTimer.Stop();
            animationTimer.Stop();
            Sprite.Visibility = Visibility.Collapsed;
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
                Canvas.SetZIndex(Sprite, 40);
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