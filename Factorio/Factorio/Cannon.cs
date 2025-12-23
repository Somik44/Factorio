using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Shapes;

namespace Factorio
{
    public class Cannon
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public bool IsBuilt { get; private set; }
        public double Range { get; private set; } = 120;

        private DispatcherTimer shootTimer;
        private DispatcherTimer shotAnimationTimer;
        private Random random = new Random();
        private List<Insect> targetInsects;
        private Canvas gameCanvas;
        private List<Ellipse> activeShots = new List<Ellipse>();

        public Cannon(double x, double y)
        {
            X = x;
            Y = y;
            Width = 60;
            Height = 60;
            IsBuilt = false;

            InitializeSprite();
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform,
                Source = LoadCannonTexture()
            };

            UpdatePosition();
        }

        private BitmapImage LoadCannonTexture()
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\npc\";
            string filePath = System.IO.Path.Combine(basePath, "cannon.png");

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreatePlaceholderSprite();
        }

        private BitmapImage CreatePlaceholderSprite()
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkRed, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                var formattedText = new FormattedText(
                    "CAN",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    14,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(15, 20));
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

        public void Build()
        {
            IsBuilt = true;
            Sprite.Opacity = 1.0;

            shootTimer = new DispatcherTimer();
            shootTimer.Interval = TimeSpan.FromSeconds(1); 
            shootTimer.Tick += (s, e) => Shoot();
            shootTimer.Start();

            shotAnimationTimer = new DispatcherTimer();
            shotAnimationTimer.Interval = TimeSpan.FromMilliseconds(50);
            shotAnimationTimer.Start();
        }

        public void SetTargetInsects(List<Insect> insects)
        {
            targetInsects = insects;
        }

        public void SetGameCanvas(Canvas canvas)
        {
            gameCanvas = canvas;
        }

        private void Shoot()
        {
            if (targetInsects == null || targetInsects.Count == 0) return;
            if (gameCanvas == null) return;

            Insect nearestInsect = null;
            double nearestDistance = double.MaxValue;

            foreach (var insect in targetInsects)
            {
                if (insect.IsDead) continue;

                double insectCenterX = insect.X + insect.Width / 2;
                double insectCenterY = insect.Y + insect.Height / 2;
                double cannonCenterX = X + Width / 2;
                double cannonCenterY = Y + Height / 2;

                double distance = Math.Sqrt(
                    Math.Pow(insectCenterX - cannonCenterX, 2) +
                    Math.Pow(insectCenterY - cannonCenterY, 2));

                if (distance <= Range && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestInsect = insect;
                }
            }

            if (nearestInsect != null)
            {
                CreateShot(nearestInsect);

                nearestInsect.TakeDamage(1);
            }
        }

        private void CreateShot(Insect target)
        {
            if (gameCanvas == null) return;

            Ellipse shot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new RadialGradientBrush(
                    Colors.Yellow,
                    Colors.OrangeRed)
                {
                    RadiusX = 0.5,
                    RadiusY = 0.5
                },
                Stroke = Brushes.Red,
                StrokeThickness = 1
            };

            double startX = X + Width / 2 - shot.Width / 2;
            double startY = Y + Height / 2 - shot.Height / 2;

            double targetX = target.X + target.Width / 2 - shot.Width / 2;
            double targetY = target.Y + target.Height / 2 - shot.Height / 2;

            Canvas.SetLeft(shot, startX);
            Canvas.SetTop(shot, startY);
            Canvas.SetZIndex(shot, 60); 

            gameCanvas.Children.Add(shot);

            activeShots.Add(shot);

            AnimateShot(shot, startX, startY, targetX, targetY, target);
        }

        private void AnimateShot(Ellipse shot, double startX, double startY, double targetX, double targetY, Insect target)
        {
            double progress = 0;
            double duration = 0.5; // 0.5 секунды на полет
            int steps = 25; // Количество шагов анимации
            double stepTime = duration * 1000 / steps; // Время одного шага в мс

            DispatcherTimer animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(stepTime);

            animationTimer.Tick += (s, e) =>
            {
                progress += 1.0 / steps;

                if (progress >= 1.0 || target.IsDead)
                {
                    // Завершаем анимацию
                    animationTimer.Stop();

                    // Создаем эффект попадания
                    CreateHitEffect(targetX, targetY);

                    // Удаляем снаряд
                    if (gameCanvas.Children.Contains(shot))
                    {
                        gameCanvas.Children.Remove(shot);
                    }
                    activeShots.Remove(shot);
                }
                else
                {
                    // Плавное движение с замедлением в конце (ease-out)
                    double easedProgress = 1 - Math.Pow(1 - progress, 2);

                    double currentX = startX + (targetX - startX) * easedProgress;
                    double currentY = startY + (targetY - startY) * easedProgress;

                    Canvas.SetLeft(shot, currentX);
                    Canvas.SetTop(shot, currentY);

                    // Небольшое изменение размера для эффекта "дрожания"
                    shot.Width = 10 + Math.Sin(progress * Math.PI * 10) * 2;
                    shot.Height = 10 + Math.Cos(progress * Math.PI * 10) * 2;
                }
            };

            animationTimer.Start();
        }

        private void CreateHitEffect(double x, double y)
        {
            if (gameCanvas == null) return;

            // Создаем эффект попадания (вспышка)
            Ellipse hitEffect = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new RadialGradientBrush(
                    Colors.White,
                    Colors.Transparent)
                {
                    RadiusX = 0.5,
                    RadiusY = 0.5
                },
                Opacity = 0.8
            };

            Canvas.SetLeft(hitEffect, x - hitEffect.Width / 2);
            Canvas.SetTop(hitEffect, y - hitEffect.Height / 2);
            Canvas.SetZIndex(hitEffect, 70);

            gameCanvas.Children.Add(hitEffect);

            // Анимация исчезновения эффекта
            DispatcherTimer effectTimer = new DispatcherTimer();
            effectTimer.Interval = TimeSpan.FromMilliseconds(50);
            double effectProgress = 0;

            effectTimer.Tick += (s, e) =>
            {
                effectProgress += 0.2;
                hitEffect.Opacity = 0.8 * (1 - effectProgress);
                hitEffect.Width = 20 * (1 + effectProgress * 0.5);
                hitEffect.Height = 20 * (1 + effectProgress * 0.5);

                Canvas.SetLeft(hitEffect, x - hitEffect.Width / 2);
                Canvas.SetTop(hitEffect, y - hitEffect.Height / 2);

                if (effectProgress >= 1.0)
                {
                    effectTimer.Stop();
                    if (gameCanvas.Children.Contains(hitEffect))
                    {
                        gameCanvas.Children.Remove(hitEffect);
                    }
                }
            };

            effectTimer.Start();
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
                Canvas.SetZIndex(Sprite, 50);
                UpdatePosition();
            }
        }

        public void RemoveFromCanvas(Canvas canvas)
        {
            if (canvas.Children.Contains(Sprite))
            {
                canvas.Children.Remove(Sprite);
            }

            foreach (var shot in activeShots)
            {
                if (canvas.Children.Contains(shot))
                {
                    canvas.Children.Remove(shot);
                }
            }
            activeShots.Clear();
        }

        public bool IsPointInside(Point point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }
    }
}