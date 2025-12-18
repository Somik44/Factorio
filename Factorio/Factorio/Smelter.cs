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
    public class Smelter
    {
        public Image Sprite { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public bool IsBuilt { get; private set; }
        public Window InterfaceWindow { get; private set; }

        // Слоты инвентаря плавильни
        public InventorySlot FuelSlot { get; private set; }
        public InventorySlot InputSlot { get; private set; }
        public InventorySlot OutputSlot { get; private set; }

        private double smeltingProgress = 0;
        private const double smeltingTime = 3.0;
        private bool isSmelting = false;
        private DispatcherTimer smeltingTimer;
        private Player player;
        private Border fuelBorder, inputBorder, outputBorder;
        private ProgressBar progressBar;
        private DispatcherTimer updateTimer;

        public Smelter(double x, double y, Player player)
        {
            X = x;
            Y = y;
            Width = 150;  // Нормальный размер
            Height = 150; // Нормальный размер
            IsBuilt = false;
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
                Source = LoadSmelterTexture(),
                Cursor = Cursors.Hand
            };

            // Добавляем обработчик клика
            Sprite.MouseDown += OnSmelterClicked;

            UpdatePosition();
        }

        private BitmapImage LoadSmelterTexture()
        {
            string basePath = @"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Factory\";
            string filePath = Path.Combine(basePath, "Smelter.png");

            if (File.Exists(filePath))
            {
                return new BitmapImage(new Uri(filePath));
            }

            // Если файл не найден, создаем простую текстовую заглушку
            return CreateSimplePlaceholder();
        }

        private BitmapImage CreateSimplePlaceholder()
        {
            var renderTarget = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkGray, null, new Rect(0, 0, Width, Height));
                drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 2), new Rect(0, 0, Width, Height));

                var formattedText = new FormattedText(
                    "SM",
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
            FuelSlot = new InventorySlot { Type = ResourceType.None, Count = 0 };
            InputSlot = new InventorySlot { Type = ResourceType.None, Count = 0 };
            OutputSlot = new InventorySlot { Type = ResourceType.None, Count = 0 };

            // Таймер для переплавки
            smeltingTimer = new DispatcherTimer();
            smeltingTimer.Interval = TimeSpan.FromSeconds(0.1);
            smeltingTimer.Tick += SmeltingTimer_Tick;
        }

        private void SmeltingTimer_Tick(object sender, EventArgs e)
        {
            UpdateSmelting();
        }

        public void Build()
        {
            IsBuilt = true;
            Sprite.Opacity = 1.0;
            smeltingTimer.Start();
        }

        private bool CanSmelt()
        {
            // Проверяем, есть ли топливо и материал для переплавки
            if (FuelSlot.Type == ResourceType.Coal && FuelSlot.Count > 0)
            {
                if (InputSlot.Type == ResourceType.Iron && InputSlot.Count > 0)
                    return true;
                if (InputSlot.Type == ResourceType.Copper && InputSlot.Count > 0)
                    return true;
            }
            return false;
        }

        private void UpdateSmelting()
        {
            if (!CanSmelt() || OutputSlot.Count >= 99)
            {
                isSmelting = false;
                smeltingProgress = 0;
                return;
            }

            if (!isSmelting)
            {
                isSmelting = true;
                smeltingProgress = 0;
            }

            smeltingProgress += 0.1; // Увеличиваем прогресс

            if (smeltingProgress >= smeltingTime)
            {
                // Завершаем переплавку
                FuelSlot.Count--;
                if (FuelSlot.Count <= 0) FuelSlot.Type = ResourceType.None;

                InputSlot.Count--;
                if (InputSlot.Count <= 0) InputSlot.Type = ResourceType.None;

                // Определяем тип слитка на выходе
                ResourceType outputType;
                if (InputSlot.Type == ResourceType.Iron)
                {
                    outputType = ResourceType.IronIngot;
                }
                else if (InputSlot.Type == ResourceType.Copper)
                {
                    outputType = ResourceType.CopperIngot;
                }
                else
                {
                    // Если тип не железо и не медь, сбрасываем прогресс
                    isSmelting = false;
                    smeltingProgress = 0;
                    return;
                }

                if (OutputSlot.Type == ResourceType.None)
                {
                    OutputSlot.Type = outputType;
                    OutputSlot.Count = 1;
                }
                else if (OutputSlot.Type == outputType)
                {
                    OutputSlot.Count++;
                }

                smeltingProgress = 0;
                isSmelting = false;

                UpdateInterface();
            }
        }

        public double GetSmeltingProgress()
        {
            return smeltingProgress / smeltingTime;
        }

        private void OnSmelterClicked(object sender, MouseButtonEventArgs e)
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
                Title = "Плавильня",
                Width = 400,
                Height = 300,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(grid, 0);
            mainGrid.Children.Add(grid);

            // Слот для топлива
            fuelBorder = CreateSlotBorder("Уголь", "Топливо для плавильни\n(только уголь)");
            fuelBorder.MouseDown += (s, e) => HandleSlotClick(FuelSlot, "fuel", e);
            Grid.SetColumn(fuelBorder, 0);
            grid.Children.Add(fuelBorder);

            // Слот для материала
            inputBorder = CreateSlotBorder("Материал", "Сырье для переплавки\n(железо или медь)");
            inputBorder.MouseDown += (s, e) => HandleSlotClick(InputSlot, "input", e);
            Grid.SetColumn(inputBorder, 1);
            grid.Children.Add(inputBorder);

            // Слот для результата
            outputBorder = CreateSlotBorder("Результат", "Готовые слитки\n(железный или медный)");
            outputBorder.MouseDown += (s, e) => HandleSlotClick(OutputSlot, "output", e);
            Grid.SetColumn(outputBorder, 2);
            grid.Children.Add(outputBorder);

            // Прогресс бар
            progressBar = new ProgressBar
            {
                Height = 20,
                Margin = new Thickness(20, 10, 20, 10),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(progressBar, 1);
            Grid.SetColumnSpan(progressBar, 3);
            mainGrid.Children.Add(progressBar);

            // Панель управления
            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                Height = 40
            };
            Grid.SetRow(controlPanel, 2);
            mainGrid.Children.Add(controlPanel);

            // Кнопка "Положить все"
            var putAllButton = new Button
            {
                Content = "Положить всё",
                Margin = new Thickness(5),
                Width = 120,
                Height = 30,
                ToolTip = "Автоматически положить все возможные ресурсы из инвентаря"
            };
            putAllButton.Click += (s, e) => PutAllResources();
            controlPanel.Children.Add(putAllButton);

            // Кнопка "Забрать все"
            var takeAllButton = new Button
            {
                Content = "Забрать всё",
                Margin = new Thickness(5),
                Width = 120,
                Height = 30,
                ToolTip = "Забрать все ресурсы из плавильни в инвентарь"
            };
            takeAllButton.Click += (s, e) => TakeAllResources();
            controlPanel.Children.Add(takeAllButton);

            // Таймер для обновления интерфейса
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

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (progressBar != null)
                progressBar.Value = GetSmeltingProgress() * 100;

            UpdateSlotDisplay(fuelBorder, FuelSlot);
            UpdateSlotDisplay(inputBorder, InputSlot);
            UpdateSlotDisplay(outputBorder, OutputSlot);
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

        private void HandleSlotClick(InventorySlot slot, string slotType, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Левый клик - положить ресурс
                PutResourceToSlot(slot, slotType);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                // Правый клик - забрать ресурс
                TakeResourceFromSlot(slot, slotType);
            }
        }

        private void PutResourceToSlot(InventorySlot slot, string slotType)
        {
            ResourceType resourceToPut = ResourceType.None;
            int amount = 1;

            // Определяем, какой ресурс можно положить в этот слот
            if (slotType == "fuel")
            {
                resourceToPut = ResourceType.Coal;
                if (player.GetResourceCount(ResourceType.Coal) == 0)
                {
                    MessageBox.Show("У вас нет угля в инвентаре!", "Нет ресурсов", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (slotType == "input")
            {
                // Проверяем, есть ли у игрока железо или медь
                if (player.GetResourceCount(ResourceType.Iron) > 0)
                {
                    resourceToPut = ResourceType.Iron;
                }
                else if (player.GetResourceCount(ResourceType.Copper) > 0)
                {
                    resourceToPut = ResourceType.Copper;
                }
                else
                {
                    MessageBox.Show("У вас нет железа или меди в инвентаре!", "Нет ресурсов", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (slotType == "output")
            {
                // В выходной слот нельзя ничего положить
                return;
            }

            // Проверяем, можно ли положить в слот
            if (slot.Type == ResourceType.None)
            {
                // Слот пустой, кладем ресурс
                if (player.RemoveResources(resourceToPut, amount))
                {
                    slot.Type = resourceToPut;
                    slot.Count = amount;
                    UpdateInterface();
                }
            }
            else if (slot.Type == resourceToPut && slot.Count < 99)
            {
                // В слоте уже есть такой ресурс, добавляем
                if (player.RemoveResources(resourceToPut, amount))
                {
                    slot.Count += amount;
                    UpdateInterface();
                }
            }
            else
            {
                MessageBox.Show($"В этот слот нельзя положить этот ресурс!\nТребуется: {slot.Type}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TakeResourceFromSlot(InventorySlot slot, string slotType)
        {
            if (slot.Type == ResourceType.None || slot.Count <= 0)
            {
                return;
            }

            int amount = 1;

            // Забираем ресурс из слота
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

        private void PutAllResources()
        {
            // Автоматически кладем все возможные ресурсы
            PutAllFuel();
            PutAllInput();
            UpdateInterface();
        }

        private void PutAllFuel()
        {
            int coalCount = player.GetResourceCount(ResourceType.Coal);
            if (coalCount <= 0) return;

            int canAdd = Math.Min(coalCount, 99 - FuelSlot.Count);
            if (canAdd > 0 && (FuelSlot.Type == ResourceType.None || FuelSlot.Type == ResourceType.Coal))
            {
                if (player.RemoveResources(ResourceType.Coal, canAdd))
                {
                    if (FuelSlot.Type == ResourceType.None)
                        FuelSlot.Type = ResourceType.Coal;

                    FuelSlot.Count += canAdd;
                }
            }
        }

        private void PutAllInput()
        {
            // Сначала пробуем железо
            int ironCount = player.GetResourceCount(ResourceType.Iron);
            if (ironCount > 0 && (InputSlot.Type == ResourceType.None || InputSlot.Type == ResourceType.Iron))
            {
                int canAdd = Math.Min(ironCount, 99 - InputSlot.Count);
                if (canAdd > 0)
                {
                    if (player.RemoveResources(ResourceType.Iron, canAdd))
                    {
                        if (InputSlot.Type == ResourceType.None)
                            InputSlot.Type = ResourceType.Iron;

                        InputSlot.Count += canAdd;
                        return; // Кладем только один тип ресурса
                    }
                }
            }

            // Потом пробуем медь
            int copperCount = player.GetResourceCount(ResourceType.Copper);
            if (copperCount > 0 && (InputSlot.Type == ResourceType.None || InputSlot.Type == ResourceType.Copper))
            {
                int canAdd = Math.Min(copperCount, 99 - InputSlot.Count);
                if (canAdd > 0)
                {
                    if (player.RemoveResources(ResourceType.Copper, canAdd))
                    {
                        if (InputSlot.Type == ResourceType.None)
                            InputSlot.Type = ResourceType.Copper;

                        InputSlot.Count += canAdd;
                    }
                }
            }
        }

        private void TakeAllResources()
        {
            // Забираем все из всех слотов
            TakeAllFromSlot(FuelSlot);
            TakeAllFromSlot(InputSlot);
            TakeAllFromSlot(OutputSlot);
            UpdateInterface();
        }

        private void TakeAllFromSlot(InventorySlot slot)
        {
            if (slot.Type == ResourceType.None || slot.Count <= 0)
                return;

            // Пытаемся забрать все
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
                    break; // Нет места в инвентаре
                }
            }

            if (slot.Count <= 0)
            {
                slot.Type = ResourceType.None;
                slot.Count = 0;
            }
        }

        private void UpdateSlotDisplay(Border border, InventorySlot slot)
        {
            if (border?.Child is StackPanel stackPanel)
            {
                // Оставляем заголовок, обновляем содержимое
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

            // Если файл не найден, создаем простую текстовую иконку
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
                // Обновляем интерфейс если он открыт
                UpdateSlotDisplay(fuelBorder, FuelSlot);
                UpdateSlotDisplay(inputBorder, InputSlot);
                UpdateSlotDisplay(outputBorder, OutputSlot);

                if (progressBar != null)
                    progressBar.Value = GetSmeltingProgress() * 100;
            }
        }

        public bool AddItem(ResourceType type, int amount, string slotType)
        {
            InventorySlot slot = slotType switch
            {
                "fuel" => FuelSlot,
                "input" => InputSlot,
                "output" => OutputSlot,
                _ => null
            };

            if (slot == null) return false;

            if (slot.Type == ResourceType.None)
            {
                slot.Type = type;
                slot.Count = amount;
                return true;
            }
            else if (slot.Type == type && slot.Count + amount <= 99)
            {
                slot.Count += amount;
                return true;
            }

            return false;
        }

        public bool TakeItem(ResourceType type, int amount, string slotType)
        {
            InventorySlot slot = slotType switch
            {
                "fuel" => FuelSlot,
                "input" => InputSlot,
                "output" => OutputSlot,
                _ => null
            };

            if (slot == null || slot.Type != type || slot.Count < amount)
                return false;

            slot.Count -= amount;
            if (slot.Count <= 0)
                slot.Type = ResourceType.None;

            return true;
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
        }

        public void CloseInterface()
        {
            InterfaceWindow?.Close();
        }

        // Получить тип результата переплавки
        public ResourceType GetOutputType()
        {
            if (InputSlot.Type == ResourceType.Iron)
                return ResourceType.IronIngot;
            else if (InputSlot.Type == ResourceType.Copper)
                return ResourceType.CopperIngot;
            else
                return ResourceType.None;
        }

        // Получить количество топлива
        public int GetFuelCount()
        {
            return FuelSlot.Count;
        }

        // Получить количество сырья
        public int GetInputCount()
        {
            return InputSlot.Count;
        }

        // Получить количество результата
        public int GetOutputCount()
        {
            return OutputSlot.Count;
        }
    }
}