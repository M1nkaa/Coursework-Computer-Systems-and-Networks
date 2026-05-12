using System.Windows;
using System.Windows.Media.Animation;

namespace RPS.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void AnimationBorder_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (AnimationBorder.IsVisible)
            {
                // Сброс трансформаций
                LeftHandTransform.Y = 0;
                RightHandTransform.Y = 0;

                // Создание анимации для левой руки
                var leftAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(System.TimeSpan.FromSeconds(1.2))
                };

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0))));
                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.1))));
                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.2))));

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.4))));

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.5))));
                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.6))));

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.8))));

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.9))));
                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.0))));

                leftAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.2))));

                // Создание анимации для правой руки (такая же)
                var rightAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(System.TimeSpan.FromSeconds(1.2))
                };

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0))));
                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.1))));
                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.2))));

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.4))));

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.5))));
                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.6))));

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.8))));

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.9))));
                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.0))));

                rightAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.2))));

                // Запуск анимаций
                LeftHandTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, leftAnimation);
                RightHandTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, rightAnimation);
            }
        }
    }
}