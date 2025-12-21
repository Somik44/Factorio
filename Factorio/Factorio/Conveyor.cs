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

        // Вместо текущей логики в UpdateTransport:
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

            // 1️⃣ Получаем ресурс ТОЛЬКО от предыдущего конвейера (если он есть)
            if (PreviousConveyor != null && PreviousConveyor.CanGive() && IsPreviousInCorrectDirection())
            {
                buffer.Enqueue(PreviousConveyor.Give());
                return;
            }

            // 2️⃣ Получаем ресурс ИЗ ИСТОЧНИКА (если он есть и находится с правильной стороны)
            if (SourceBuilding != null && IsSourceInInputDirection())
            {
                ResourceType res = TryGetFromSource(SourceBuilding);
                if (res != ResourceType.None)
                {
                    buffer.Enqueue(res);
                    return;
                }
            }

            // 3️⃣ Или из своего буфера
            if (buffer.Count > 0)
            {
                StartTransport(buffer.Dequeue());
            }
        }

        // Проверяем, находится ли предыдущий конвейер в правильном направлении
        private bool IsPreviousInCorrectDirection()
        {
            if (PreviousConveyor == null) return false;

            // Проверяем, что предыдущий конвейер "смотрит" на этот
            return PreviousConveyor.Direction == GetDirectionTo(this);
        }

        // Получаем направление от данного конвейера к целевому
        private Direction GetDirectionTo(Conveyor target)
        {
            double dx = target.X - this.X;
            double dy = target.Y - this.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
                return dx > 0 ? Direction.Right : Direction.Left;
            else
                return dy > 0 ? Direction.Down : Direction.Up;
        }

        // Проверяем, находится ли источник с входной стороны
        private bool IsSourceInInputDirection()
        {
            if (SourceBuilding == null) return false;

            Point sourceCenter = GetBuildingCenter(SourceBuilding);
            Point conveyorCenter = new Point(X + Width / 2, Y + Height / 2);

            // Проверяем, что источник находится с "обратной" стороны от направления
            switch (Direction)
            {
                case Direction.Right:  // Движение вправо → источник должен быть слева
                    return sourceCenter.X < conveyorCenter.X;
                case Direction.Left:   // Движение влево ← источник должен быть справа
                    return sourceCenter.X > conveyorCenter.X;
                case Direction.Down:   // Движение вниз ↓ источник должен быть сверху
                    return sourceCenter.Y < conveyorCenter.Y;
                case Direction.Up:     // Движение вверх ↑ источник должен быть снизу
                    return sourceCenter.Y > conveyorCenter.Y;
                default:
                    return false;
            }
        }

        // Аналогично для цели
        private bool IsTargetInOutputDirection()
        {
            if (TargetBuilding == null) return false;

            Point targetCenter = GetBuildingCenter(TargetBuilding);
            Point conveyorCenter = new Point(X + Width / 2, Y + Height / 2);

            // Проверяем, что цель находится с "лицевой" стороны от направления
            switch (Direction)
            {
                case Direction.Right:  // Движение вправо → цель должна быть справа
                    return targetCenter.X > conveyorCenter.X;
                case Direction.Left:   // Движение влево ← цель должна быть слева
                    return targetCenter.X < conveyorCenter.X;
                case Direction.Down:   // Движение вниз ↓ цель должна быть снизу
                    return targetCenter.Y > conveyorCenter.Y;
                case Direction.Up:     // Движение вверх ↑ цель должна быть сверху
                    return targetCenter.Y < conveyorCenter.Y;
                default:
                    return false;
            }
        }

        // Метод для получения центра здания (нужен для проверок направления)
        private Point GetBuildingCenter(object building)
        {
            if (building is Miner miner)
            {
                return new Point(miner.X + miner.Width / 2, miner.Y + miner.Height / 2);
            }
            else if (building is Smelter smelter)
            {
                return new Point(smelter.X + smelter.Width / 2, smelter.Y + smelter.Height / 2);
            }
            else if (building is ArmsFactory armsFactory)
            {
                return new Point(armsFactory.X + armsFactory.Width / 2, armsFactory.Y + armsFactory.Height / 2);
            }

            return new Point(0, 0);
        }

        // В CompleteTransport проверяем направление цели
        private void CompleteTransport()
        {
            // 1️⃣ Передаём дальше по ленте (если следующий конвейер в правильном направлении)
            if (NextConveyor != null && IsNextInCorrectDirection() && NextConveyor.Receive(currentResource))
            {
                ResetTransport();
                return;
            }

            // 2️⃣ Пытаемся отдать в здание (если оно в правильном направлении)
            if (TargetBuilding != null && IsTargetInOutputDirection() &&
                TryDeliverToTarget(TargetBuilding, currentResource))
            {
                ResetTransport();
                return;
            }

            // 3️⃣ Возвращаем в буфер (если не смогли передать дальше)
            if (buffer.Count < MaxBufferSize)
                buffer.Enqueue(currentResource);

            ResetTransport();
        }

        // Проверяем, находится ли следующий конвейер в правильном направлении
        private bool IsNextInCorrectDirection()
        {
            if (NextConveyor == null) return false;

            // Проверяем, что этот конвейер "смотрит" на следующий
            return this.Direction == GetDirectionTo(NextConveyor);
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
