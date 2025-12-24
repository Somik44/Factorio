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
    public class Miner
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public bool IsBuilt { get; private set; }
        public bool IsPlacedOnResource { get; private set; }
        public ResourceType MiningType { get; private set; }
        public Window InterfaceWindow { get; private set; }

        public InventorySlot OutputSlot { get; private set; }

        private DispatcherTimer miningTimer;
        private Player player;
        private Border outputBorder;
        private DispatcherTimer updateTimer;
        private Resource targetResource;

        //Инициализация
        public Miner(double x, double y, Player player)
        {
            X = x;
            Y = y;
            Width = 90;
            Height = 90;
            IsBuilt = false;
            IsPlacedOnResource = false;
            MiningType = ResourceType.None;
            this.player = player;

            InitializeSprite();
            InitializeInventory();
        }

        private void InitializeSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform,
                Source = LoadMinerTexture(),
                Cursor = Cursors.Hand,
                Opacity = 0.5
            };

            Sprite.MouseDown += OnMinerClicked;
            UpdatePosition();
        }

        private BitmapImage LoadMinerTexture()
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string filePath = Path.Combine(basePath, "Mining.png");

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            return CreateSimplePlaceholder();
        }

        private BitmapImage CreateSimplePlaceholder()
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkBlue, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                var formattedText = new FormattedText(
                    "MI",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White,
                    1.0);

                drawingContext.DrawText(formattedText, new Point(Width / 2 - 15, Height / 2 - 10));
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

        private void InitializeInventory()
        {
            OutputSlot = new InventorySlot { Type = ResourceType.None, Count = 0 };

            miningTimer = new DispatcherTimer();
            miningTimer.Interval = TimeSpan.FromSeconds(1.5);
            miningTimer.Tick += MiningTimer_Tick;
        }

        private void MiningTimer_Tick(object sender, EventArgs e)
        {
            if (IsPlacedOnResource && IsBuilt && OutputSlot.Count < 99)
            {
                if (OutputSlot.Type == ResourceType.None)
                {
                    OutputSlot.Type = MiningType;
                    OutputSlot.Count = 1;
                }
                else if (OutputSlot.Type == MiningType)
                {
                    OutputSlot.Count++;
                }

                UpdateInterface();
            }
        }

        //Постройка
        public bool CheckPlacementOnResource(List<Resource> resources)
        {
            double minerCenterX = X + Width / 2;
            double minerCenterY = Y + Height / 2;

            foreach (var resource in resources)
            {

                double resourceCenterX = resource.X + resource.Width / 2;
                double resourceCenterY = resource.Y + resource.Height / 2;

                double distance = Math.Sqrt(
                    Math.Pow(resourceCenterX - minerCenterX, 2) +
                    Math.Pow(resourceCenterY - minerCenterY, 2)
                );

                if (distance <= 30)
                {
                    IsPlacedOnResource = true;
                    MiningType = resource.Type;
                    targetResource = resource;
                    Sprite.Opacity = 1.0;
                    return true;
                }
            }

            IsPlacedOnResource = false;
            MiningType = ResourceType.None;
            Sprite.Opacity = 0.5;
            return false;
        }

        public void Build()
        {
            IsBuilt = true;
            Sprite.Opacity = 1.0;
            miningTimer.Start();
        }

        //Интерфейс
        private void OnMinerClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed && IsBuilt)
            {
                OpenInterface();
            }
        }

        public void OpenInterface()
        {
            if (InterfaceWindow != null && InterfaceWindow.IsVisible)
            {
                InterfaceWindow.Focus();
                return;
            }

            InterfaceWindow = new Window
            {
                Title = "Добытчик",
                Width = 300,
                Height = 250,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var infoPanel = new StackPanel
            {
                Margin = new Thickness(10),
                Orientation = Orientation.Vertical
            };

            var typeText = new TextBlock
            {
                Text = $"Тип добычи: {GetResourceName(MiningType)}",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 5, 0, 5)
            };


            infoPanel.Children.Add(typeText);
            Grid.SetRow(infoPanel, 0);
            mainGrid.Children.Add(infoPanel);

            outputBorder = CreateSlotBorder("Добыча", "Добытые ресурсы");
            outputBorder.MouseDown += (s, e) => HandleSlotClick(OutputSlot, e);
            outputBorder.HorizontalAlignment = HorizontalAlignment.Center;
            outputBorder.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(outputBorder, 0);
            mainGrid.Children.Add(outputBorder);

            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                Height = 40
            };
            Grid.SetRow(controlPanel, 1);
            mainGrid.Children.Add(controlPanel);

            var takeAllButton = new Button
            {
                Content = "Забрать всё",
                Margin = new Thickness(5),
                Width = 100,
                Height = 30,
                ToolTip = "Забрать все добытые ресурсы в инвентарь"
            };
            takeAllButton.Click += (s, e) => TakeAllResources();
            controlPanel.Children.Add(takeAllButton);

            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(100);
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            InterfaceWindow.Closed += (s, e) =>
            {
                updateTimer.Stop();
                InterfaceWindow = null;
            };

            InterfaceWindow.Content = mainGrid;
            InterfaceWindow.Show();
            UpdateInterface();
        }

        private string GetResourceName(ResourceType type)
        {
            return type switch
            {
                ResourceType.Iron => "Железо",
                ResourceType.Copper => "Медь",
                ResourceType.Coal => "Уголь",
                ResourceType.Stone => "Камень",
                ResourceType.IronIngot => "Железный слиток",
                ResourceType.CopperIngot => "Медный слиток",
                _ => "Нет"
            };
        }

        private Border CreateSlotBorder(string title, string tooltip)
        {
            var border = new Border
            {
                Width = 100,
                Height = 100,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10),
                Background = Brushes.DarkGray,
                ToolTip = tooltip,
                CornerRadius = new CornerRadius(5)
            };

            var stackPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 5),
                FontWeight = FontWeights.Bold
            };
            stackPanel.Children.Add(titleText);

            border.Child = stackPanel;
            return border;
        }

        //Ресурсы
        private void HandleSlotClick(InventorySlot slot, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                return;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                TakeResourceFromSlot(slot);
            }
        }

        private void TakeResourceFromSlot(InventorySlot slot)
        {
            if (slot.Type == ResourceType.None || slot.Count <= 0)
            {
                return;
            }

            int amount = 1;

            if (player.AddResource(slot.Type, amount))
            {
                slot.Count -= amount;

                if (slot.Count <= 0)
                {
                    slot.Type = ResourceType.None;
                    slot.Count = 0;
                }

                UpdateInterface();
            }
            else
            {
                MessageBox.Show("В вашем инвентаре нет места!", "Инвентарь полон", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TakeAllResources()
        {
            TakeAllFromSlot(OutputSlot);
            UpdateInterface();
        }

        private void TakeAllFromSlot(InventorySlot slot)
        {
            if (slot.Type == ResourceType.None || slot.Count <= 0)
                return;

            int amount = slot.Count;
            while (amount > 0)
            {
                if (player.AddResource(slot.Type, 1))
                {
                    amount--;
                    slot.Count--;
                }
                else
                {
                    break;
                }
            }

            if (slot.Count <= 0)
            {
                slot.Type = ResourceType.None;
                slot.Count = 0;
            }
        }

        //Отображение
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateSlotDisplay(outputBorder, OutputSlot);
        }

        private void UpdateSlotDisplay(Border border, InventorySlot slot)
        {
            if (border?.Child is StackPanel stackPanel)
            {
                if (stackPanel.Children.Count > 1)
                {
                    stackPanel.Children.RemoveAt(1);
                }

                if (slot.Type != ResourceType.None)
                {
                    var contentPanel = new StackPanel();

                    var icon = new Image
                    {
                        Width = 50,
                        Height = 50,
                        Source = LoadResourceIcon(slot.Type),
                        Stretch = Stretch.Uniform
                    };
                    contentPanel.Children.Add(icon);

                    var countText = new TextBlock
                    {
                        Text = slot.Count.ToString(),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 16
                    };
                    contentPanel.Children.Add(countText);

                    stackPanel.Children.Add(contentPanel);
                }
            }
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
            var renderTarget = new RenderTargetBitmap(50, 50, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, 50, 50));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, 50, 50));

                string text = type.ToString().Substring(0, 2);
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    16,
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

        public void UpdateInterface()
        {
            if (InterfaceWindow != null && InterfaceWindow.IsVisible)
            {
                UpdateSlotDisplay(outputBorder, OutputSlot);
            }
        }

        //Предметы
        public int GetOutputCount()
        {
            return OutputSlot.Count;
        }

        public ResourceType GetOutputType()
        {
            return OutputSlot.Type;
        }

        public InventorySlot GetOutputSlot()
        {
            return OutputSlot;
        }

        //Размещени
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
        }

        public void CloseInterface()
        {
            InterfaceWindow?.Close();
        }

        public void SetTargetResource(Resource resource)
        {
            targetResource = resource;
            if (resource != null)
            {
                MiningType = resource.Type;
                IsPlacedOnResource = true;
            }
        }

        public bool IsPointInside(Point point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }

        public bool AddResource(ResourceType type, int amount)
        {
            if (OutputSlot.Type == ResourceType.None)
            {
                OutputSlot.Type = type;
                OutputSlot.Count = Math.Min(amount, 99);
                return true;
            }
            else if (OutputSlot.Type == type && OutputSlot.Count + amount <= 99)
            {
                OutputSlot.Count += amount;
                return true;
            }
            return false;
        }
    }
}