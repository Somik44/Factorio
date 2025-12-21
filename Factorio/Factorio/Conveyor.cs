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
        // =========================
        // PUBLIC API (старый код)
        // =========================

        public Image Sprite { get; private set; }

        public double X { get; }
        public double Y { get; }
        public double Width { get; } = 30;
        public double Height { get; } = 30;

        public Direction Direction { get; private set; }

        public bool IsBuilt { get; private set; }
        public bool IsActive { get; private set; }

        public Conveyor NextConveyor { get; set; }
        public Conveyor PreviousConveyor { get; set; }

        // Старые поля — оставлены для совместимости
        public object SourceBuilding { get; set; }
        public object TargetBuilding { get; set; }

        // =========================
        // INTERNAL STATE
        // =========================

        private readonly Queue<ResourceType> buffer = new Queue<ResourceType>();
        private const int MaxBufferSize = 3;

        private bool isTransporting;
        private ResourceType currentResource;
        private double progress;

        private DispatcherTimer transportTimer;
        private DispatcherTimer animationTimer;

        private List<BitmapImage> animationFrames = new List<BitmapImage>();
        private int frameIndex;

        private Image resourceSprite;

        private const double TransportSpeed = 0.05;

        // =========================
        // CONSTRUCTOR
        // =========================

        public Conveyor(double x, double y, Direction direction)
        {
            X = x;
            Y = y;
            Direction = direction;

            InitSprite();
            InitAnimation();
        }

        // =========================
        // INIT
        // =========================

        private void InitSprite()
        {
            Sprite = new Image
            {
                Width = Width,
                Height = Height,
                Stretch = Stretch.Uniform,
                Source = LoadConveyorTexture(0)
            };

            Canvas.SetLeft(Sprite, X);
            Canvas.SetTop(Sprite, Y);
        }

        private void InitAnimation()
        {
            LoadAnimationFrames();

            animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            animationTimer.Tick += (_, __) =>
            {
                if (!IsActive) return;
                frameIndex = (frameIndex + 1) % animationFrames.Count;
                Sprite.Source = animationFrames[frameIndex];
            };

            transportTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            transportTimer.Tick += (_, __) => UpdateTransport();

            resourceSprite = new Image
            {
                Width = 20,
                Height = 20,
                Visibility = Visibility.Collapsed
            };
        }

        // =========================
        // BUILD / CONNECTION
        // =========================

        public void Build()
        {
            IsBuilt = true;
            UpdateActivity();
        }

        public void SetNextConveyor(Conveyor next)
        {
            NextConveyor = next;

            if (next != null && next.PreviousConveyor != this)
                next.PreviousConveyor = this;

            UpdateActivity();
            next?.UpdateActivity();
        }

        private void UpdateActivity()
        {
            bool wasActive = IsActive;
            IsActive = IsBuilt && (PreviousConveyor != null || SourceBuilding != null);

            if (IsActive && !wasActive)
            {
                animationTimer.Start();
                transportTimer.Start();
            }
            else if (!IsActive && wasActive)
            {
                animationTimer.Stop();
                transportTimer.Stop();
                ResetTransport();
            }
        }

        // =========================
        // TRANSPORT LOGIC
        // =========================

        private void UpdateTransport()
        {
            if (!IsActive) return;

            // Если ресурс едет
            if (isTransporting)
            {
                progress += TransportSpeed;
                UpdateResourcePosition();

                if (progress >= 1.0)
                    CompleteTransport();

                return;
            }

            // 1️⃣ Из своего буфера
            if (buffer.Count > 0)
            {
                StartTransport(buffer.Dequeue());
                return;
            }

            // 2️⃣ От предыдущего конвейера
            if (PreviousConveyor != null && PreviousConveyor.CanGive())
            {
                buffer.Enqueue(PreviousConveyor.Give());
                return;
            }

            // 3️⃣ 🔥 ИЗ ИСТОЧНИКА (ЭТОГО НЕ БЫЛО)
            if (SourceBuilding != null)
            {
                ResourceType res = TryGetFromSource(SourceBuilding);
                if (res != ResourceType.None)
                {
                    buffer.Enqueue(res);
                }
            }
        }

        private ResourceType TryGetFromSource(object source)
        {
            // ДОБЫТЧИК
            if (source is Miner miner)
            {
                if (miner.OutputSlot.Type != ResourceType.None &&
                    miner.OutputSlot.Count > 0)
                {
                    miner.OutputSlot.Count--;
                    var type = miner.OutputSlot.Type;
                    if (miner.OutputSlot.Count == 0)
                        miner.OutputSlot.Type = ResourceType.None;

                    return type;
                }
            }

            // ПЛАВИЛЬНЯ (выход)
            if (source is Smelter smelter)
            {
                if (smelter.OutputSlot.Type != ResourceType.None &&
                    smelter.OutputSlot.Count > 0)
                {
                    smelter.OutputSlot.Count--;
                    var type = smelter.OutputSlot.Type;
                    if (smelter.OutputSlot.Count == 0)
                        smelter.OutputSlot.Type = ResourceType.None;

                    return type;
                }
            }

            // ОРУЖЕЙНЫЙ ЗАВОД (выход) - ДОБАВИТЬ
            if (source is ArmsFactory armsFactory)
            {
                if (armsFactory.OutputSlot.Type != ResourceType.None &&
                    armsFactory.OutputSlot.Count > 0)
                {
                    armsFactory.OutputSlot.Count--;
                    var type = armsFactory.OutputSlot.Type;
                    if (armsFactory.OutputSlot.Count == 0)
                        armsFactory.OutputSlot.Type = ResourceType.None;

                    return type;
                }
            }

            return ResourceType.None;
        }



        private void StartTransport(ResourceType res)
        {
            isTransporting = true;
            currentResource = res;
            progress = 0;

            resourceSprite.Source = LoadResourceIcon(res);
            resourceSprite.Visibility = Visibility.Visible;

            UpdateResourcePosition();
        }

        private void CompleteTransport()
        {
            // 1️⃣ Передаём дальше по ленте
            if (NextConveyor != null && NextConveyor.Receive(currentResource))
            {
                ResetTransport();
                return;
            }

            // 2️⃣ Пытаемся отдать в здание (старый TargetBuilding)
            if (TargetBuilding != null && TryDeliverToTarget(TargetBuilding, currentResource))
            {
                ResetTransport();
                return;
            }

            // 3️⃣ Возвращаем в буфер
            if (buffer.Count < MaxBufferSize)
                buffer.Enqueue(currentResource);

            ResetTransport();
        }

        private void ResetTransport()
        {
            isTransporting = false;
            currentResource = ResourceType.None;
            progress = 0;
            resourceSprite.Visibility = Visibility.Collapsed;
        }

        // =========================
        // BUFFER API
        // =========================

        public bool CanGive() => buffer.Count > 0 && !isTransporting;

        public ResourceType Give()
        {
            return buffer.Count > 0 ? buffer.Dequeue() : ResourceType.None;
        }

        public bool Receive(ResourceType res)
        {
            if (buffer.Count >= MaxBufferSize) return false;
            buffer.Enqueue(res);
            return true;
        }

        // =========================
        // DELIVERY
        // =========================

        private bool TryDeliverToTarget(object target, ResourceType res)
        {
            if (target is Smelter smelter)
            {
                if (res == ResourceType.Coal &&
                    (smelter.FuelSlot.Type == ResourceType.None || smelter.FuelSlot.Type == ResourceType.Coal))
                {
                    smelter.FuelSlot.Type = ResourceType.Coal;
                    smelter.FuelSlot.Count++;
                    return true;
                }

                if ((res == ResourceType.Iron || res == ResourceType.Copper) &&
                    (smelter.InputSlot.Type == ResourceType.None || smelter.InputSlot.Type == res))
                {
                    smelter.InputSlot.Type = res;
                    smelter.InputSlot.Count++;
                    return true;
                }
            }
            else if (target is ArmsFactory armsFactory)  // ДОБАВИТЬ ЭТО
            {
                // Уголь в топливный слот
                if (res == ResourceType.Coal &&
                    (armsFactory.FuelSlot.Type == ResourceType.None || armsFactory.FuelSlot.Type == ResourceType.Coal))
                {
                    armsFactory.FuelSlot.Type = ResourceType.Coal;
                    armsFactory.FuelSlot.Count++;
                    return true;
                }

                // Железные или медные слитки в входной слот
                if ((res == ResourceType.IronIngot || res == ResourceType.CopperIngot) &&
                    (armsFactory.InputSlot.Type == ResourceType.None || armsFactory.InputSlot.Type == res))
                {
                    armsFactory.InputSlot.Type = res;
                    armsFactory.InputSlot.Count++;
                    return true;
                }
            }

            return false;
        }


        // =========================
        // REQUIRED BY MainWindow
        // =========================

        public void RemoveFromCanvas(Canvas canvas)
        {
            canvas.Children.Remove(Sprite);
            canvas.Children.Remove(resourceSprite);
        }

        public List<Conveyor> GetAdjacentConveyors()
        {
            return new List<Conveyor>();
        }

        public bool IsNextDirection(Direction dir)
        {
            return Direction == dir;
        }

        // =========================
        // VISUALS
        // =========================

        private void UpdateResourcePosition()
        {
            double px = X;
            double py = Y;

            switch (Direction)
            {
                case Direction.Right:
                    px += Width * progress;
                    py += Height / 2 - 10;
                    break;
                case Direction.Left:
                    px += Width - Width * progress;
                    py += Height / 2 - 10;
                    break;
                case Direction.Down:
                    px += Width / 2 - 10;
                    py += Height * progress;
                    break;
                case Direction.Up:
                    px += Width / 2 - 10;
                    py += Height - Height * progress;
                    break;
            }

            Canvas.SetLeft(resourceSprite, px);
            Canvas.SetTop(resourceSprite, py);
        }

        public void AddToCanvas(Canvas canvas)
        {
            canvas.Children.Add(Sprite);
            canvas.Children.Add(resourceSprite);
            Canvas.SetZIndex(Sprite, 30);
            Canvas.SetZIndex(resourceSprite, 31);
        }

        // =========================
        // ASSETS
        // =========================

        private BitmapImage LoadConveyorTexture(int frame)
        {
            string path =
                $@"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\conveyor\{Direction.ToString().ToLower()}_{frame + 1}.png";

            return File.Exists(path)
                ? new BitmapImage(new Uri(path))
                : new BitmapImage();
        }

        private void LoadAnimationFrames()
        {
            animationFrames.Clear();
            animationFrames.Add(LoadConveyorTexture(0));
            animationFrames.Add(LoadConveyorTexture(1));
        }

        public bool IsNextInDirection(Conveyor other)
        {
            // other должен быть ровно в направлении, куда "смотрит" этот конвейер

            switch (Direction)
            {
                case Direction.Right:
                    return other.X == X + Width && other.Y == Y;

                case Direction.Left:
                    return other.X == X - Width && other.Y == Y;

                case Direction.Down:
                    return other.Y == Y + Height && other.X == X;

                case Direction.Up:
                    return other.Y == Y - Height && other.X == X;

                default:
                    return false;
            }
        }

        public List<Conveyor> GetAdjacentConveyors(List<Conveyor> allConveyors)
        {
            List<Conveyor> result = new List<Conveyor>();

            foreach (var c in allConveyors)
            {
                if (c == this) continue;

                double dx = Math.Abs(X - c.X);
                double dy = Math.Abs(Y - c.Y);

                // строго соседние клетки
                if ((dx == Width && dy == 0) || (dy == Height && dx == 0))
                {
                    result.Add(c);
                }
            }

            return result;
        }


        private BitmapImage LoadResourceIcon(ResourceType t)
        {
            string path =
                $@"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Resources\{t.ToString().ToLower()}.png";

            return File.Exists(path)
                ? new BitmapImage(new Uri(path))
                : new BitmapImage();
        }
    }
}
