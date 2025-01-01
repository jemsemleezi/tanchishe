using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SnakeGame
{
    public partial class MainWindow : Window
    {
        private const int GridSize = 64;
        private const int SnakeSquareSize = 10; // 每个网格的大小
        private const int SnakeStartLength = 5;
        private const int InitialSnakeSpeed = 500; // 2个网格每秒
        private const int MinSnakeSpeed = 50; // 20个网格每秒
        private const int MaxSnakeSpeed = 500; // 2个网格每秒

        private enum SnakeDirection { Left, Right, Up, Down };
        private enum FoodType { Score, SpeedUp, SpeedDown, LengthUp, LengthDown };
        private SnakeDirection _snakeDirection;
        private List<Point> _snakeParts = new List<Point>();
        private List<(Point Position, FoodType Type)> _foods = new List<(Point, FoodType)>();
        private DispatcherTimer _gameTickTimer = new DispatcherTimer();
        private DispatcherTimer _foodSpawnTimer = new DispatcherTimer();
        private int _score = 0;
        private int _level = 1;
        private bool _isPaused = false;

        private MediaPlayer _eatSoundPlayer = new MediaPlayer();
        private MediaPlayer _backgroundMusicPlayer = new MediaPlayer();

        public MainWindow()
        {
            InitializeComponent();
            _gameTickTimer.Tick += GameTickTimer_Tick;
            _foodSpawnTimer.Tick += FoodSpawnTimer_Tick;
            this.Loaded += MainWindow_Loaded;
            this.KeyDown += Window_KeyDown;

            // 设置窗口图标
            this.Icon = new BitmapImage(new Uri("pack://application:,,,/R-C.ico"));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartNewGame();
        }

        private void StartNewGame()
        {
            _snakeParts.Clear();
            _foods.Clear();
            _score = 0;
            _level = 1;
            _isPaused = false;
            _gameTickTimer.Interval = TimeSpan.FromMilliseconds(InitialSnakeSpeed);
            UpdateScore();
            UpdateLevel();

            // 将蛇的位置设置在画布的中心位置
            double startX = (GridSize / 2) - (SnakeStartLength / 2);
            double startY = GridSize / 2;

            for (int i = 0; i < SnakeStartLength; i++)
            {
                _snakeParts.Add(new Point(startX + i, startY));
            }

            // 设置蛇头的初始方向为蛇身第一节的反方向
            SetInitialSnakeDirection();

            // 生成随机位置的食物
            SpawnFoods();

            DrawGame();
            _gameTickTimer.Start();
            _foodSpawnTimer.Interval = TimeSpan.FromSeconds(15);
            _foodSpawnTimer.Start();

            // Play background music
            _backgroundMusicPlayer.Open(new Uri("pack://application:,,,/SnakeGame;component/background.mp3"));
            _backgroundMusicPlayer.MediaEnded += (s, e) => _backgroundMusicPlayer.Position = TimeSpan.Zero;
            _backgroundMusicPlayer.Volume = 0.5; // 设置背景音乐音量
            _backgroundMusicPlayer.Play();
        }

        private void SetInitialSnakeDirection()
        {
            Point head = _snakeParts[0];
            Point neck = _snakeParts[1];
            if (head.X > neck.X)
                _snakeDirection = SnakeDirection.Right;
            else if (head.X < neck.X)
                _snakeDirection = SnakeDirection.Left;
            else if (head.Y > neck.Y)
                _snakeDirection = SnakeDirection.Down;
            else if (head.Y < neck.Y)
                _snakeDirection = SnakeDirection.Up;
        }

        private void GameTickTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPaused)
            {
                MoveSnake();
            }
        }

        private void FoodSpawnTimer_Tick(object sender, EventArgs e)
        {
            SpawnFoods();
            DrawGame();
        }

        private void MoveSnake()
        {
            Point tail = _snakeParts[_snakeParts.Count - 1]; // 记录蛇尾的位置

            for (int i = _snakeParts.Count - 1; i > 0; i--)
            {
                _snakeParts[i] = _snakeParts[i - 1];
            }

            Point head = _snakeParts[0];
            switch (_snakeDirection)
            {
                case SnakeDirection.Left:
                    head.X -= 1;
                    break;
                case SnakeDirection.Right:
                    head.X += 1;
                    break;
                case SnakeDirection.Up:
                    head.Y -= 1;
                    break;
                case SnakeDirection.Down:
                    head.Y += 1;
                    break;
            }
            _snakeParts[0] = head;

            for (int i = 0; i < _foods.Count; i++)
            {
                if (head == _foods[i].Position)
                {
                    HandleFoodEffect(_foods[i].Type, tail);
                    _foods.RemoveAt(i);
                    break;
                }
            }

            if (CheckCollision())
            {
                EndGame();
                return;
            }

            DrawGame();
        }

        private void HandleFoodEffect(FoodType foodType, Point tail)
        {
            switch (foodType)
            {
                case FoodType.Score:
                    _score += 10; // 增加积分
                    _gameTickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(MinSnakeSpeed, _gameTickTimer.Interval.TotalMilliseconds - 50));
                    _snakeParts.Add(tail);
                    UpdateScore();
                    CheckLevelUp();
                    break;
                case FoodType.SpeedUp:
                    _gameTickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(MinSnakeSpeed, _gameTickTimer.Interval.TotalMilliseconds - 50));
                    break;
                case FoodType.SpeedDown:
                    _gameTickTimer.Interval = TimeSpan.FromMilliseconds(Math.Min(MaxSnakeSpeed, _gameTickTimer.Interval.TotalMilliseconds + 50));
                    break;
                case FoodType.LengthUp:
                    _snakeParts.Add(tail);
                    break;
                case FoodType.LengthDown:
                    if (_snakeParts.Count > 5)
                    {
                        _snakeParts.RemoveAt(_snakeParts.Count - 1);
                    }
                    break;
            }

            // Play eat sound
            _eatSoundPlayer.Open(new Uri("pack://application:,,,/SnakeGame;component/eat.wav"));
            _eatSoundPlayer.Volume = 1.0; // 设置吃食物音效音量
            _eatSoundPlayer.Play();
        }

        private void DrawGame()
        {
            GameCanvas.Children.Clear();

            // 绘制网格线
            for (int i = 0; i <= GridSize; i++)
            {
                Line line = new Line
                {
                    Stroke = Brushes.Gray,
                    X1 = i * SnakeSquareSize,
                    Y1 = 0,
                    X2 = i * SnakeSquareSize,
                    Y2 = GridSize * SnakeSquareSize
                };
                GameCanvas.Children.Add(line);

                line = new Line
                {
                    Stroke = Brushes.Gray,
                    X1 = 0,
                    Y1 = i * SnakeSquareSize,
                    X2 = GridSize * SnakeSquareSize,
                    Y2 = i * SnakeSquareSize
                };
                GameCanvas.Children.Add(line);
            }

            // 绘制边界
            Rectangle border = new Rectangle
            {
                Stroke = Brushes.White,
                Width = GridSize * SnakeSquareSize,
                Height = GridSize * SnakeSquareSize
            };
            Canvas.SetTop(border, 0);
            Canvas.SetLeft(border, 0);
            GameCanvas.Children.Add(border);

            // 绘制蛇
            for (int i = 0; i < _snakeParts.Count; i++)
            {
                Rectangle rect = new Rectangle
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = i == 0 ? Brushes.Yellow : Brushes.Green // 区分蛇头和蛇身
                };
                Canvas.SetTop(rect, _snakeParts[i].Y * SnakeSquareSize);
                Canvas.SetLeft(rect, _snakeParts[i].X * SnakeSquareSize);
                GameCanvas.Children.Add(rect);
            }

            // 重新绘制食物
            foreach (var food in _foods)
            {
                Rectangle foodRect = new Rectangle
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = GetFoodColor(food.Type)
                };
                Canvas.SetTop(foodRect, food.Position.Y * SnakeSquareSize);
                Canvas.SetLeft(foodRect, food.Position.X * SnakeSquareSize);
                GameCanvas.Children.Add(foodRect);
            }

            // 绘制食物颜色和功能对应关系
            DrawFoodLegend();
        }

        private void DrawFoodLegend()
        {
            Dictionary<FoodType, string> foodDescriptions = new Dictionary<FoodType, string>
            {
                { FoodType.Score, "Score，Speed Up，Length Up" },
                { FoodType.SpeedUp, "Speed Up" },
                { FoodType.SpeedDown, "Speed Down" },
                { FoodType.LengthUp, "Length Up" },
                { FoodType.LengthDown, "Length Down" }
            };

            int legendY = 10;
            foreach (var food in foodDescriptions)
            {
                Rectangle legendRect = new Rectangle
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = GetFoodColor(food.Key)
                };
                Canvas.SetTop(legendRect, legendY);
                Canvas.SetLeft(legendRect, GridSize * SnakeSquareSize + 10);
                GameCanvas.Children.Add(legendRect);

                TextBlock legendText = new TextBlock
                {
                    Text = food.Value,
                    Foreground = Brushes.White,
                    FontSize = 12
                };
                Canvas.SetTop(legendText, legendY);
                Canvas.SetLeft(legendText, GridSize * SnakeSquareSize + 30);
                GameCanvas.Children.Add(legendText);

                legendY += 20;
            }
        }

        private Brush GetFoodColor(FoodType foodType)
        {
            switch (foodType)
            {
                case FoodType.Score:
                    return Brushes.Red;
                case FoodType.SpeedUp:
                    return Brushes.Blue;
                case FoodType.SpeedDown:
                    return Brushes.Purple;
                case FoodType.LengthUp:
                    return Brushes.Orange;
                case FoodType.LengthDown:
                    return Brushes.Brown;
                default:
                    return Brushes.Red;
            }
        }

        private void SpawnFoods()
        {
            Random rand = new Random();
            int foodCount = rand.Next(5, 16); // 0到10之间的随机数

            _foods.Clear();
            for (int i = 0; i < foodCount; i++)
            {
                Point foodPosition;
                do
                {
                    foodPosition = new Point(rand.Next(0, GridSize), rand.Next(0, GridSize));
                } while (_snakeParts.Contains(foodPosition) || _foods.Exists(f => f.Position == foodPosition));

                FoodType foodType;
                int foodTypeChance = rand.Next(0, 100);
                if (foodTypeChance < 60)
                {
                    foodType = FoodType.Score;
                }
                else if (foodTypeChance < 70)
                {
                    foodType = FoodType.SpeedUp;
                }
                else if (foodTypeChance < 80)
                {
                    foodType = FoodType.SpeedDown;
                }
                else if (foodTypeChance < 90)
                {
                    foodType = FoodType.LengthUp;
                }
                else
                {
                    foodType = FoodType.LengthDown;
                }

                _foods.Add((foodPosition, foodType));
            }
        }

        private void UpdateScore()
        {
            ScoreTextBlock.Text = $"Score: {_score}";
        }

        private void UpdateLevel()
        {
            LevelTextBlock.Text = $"Level: {_level}";
        }

        private bool CheckCollision()
        {
            Point head = _snakeParts[0];

            // Check if snake hits the wall
            if (head.X < 0 || head.X >= GridSize || head.Y < 0 || head.Y >= GridSize)
            {
                return true;
            }

            // Check if snake hits itself (excluding the head)
            for (int i = 1; i < _snakeParts.Count; i++)
            {
                if (head == _snakeParts[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void CheckLevelUp()
        {
            if (_score >= 50)
            {
                _level++;
                _score = 0; // 重置得分
                UpdateLevel();
                UpdateScore();
                _gameTickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(MinSnakeSpeed, InitialSnakeSpeed - (_level * 50)));
                SpawnFoods(); // 刷新食物位置
                SetInitialSnakeDirection(); // 确保蛇头方向和蛇身第一节方向相反
                MessageBox.Show($"Level Up! You are now on level {_level}.", "Snake Game", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EndGame()
        {
            _gameTickTimer.Stop();
            _foodSpawnTimer.Stop();
            _backgroundMusicPlayer.Stop();
            MessageBoxResult result = MessageBox.Show($"Game Over! You reached level {_level}.\nDo you want to play again?", "Snake Game", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                StartNewGame();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_snakeParts.Count > 1)
            {
                Point head = _snakeParts[0];
                Point neck = _snakeParts[1];

                switch (e.Key)
                {
                    case Key.Left:
                        if (_snakeDirection != SnakeDirection.Right && head.X - 1 != neck.X)
                            _snakeDirection = SnakeDirection.Left;
                        break;
                    case Key.Right:
                        if (_snakeDirection != SnakeDirection.Left && head.X + 1 != neck.X)
                            _snakeDirection = SnakeDirection.Right;
                        break;
                    case Key.Up:
                        if (_snakeDirection != SnakeDirection.Down && head.Y - 1 != neck.Y)
                            _snakeDirection = SnakeDirection.Up;
                        break;
                    case Key.Down:
                        if (_snakeDirection != SnakeDirection.Up && head.Y + 1 != neck.Y)
                            _snakeDirection = SnakeDirection.Down;
                        break;
                    case Key.P:
                        TogglePause();
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        if (_snakeDirection != SnakeDirection.Right)
                            _snakeDirection = SnakeDirection.Left;
                        break;
                    case Key.Right:
                        if (_snakeDirection != SnakeDirection.Left)
                            _snakeDirection = SnakeDirection.Right;
                        break;
                    case Key.Up:
                        if (_snakeDirection != SnakeDirection.Down)
                            _snakeDirection = SnakeDirection.Up;
                        break;
                    case Key.Down:
                        if (_snakeDirection != SnakeDirection.Up)
                            _snakeDirection = SnakeDirection.Down;
                        break;
                    case Key.P:
                        TogglePause();
                        break;
                }
            }
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_isPaused)
            {
                _backgroundMusicPlayer.Pause();
            }
            else
            {
                _backgroundMusicPlayer.Play();
            }
        }
    }
}