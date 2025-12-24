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
        public double X { get; }
        public double Y { get; }
        public double Width { get; } = 30;
        public double Height { get; } = 30;
        public Direction Direction { get; private set; }
        public bool IsBuilt { get; private set; }

        public object LinkedBuilding { get; set; }
        public bool IsInputConveyor { get; set; }
        public bool IsOutputConveyor { get; set; }

        private ResourceType currentResource = ResourceType.None;
        private double transportProgress = 0;
        private bool isTransporting = false;

        private DispatcherTimer transportTimer;
        private DispatcherTimer animationTimer;
        private List<BitmapImage> animationFrames = new List<BitmapImage>();
        private int frameIndex;

        private Image resourceSprite;
        private const double TransportSpeed = 0.08;

        private ResourceType bufferedResource = ResourceType.None;

        //Инициализация
        public Conveyor(double x, double y, Direction direction)
        {
            X = x;
            Y = y;
            Direction = direction;

            InitializeSprite();
            InitializeAnimation();
            InitializeTransport();
        }

        private void InitializeSprite()
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

        private void InitializeAnimation()
        {
            LoadAnimationFrames();

            animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            animationTimer.Tick += (_, __) =>
            {
                frameIndex = (frameIndex + 1) % animationFrames.Count;
                Sprite.Source = animationFrames[frameIndex];
            };
        }

        private void InitializeTransport()
        {
            transportTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            transportTimer.Tick += (_, __) => UpdateTransport();

            resourceSprite = new Image
            {
                Width = 20,
                Height = 20,
                Visibility = Visibility.Collapsed
            };
        }

        //Логика
        public void Build()
        {
            IsBuilt = true;
            animationTimer.Start();
            transportTimer.Start();
        }

        public bool TryReceiveResource(ResourceType resource)
        {
            if (isTransporting || bufferedResource != ResourceType.None)
                return false;

            bufferedResource = resource;
            return true;
        }

        private void UpdateTransport()
        {
            if (isTransporting)
            {
                MoveResource();
                return;
            }

            if (bufferedResource != ResourceType.None && !isTransporting)
            {
                StartTransport(bufferedResource);
                bufferedResource = ResourceType.None;
                return;
            }
            if (IsOutputConveyor && LinkedBuilding != null && !isTransporting)
            {
                TryTakeFromBuilding();
            }
        }

        private void StartTransport(ResourceType resource)
        {
            isTransporting = true;
            currentResource = resource;
            transportProgress = 0;

            resourceSprite.Source = LoadResourceIcon(resource);
            resourceSprite.Visibility = Visibility.Visible;
            UpdateResourcePosition();
        }

        private void MoveResource()
        {
            transportProgress += TransportSpeed;
            UpdateResourcePosition();

            if (transportProgress >= 1.0)
            {
                CompleteTransport();
            }
        }

        private void CompleteTransport()
        {
            if (IsInputConveyor && LinkedBuilding != null)
            {
                if (TryDeliverToBuilding(currentResource))
                {
                    ResetTransport();
                    return;
                }
            }
            Conveyor nextConveyor = FindNextConveyorInDirection();

            if (nextConveyor != null)
            {
                if (nextConveyor.TryReceiveResource(currentResource))
                {
                    ResetTransport();
                    return;
                }
                else
                {
                    transportProgress = 0.99;
                    return;
                }
            }
            ResetTransport();
        }

        private void ResetTransport()
        {
            isTransporting = false;
            currentResource = ResourceType.None;
            transportProgress = 0;
            resourceSprite.Visibility = Visibility.Collapsed;
        }

        //Взаимодействие с зданиями
        private void TryTakeFromBuilding()
        {
            if (LinkedBuilding is Miner miner && miner.OutputSlot.Count > 0)
            {
                var resource = miner.OutputSlot.Type;
                miner.OutputSlot.Count--;
                if (miner.OutputSlot.Count == 0)
                    miner.OutputSlot.Type = ResourceType.None;

                StartTransport(resource);
            }
            else if (LinkedBuilding is Smelter smelter && smelter.OutputSlot.Count > 0)
            {
                var resource = smelter.OutputSlot.Type;
                smelter.OutputSlot.Count--;
                if (smelter.OutputSlot.Count == 0)
                    smelter.OutputSlot.Type = ResourceType.None;

                StartTransport(resource);
            }
            else if (LinkedBuilding is ArmsFactory armsFactory && armsFactory.OutputSlot.Count > 0)
            {
                var resource = armsFactory.OutputSlot.Type;
                armsFactory.OutputSlot.Count--;
                if (armsFactory.OutputSlot.Count == 0)
                    armsFactory.OutputSlot.Type = ResourceType.None;

                StartTransport(resource);
            }
        }

        private bool TryDeliverToBuilding(ResourceType resource)
        {
            if (LinkedBuilding is Smelter smelter)
            {
                if (resource == ResourceType.Coal)
                {
                    if (smelter.FuelSlot.Type == ResourceType.None ||
                        smelter.FuelSlot.Type == ResourceType.Coal)
                    {
                        smelter.FuelSlot.Type = ResourceType.Coal;
                        smelter.FuelSlot.Count++;
                        return true;
                    }
                }
                else if (resource == ResourceType.Iron || resource == ResourceType.Copper)
                {
                    if (smelter.InputSlot.Type == ResourceType.None ||
                        smelter.InputSlot.Type == resource)
                    {
                        smelter.InputSlot.Type = resource;
                        smelter.InputSlot.Count++;
                        return true;
                    }
                }
            }
            else if (LinkedBuilding is ArmsFactory armsFactory)
            {
                if (resource == ResourceType.Coal)
                {
                    if (armsFactory.FuelSlot.Type == ResourceType.None ||
                        armsFactory.FuelSlot.Type == ResourceType.Coal)
                    {
                        armsFactory.FuelSlot.Type = ResourceType.Coal;
                        armsFactory.FuelSlot.Count++;
                        return true;
                    }
                }
                else if (resource == ResourceType.IronIngot || resource == ResourceType.CopperIngot)
                {
                    if (armsFactory.InputSlot.Type == ResourceType.None ||
                        armsFactory.InputSlot.Type == resource)
                    {
                        armsFactory.InputSlot.Type = resource;
                        armsFactory.InputSlot.Count++;
                        return true;
                    }
                }
            }

            return false;
        }

        //С другими конвейерами
        private Conveyor FindNextConveyorInDirection()
        {
            double nextX = X;
            double nextY = Y;

            switch (Direction)
            {
                case Direction.Right: nextX += Width; break;
                case Direction.Left: nextX -= Width; break;
                case Direction.Down: nextY += Height; break;
                case Direction.Up: nextY -= Height; break;
            }

            return GameCanvasHelper.FindConveyorAtPosition(nextX, nextY);
        }

        public List<Conveyor> GetAdjacentConveyors(List<Conveyor> allConveyors)
        {
            List<Conveyor> result = new List<Conveyor>();

            foreach (var conveyor in allConveyors)
            {
                if (conveyor == this) continue;

                double dx = Math.Abs(X - conveyor.X);
                double dy = Math.Abs(Y - conveyor.Y);

                if ((dx == Width && dy == 0) || (dy == Height && dx == 0))
                {
                    result.Add(conveyor);
                }
            }

            return result;
        }

        //Визуализация
        private void UpdateResourcePosition()
        {
            double px = X;
            double py = Y;

            switch (Direction)
            {
                case Direction.Right:
                    px += Width * transportProgress;
                    py += Height / 2 - 10;
                    break;
                case Direction.Left:
                    px += Width - Width * transportProgress;
                    py += Height / 2 - 10;
                    break;
                case Direction.Down:
                    px += Width / 2 - 10;
                    py += Height * transportProgress;
                    break;
                case Direction.Up:
                    px += Width / 2 - 10;
                    py += Height - Height * transportProgress;
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

        public void RemoveFromCanvas(Canvas canvas)
        {
            canvas.Children.Remove(Sprite);
            canvas.Children.Remove(resourceSprite);
        }

        private BitmapImage LoadConveyorTexture(int frame)
        {
            string path = $@"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\conveyor\{Direction.ToString().ToLower()}_{frame + 1}.png";
            return File.Exists(path) ? new BitmapImage(new Uri(path)) : new BitmapImage();
        }

        private void LoadAnimationFrames()
        {
            animationFrames.Clear();
            animationFrames.Add(LoadConveyorTexture(0));
            animationFrames.Add(LoadConveyorTexture(1));
        }

        private BitmapImage LoadResourceIcon(ResourceType type)
        {
            string fileName = type.ToString().ToLower();

            if (type == ResourceType.IronIngot)
                fileName = "iron_ingot";
            else if (type == ResourceType.CopperIngot)
                fileName = "copper_ingot";

            string path = $@"C:\Users\Михаил\Desktop\Game\Factorio\Factorio\textures\Resources\{fileName}.png";
            return File.Exists(path) ? new BitmapImage(new Uri(path)) : new BitmapImage();
        }

        //Доп
        public bool IsPointInside(Point point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }

        public bool IsNextInDirection(Conveyor other)
        {
            switch (Direction)
            {
                case Direction.Right: return other.X == X + Width && other.Y == Y;
                case Direction.Left: return other.X == X - Width && other.Y == Y;
                case Direction.Down: return other.Y == Y + Height && other.X == X;
                case Direction.Up: return other.Y == Y - Height && other.X == X;
                default: return false;
            }
        }
    }

    public static class GameCanvasHelper
    {
        public static List<Conveyor> AllConveyors { get; set; } = new List<Conveyor>();

        public static Conveyor FindConveyorAtPosition(double x, double y)
        {
            foreach (var conveyor in AllConveyors)
            {
                if (conveyor.IsBuilt &&
                    Math.Abs(conveyor.X - x) < 5 &&
                    Math.Abs(conveyor.Y - y) < 5)
                {
                    return conveyor;
                }
            }
            return null;
        }
    }
}