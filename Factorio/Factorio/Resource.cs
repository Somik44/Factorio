using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Factorio
{
    public class Resource
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public ResourceType Type { get; private set; }
        public int Amount { get; private set; }
        private TextBlock amountText;

        public Resource(double x, double y, ResourceType type, int amount = 1)
        {
            X = x;
            Y = y;
            Type = type;

            switch (type)
            {
                case ResourceType.Coal:
                    Width = 25;
                    Height = 25;
                    break;
                default:
                    Width = 40;
                    Height = 40;
                    break;
            }
            Amount = amount;

            InitializeSprite();
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform,
                Source = LoadResourceTexture(Type)
            };

            amountText = new TextBlock
            {
                Text = Amount.ToString(),
                Foreground = Brushes.White,
                Background = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2)
            };

            UpdatePosition();
        }

        private BitmapImage LoadResourceTexture(ResourceType type)
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Resources\";
            string fileName = type switch
            {
                ResourceType.Iron => "iron.png",
                ResourceType.Copper => "copper.png",
                ResourceType.Coal => "coal.png",
                _ => "default.png"
            };

            string filePath = Path.Combine(basePath, fileName);

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreatePlaceholderTexture(type);
        }

        private BitmapImage CreatePlaceholderTexture(ResourceType type)
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
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

                drawingContext.DrawRectangle(color, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                string letter = type switch
                {
                    ResourceType.Iron => "Fe",
                    ResourceType.Copper => "Cu",
                    ResourceType.Coal => "Co",
                    _ => "??"
                };

                var formattedText = new FormattedText(
                    letter,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    12,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(Width / 2 - 10, Height / 2 - 8));
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

        public void UpdatePosition()
        {
            Canvas.SetLeft(Sprite, X);
            Canvas.SetTop(Sprite, Y);

            if (amountText != null && amountText.Parent != null)
            {
                Canvas.SetLeft(amountText, X + Width - 15);
                Canvas.SetTop(amountText, Y - 5);
            }
        }

        public void AddToCanvas(Canvas canvas)
        {
            if (!canvas.Children.Contains(Sprite))
            {
                canvas.Children.Add(Sprite);
                Canvas.SetZIndex(Sprite, 10); // Ресурсы под игроком
                UpdatePosition();

                canvas.Children.Add(amountText);
                Canvas.SetZIndex(amountText, 11);
            }
        }

        public void RemoveFromCanvas(Canvas canvas)
        {
            if (canvas.Children.Contains(Sprite))
                canvas.Children.Remove(Sprite);
            if (canvas.Children.Contains(amountText))
                canvas.Children.Remove(amountText);
        }

        public void DecreaseAmount(int amount)
        {
            Amount -= amount;
            if (Amount < 0) Amount = 0;

            amountText.Text = Amount.ToString();

            if (Amount == 0)
            {
                Sprite.Opacity = 0.3;
            }
        }

        public bool IsPointInside(Point point)
        {
            return point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
        }

        public bool IsPlayerInRange(Player player, double range = 80)
        {
            double playerCenterX = player.X + player.Width / 2;
            double playerCenterY = player.Y + player.Height / 2;
            double resourceCenterX = X + Width / 2;
            double resourceCenterY = Y + Height / 2;

            double distance = Math.Sqrt(Math.Pow(playerCenterX - resourceCenterX, 2) + Math.Pow(playerCenterY - resourceCenterY, 2));
            return distance <= range;
        }
    }
}